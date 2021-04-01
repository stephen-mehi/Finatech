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
        private readonly IModelFormatter _formatter;
        private readonly string _outputDir;
        private readonly string _rootEthDir;
        private bool _disposed;

        public DataPreProcessingWorker(
            ILogger<DataIngestWorker> logger,
            IDataBufferReader<OhlcRecordBase> bufferIn,
            IDataBufferWriter<NormalizedOhlcRecord> bufferOut,
            IModelFormatter formatter,
            ICryptoDataNormalizer normalizer)
        {
            _logger = logger;
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _normalizer = normalizer;
            _formatter = formatter;

            _rootEthDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ETH");
            _outputDir = Path.Combine(_rootEthDir, "NormalizedData");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                Directory.CreateDirectory(_outputDir);

                await foreach (OhlcRecordBase data in _bufferIn.GetDataAsync(stoppingToken))
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    //normalize
                    var normalizedData = _normalizer.Normalize(new List<OhlcRecordBase>() { data });
                    _bufferOut.AddData(normalizedData.FirstOrDefault(), stoppingToken);

                    //format normalized data
                    var formatted = $"{_formatter.GetHeader<NormalizedOhlcRecord>()}{Environment.NewLine}{string.Join(Environment.NewLine, _formatter.Format(normalizedData))}";

                    //calculate current unix time
                    long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                    //write data to file
                    await File.WriteAllTextAsync(Path.Combine(_outputDir, $"{currentUnixTimestamp}.csv"), formatted, stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run OHLC data preprocessing. Error occurred during preprocessing worker loop. ");
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
