using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Workers
{
    internal class DataPreProcessingWorker : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly IDataBufferReader<OhlcRecordBase> _bufferIn;
        private readonly IDataBufferWriter<NormalizedOhlcRecord> _bufferOut;
        private readonly ICryptoDataNormalizer _normalizer;
        private readonly string _outputDir;
        private readonly IDataPersistence _persistence;
        private readonly int _lookBackBatchSize;
        private bool _disposed;

        public DataPreProcessingWorker(
            ILogger<DataIngestWorker> logger,
            IDataBufferReader<OhlcRecordBase> bufferIn,
            IDataBufferWriter<NormalizedOhlcRecord> bufferOut,
            ICryptoDataNormalizer normalizer,
            IDataPersistence persistence,
            GlobalConfiguration config)
        {
            _logger = logger;
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _normalizer = normalizer;
            _persistence = persistence;
            _outputDir = config.ProcessedDataDirectory;
            _lookBackBatchSize = config.LookBackBatchSize;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var lookBackQueue = new Queue<OhlcRecordBase>();

            await foreach (OhlcRecordBase data in _bufferIn.GetDataAsync(stoppingToken))
            {
                try
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    //add to batch
                    lookBackQueue.Enqueue(data);

                    //process if queue reaches batch size
                    if (lookBackQueue.Count == _lookBackBatchSize)
                    {
                        //copy queue locally
                        var localLookBack = lookBackQueue.ToList();
                        //normalize
                        var normalizedData = _normalizer.Normalize(localLookBack).ToList();

                        //post to buffer
                        foreach (var item in normalizedData)
                            _bufferOut.AddData(item, stoppingToken);

                        //write to file
                        await _persistence.WriteToDirectoryAsync(_outputDir, normalizedData, stoppingToken);

                        //remove one for sliding window
                        lookBackQueue.Dequeue();
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to run OHLC data preprocessing. Error occurred during preprocessing worker loop. ");
                }
            }
        }

        // Public implementation of Dispose pattern callable by consumers.
        public override void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed state (managed objects).
                _bufferIn.Dispose();
                _bufferOut.Dispose();
            }

            _disposed = true;
        }
    }
}
