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
    internal class FetchTrainingDataTask : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly ICryptoDataClient _dataClient;
        private readonly IModelFormatter _dataFormatter;
        private readonly ICryptoDataNormalizer _dataNormalizer;
        private readonly TimeIntervalEnum _interval;
        private readonly string _cryptoSymbol;
        private readonly string _currencySymbol;
        private readonly string _outputDir;
        private readonly string _outputPath;
        private readonly string _rootEthDir;
        private const int _batchSize = 500;

        private IReadOnlyList<(DateTime start, DateTime end)> _trainingDataTimePeriods =
            new List<(DateTime start, DateTime end)>()
            {
                (new DateTime(2018, 5, 29), new DateTime(2018, 12, 31)),
                (new DateTime(2019, 1, 12), new DateTime(2019, 4, 14)),
                (new DateTime(2019, 5, 31), new DateTime(2019, 9, 23)),
                (new DateTime(2019, 9, 28), new DateTime(2019, 12, 23)),
                (new DateTime(2020, 1, 23), new DateTime(2020, 3, 1)),
                (new DateTime(2020, 3, 9), new DateTime(2020, 4, 16)),
                (new DateTime(2020, 5, 12), new DateTime(2020, 10, 26))
                //(new DateTime(2020, 11, 9), new DateTime(2021, 3, 16)),
            };

        public FetchTrainingDataTask(
            ILogger<DataIngestWorker> logger,
            ICryptoDataClient dataClient,
            IModelFormatter dataFormatter,
            ICryptoDataNormalizer normalizer)
        {
            _dataNormalizer = normalizer;
            _logger = logger;
            _dataFormatter = dataFormatter;
            _dataClient = dataClient;

            _interval = TimeIntervalEnum.fiveMinute;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _rootEthDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ETH");
            _outputDir = Path.Combine(_rootEthDir, "TrainingData");
            _outputPath = Path.Combine(_outputDir, "training.csv");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //create root data dir if doesn't exist
            Directory.CreateDirectory(_outputDir);

            if (File.Exists(_outputPath))
                File.Delete(_outputPath);

            try
            {
                var rawData = new List<OhlcRecordBase>();

                foreach (var (start, end) in _trainingDataTimePeriods)
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
                        rawData.AddRange(await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, _interval, currentStart, currentEnd, stoppingToken));
                    }
                }

                //normalize data
                //var normalizedData = _dataNormalizer.Normalize(rawData);

                //format/supplement data
                var formattedData = 
                    $"{_dataFormatter.GetHeader<NormalizedOhlcRecord>()}" +
                    $"{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, _dataFormatter.Format(rawData))}";

                //write batch of new data to file. Create new file every day
                await File.WriteAllTextAsync(_outputPath, formattedData, stoppingToken);

            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to fetch OHLC data for training ");
            }

        }
    }
}
