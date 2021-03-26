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

namespace CryptoDataIngest
{
    internal class DataPreProcessingWorker : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly BlockingCollection<OhlcRecordBase> _bufferIn;
        private readonly BlockingCollection<NormalizedOhlcRecord> _bufferOut;
        private readonly ICryptoDataNormalizer _normalizer;
        private readonly IModelFormatter _formatter;
        private readonly string _outputDir;
        private readonly string _rootEthDir;
        private const int _batchSize = 6;

        public DataPreProcessingWorker(
            ILogger<DataIngestWorker> logger,
            IIngestedDataBuffer<OhlcRecordBase> bufferIn,
            IIngestedDataBuffer<NormalizedOhlcRecord> bufferOut,
            IModelFormatter formatter,
            ICryptoDataNormalizer normalizer)
        {
            _logger = logger;
            _bufferIn = bufferIn.GetDataBuffer();
            _bufferOut = bufferOut.GetDataBuffer();
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
                var batch = new Queue<OhlcRecordBase>();

                while (!_bufferIn.IsCompleted)
                {
                    OhlcRecordBase data = default;
                    bool foundData = default;

                    try
                    {
                        foundData = _bufferIn.TryTake(out data);
                    }
                    //ignore taking an item if the collection is 'complete' since will just exit on next loop
                    catch (InvalidOperationException) { }

                    //operate on data if found
                    if (!foundData)
                        await Task.Delay(250, stoppingToken);
                    else
                    {
                        //add to batch
                        batch.Enqueue(data);

                        //process if queue reaches batch size
                        if (batch.Count == _batchSize)
                        {
                            //normalize
                            var normalizedData = _normalizer.Normalize(batch.DequeueAll().ToList());

                            //queue into outgoing buffer
                            var postDataTask = Task.Run(() =>
                            {
                                foreach (var item in normalizedData)
                                    _bufferOut.Add(item, stoppingToken);
                            }, stoppingToken);

                            //format normalized data
                            var formatted = $"{_formatter.GetHeader<NormalizedOhlcRecord>()}{Environment.NewLine}{string.Join(Environment.NewLine, _formatter.Format(normalizedData))}";

                            //calculate current unix time
                            long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                            //write data to file
                            await File.WriteAllTextAsync(Path.Combine(_outputDir, $"{currentUnixTimestamp}.csv"), formatted, stoppingToken);
                            //finally await writing to out buffer
                            await postDataTask;
                        }
                    }

                    await Task.Delay(100, stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run OHLC data preprocessing. Error occurred during preprocessing worker loop. ");
            }
            finally
            {
                _bufferOut.CompleteAdding();
            }
        }
    }
}
