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
        private readonly TimeIntervalEnum _interval;
        private readonly string _cryptoSymbol;
        private readonly string _currencySymbol;
        private readonly string _outputDir;
        private readonly string _outputPath;
        private readonly string _minMaxDir;
        private readonly string _minMaxPath;
        private readonly GlobalConfiguration _config;
        private IReadOnlyList<(DateTime start, DateTime end)> _dataTimeRanges;
        private readonly IDataBufferWriter<OhlcRecordBaseBatch> _bufferOut;
        private readonly IMinMaxSelectorProvider _minMaxSelectorProv;
        private readonly IModelSourceRepo _modelSourceRepo;
        private const int _batchSize = 500;
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

            _interval = config.TimeInterval;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _minMaxSelectorProv = minMaxSelectorProv;

            _outputDir = Path.GetDirectoryName(config.TrainingDataPath);
            _outputPath = config.TrainingDataPath;
            _minMaxDir = Path.GetDirectoryName(config.MinMaxDataPath);
            _minMaxPath = config.MinMaxDataPath;
            _dataTimeRanges = GlobalConfiguration.TrainingDataTimePeriods;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            //get existing model ts and paths
            var modelTimestampMap = _modelSourceRepo.ListModelSources();

            //if any models found
            if(modelTimestampMap.Count > 0)
            {
                var timeDiffHours = (DateTime.Now - modelTimestampMap.Keys.Max());

                //model exists that is not older than retrain delay, wait difference amount of time before getting data for retrain
                if (timeDiffHours.TotalHours < _config.ModelRetrainDelayHours)
                    await Task.Delay(timeDiffHours, stoppingToken);
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                //create root data dir if doesn't exist
                Directory.CreateDirectory(_outputDir);
                Directory.CreateDirectory(_minMaxDir);

                if (File.Exists(_outputPath))
                    File.Delete(_outputPath);

                if (File.Exists(_minMaxPath))
                    File.Delete(_minMaxPath);

                var minMaxSelector = _minMaxSelectorProv.Get();

                try
                {
                    var rawData = new List<OhlcRecordBase>();

                    foreach (var (start, end) in _dataTimeRanges)
                    {
                        long unixStart = (long)start.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                        long unixEnd = (long)end.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                        long range = unixEnd - unixStart;
                        long nRecords = range / (int)_interval;
                        long nBatches = nRecords / _batchSize;
                        long leftovers = nRecords % _batchSize;
                        bool hasLeftovers = leftovers > 0;

                        //add additional loop if have leftovers
                        if (hasLeftovers)
                            nBatches++;

                        for (int i = 0; i < nBatches; i++)
                        {
                            long currentStart = unixStart + (_batchSize * (int)_interval * i);
                            long currentEnd = i == nBatches && hasLeftovers ? currentStart + (leftovers * (int)_interval) : currentStart + (_batchSize * (int)_interval);
                            //fetch batch of data filtering for data occuring after last logged timestamp
                            var batchedData = await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, _interval, currentStart, currentEnd, stoppingToken);

                            foreach (var dataPoint in batchedData)
                            {
                                minMaxSelector.Assess(dataPoint);
                                rawData.Add(dataPoint);
                            }
                        }
                    }

                    //write batch of data to out buffer
                    _bufferOut.AddData(new OhlcRecordBaseBatch(rawData), stoppingToken);

                    //format/supplement data
                    var formattedData =
                        $"{_dataFormatter.GetHeader<ScaledOhlcRecord>()}" +
                        $"{Environment.NewLine}" +
                        $"{string.Join(Environment.NewLine, _dataFormatter.Format(rawData))}";

                    //write batch of new data to file. Create new file every day
                    await File.WriteAllTextAsync(_outputPath, formattedData, stoppingToken);

                    //persist min-max data
                    await File.WriteAllTextAsync(_minMaxPath, JsonConvert.SerializeObject(minMaxSelector.GetCurrentMinMax()), stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to fetch OHLC data for training ");
                }

                //run every n hours
                await Task.Delay(TimeSpan.FromHours(_config.ModelRetrainDelayHours), stoppingToken);
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
