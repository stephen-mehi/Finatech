using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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
        private readonly IDataBufferReader<(TimeIntervalEnum, OhlcRecordBase)> _bufferIn;
        private readonly IDataBufferWriter<(TimeIntervalEnum, ScaledOhlcRecord)> _bufferOut;
        private readonly IMinMaxScalerProvider _scalerProv;
        private readonly string _outputDir;
        private readonly IDataPersistence _persistence;
        private readonly int _lookBackBatchSize;
        private readonly GlobalConfiguration _config;
        private readonly IReadOnlyDictionary<TimeIntervalEnum, Queue<OhlcRecordBase>> _lookBackQueues;
        private readonly ConsoleColor _consoleColor = ConsoleColor.Magenta;
        private bool _disposed;

        public DataPreProcessingWorker(
            ILogger<DataIngestWorker> logger,
            IDataBufferReader<(TimeIntervalEnum, OhlcRecordBase)> bufferIn,
            IDataBufferWriter<(TimeIntervalEnum, ScaledOhlcRecord)> bufferOut,
            IMinMaxScalerProvider scalerProv,
            IDataPersistence persistence,
            GlobalConfiguration config)
        {
            _logger = logger;
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _scalerProv = scalerProv;
            _persistence = persistence;
            _outputDir = config.ProcessedDataDirectory;
            _lookBackBatchSize = config.HyperParams.LookBack;
            _config = config;
            _lookBackQueues =
                config
                .TimeIntervals
                .ToDictionary(x => x, x => new Queue<OhlcRecordBase>());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var data in _bufferIn.GetDataAsync(stoppingToken))
            {
                try
                {
                    string minMaxPath = Path.Combine(_config.MinMaxDataDirectory, $"minmax_{data.Item1}.json");

                    //skip data if we don't have min max data established
                    if (!File.Exists(minMaxPath))
                        continue;

                    //get global max and min for current interval
                    var minMaxData = JsonConvert.DeserializeObject<MinMaxModel>(File.ReadAllText(Path.Combine(_config.MinMaxDataDirectory, $"minmax_{data.Item1}.json")));
                    //get scaler
                    var scaler = _scalerProv.Get(minMaxData);

                    if (stoppingToken.IsCancellationRequested)
                        break;

                    if (!_lookBackQueues.TryGetValue(data.Item1, out var targetQueue))
                        throw new ArgumentException($"Failed to get lookback queue, interval: {data.Item1} not configured");

                    //add to batch
                    targetQueue.Enqueue(data.Item2);

                    //process if queue reaches batch size
                    if (targetQueue.Count == _lookBackBatchSize)
                    {
                        var currentInterval = data.Item1;
                        ColorConsole.WriteLineWithTimestamp($"Starting to pre-process for interval: {currentInterval}", _consoleColor);
                        //copy queue locally
                        var localLookBack = targetQueue.ToList();
                        //scale
                        var scaledData = scaler.Scale(localLookBack).ToList();
                        ColorConsole.WriteLineWithTimestamp($"Completed pre-process scaling for interval: {currentInterval}. Posting to out buffer...", _consoleColor);

                        //post to buffer
                        foreach (var item in scaledData)
                            _bufferOut.AddData((currentInterval, item), stoppingToken);
                        ColorConsole.WriteLineWithTimestamp($"Completed posting to out buffer for interval: {currentInterval}. Persisting to file...", _consoleColor);

                        //write to file
                        await _persistence.WriteToDirectoryAsync(_outputDir, scaledData, stoppingToken);
                        ColorConsole.WriteLineWithTimestamp($"Completed persisting to file for interval: {currentInterval}", _consoleColor);

                        //remove one for sliding window
                        targetQueue.Dequeue();
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to run OHLC data preprocessing. Error occurred during preprocessing worker loop. ");
                }
                finally
                {
                    await Task.Delay(5000, stoppingToken);
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
