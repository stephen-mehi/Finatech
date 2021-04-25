using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Python.Runtime;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Workers
{
    internal class FetchTrainingDataWorker : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly ICryptoDataClient _dataClient;
        private readonly IModelFormatter _dataFormatter;
        private readonly IReadOnlyList<TimeIntervalEnum> _intervals;
        private readonly string _cryptoSymbol;
        private readonly string _currencySymbol;
        private readonly string _outputDir;
        private readonly string _outputPath;
        private readonly GlobalConfiguration _config;
        private IReadOnlyList<(DateTime start, DateTime end)> _dataTimeRanges;
        private readonly IDataBufferWriter<OhlcRecordBaseBatch> _bufferOut;
        private readonly IMinMaxSelectorProvider _minMaxSelectorProv;
        private readonly IModelSourceRepo _modelSourceRepo;
        private const int _batchSize = 500;
        private readonly ConsoleColor _consoleColor = ConsoleColor.Cyan;
        private bool _disposed;

        public FetchTrainingDataWorker(
            ILogger<DataIngestWorker> logger,
            ICryptoDataClient dataClient,
            IModelFormatter dataFormatter,
            IDataBufferWriter<OhlcRecordBaseBatch> bufferOut,
            IMinMaxSelectorProvider minMaxSelectorProv,
            GlobalConfiguration config,
            IModelSourceRepo modelSrcRepo)
        {
            _modelSourceRepo = modelSrcRepo;
            _config = config;
            _bufferOut = bufferOut;
            _logger = logger;
            _dataFormatter = dataFormatter;
            _dataClient = dataClient;

            _intervals = config.TimeIntervals;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _minMaxSelectorProv = minMaxSelectorProv;

            _outputDir = Path.GetDirectoryName(config.TrainingDataDirectory);
            _outputPath = config.TrainingDataDirectory;
            _dataTimeRanges = GlobalConfiguration.TrainingDataTimePeriods;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ColorConsole.WriteLine($"Starting to fetch training data for date range: {_dataTimeRanges.First().start} TO {_dataTimeRanges.Last().end}", _consoleColor);
                //create root data dir if doesn't exist
                Directory.CreateDirectory(_outputDir);

                Directory.CreateDirectory(_config.MinMaxDataDirectory);

                if (File.Exists(_outputPath))
                    File.Delete(_outputPath);

                var minMaxSelector = _minMaxSelectorProv.Get();

                try
                {
                    //get any recent models that are not expired for this time interval
                    var recentModels =
                        _modelSourceRepo
                        .ListModelSources()
                        .GroupBy(x => x.Interval)
                        .Select(x => x.OrderByDescending(x => x.Timestamp).FirstOrDefault())
                        .Where(x => (DateTime.Now - x.Timestamp).TotalHours < _config.ModelRetrainDelayHours)
                        .ToDictionary(x => x.Interval, x => x.Timestamp);

                    foreach (var interval in _intervals)
                    {
                        //if any non-expired models found for this interval, just skip loop
                        if (recentModels.TryGetValue(interval, out var recentModel))
                            continue;

                        string minMaxPath = Path.Combine(_config.MinMaxDataDirectory, $"minmax_{interval}.json");

                        //delete old minmax data
                        if (File.Exists(minMaxPath))
                            File.Delete(minMaxPath);

                        ColorConsole.WriteLine($"Starting to get data for time interval: {interval}", _consoleColor);
                        var rawData = new List<OhlcRecordBase>();

                        foreach (var (start, end) in _dataTimeRanges)
                        {
                            long unixStart = (long)start.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                            long unixEnd = (long)end.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                            long range = unixEnd - unixStart;
                            long nRecords = range / (int)interval;
                            long nBatches = nRecords / _batchSize;
                            long leftovers = nRecords % _batchSize;
                            bool hasLeftovers = leftovers > 0;

                            //add additional loop if have leftovers
                            if (hasLeftovers)
                                nBatches++;

                            ColorConsole.WriteLine($"Incoming training data split into: {nBatches} batches", _consoleColor);

                            for (int i = 0; i < nBatches; i++)
                            {
                                long currentStart = unixStart + (_batchSize * (int)interval * i);
                                long currentEnd = i == nBatches && hasLeftovers ? currentStart + (leftovers * (int)interval) : currentStart + (_batchSize * (int)interval);
                                //fetch batch of data filtering for data occuring after last logged timestamp
                                var batchedData = await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, interval, currentStart, currentEnd, stoppingToken);

                                foreach (var dataPoint in batchedData)
                                {
                                    minMaxSelector.Assess(dataPoint);
                                    rawData.Add(dataPoint);
                                }

                                ColorConsole.WriteLine($"Completed batch: {i}", _consoleColor);
                            }
                        }

                        ColorConsole.WriteLine($"Completed fetching all training data for interval: {interval}. Writing to outgoing buffer..", _consoleColor);

                        //write batch of data to out buffer
                        _bufferOut.AddData(new OhlcRecordBaseBatch(rawData, interval), stoppingToken);

                        ColorConsole.WriteLine($"Completed writing to outgoing buffer for interval: {interval}. Persisting data..", _consoleColor);

                        //format/supplement data
                        var formattedData =
                            $"{_dataFormatter.GetHeader<ScaledOhlcRecord>()}" +
                            $"{Environment.NewLine}" +
                            $"{string.Join(Environment.NewLine, _dataFormatter.Format(rawData))}";

                        //write batch of new data to file. Create new file every day
                        await File.WriteAllTextAsync(Path.Combine(_outputPath, $"training_{interval}.csv"), formattedData, stoppingToken);

                        ColorConsole.WriteLine($"Completed persisting data for interval: {interval}. Persisting min max data..", _consoleColor);

                        //persist min-max data
                        await File.WriteAllTextAsync(minMaxPath, JsonConvert.SerializeObject(minMaxSelector.GetCurrentMinMax()), stoppingToken);

                        ColorConsole.WriteLine($"Completed persisting min max data for interval: {interval}.", _consoleColor);
                    }

                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to fetch OHLC data for training ");
                }

                //check if retrain needs to happen every hour
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
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
                _bufferOut.Dispose();
            }

            _disposed = true;
        }
    }
}
