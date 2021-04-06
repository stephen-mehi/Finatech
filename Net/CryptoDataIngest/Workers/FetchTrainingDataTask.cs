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
    internal class FetchTrainingDataTask : BackgroundService
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
            GlobalConfiguration config)
        {
            _logger = logger;
            _dataFormatter = dataFormatter;
            _dataClient = dataClient;

            _interval = config.TimeInterval;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _outputDir =  Path.GetDirectoryName(config.TrainingDataPath);
            _outputPath = config.TrainingDataPath;
            _minMaxDir = Path.GetDirectoryName(config.MinMaxDataPath);
            _minMaxPath = config.MinMaxDataPath;

        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //create root data dir if doesn't exist
            Directory.CreateDirectory(_outputDir);
            Directory.CreateDirectory(_minMaxDir);

            if (File.Exists(_outputPath))
                File.Delete(_outputPath);

            if (File.Exists(_minMaxPath))
                File.Delete(_minMaxPath);

            try
            {
                //init min-max vars
                double maxOpen, maxHigh, maxLow, maxClose, maxWeightedAvg, maxVol, maxQuoteVol;
                maxOpen = maxHigh = maxLow = maxClose = maxWeightedAvg = maxVol = maxQuoteVol = double.MinValue;
                double minOpen, minHigh, minLow, minClose, minWeightedAvg, minVol, minQuoteVol;
                minOpen = minHigh = minLow = minClose = minWeightedAvg = minVol = minQuoteVol = double.MaxValue;

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
                        var batchedData = await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, _interval, currentStart, currentEnd, stoppingToken);

                        foreach (var dataPoint in batchedData)
                        {
                            //set mins and maxes
                            if (dataPoint.open > maxOpen)
                                maxOpen = dataPoint.open;
                            if (dataPoint.open < minOpen)
                                minOpen = dataPoint.open;
                            if (dataPoint.high > maxHigh)
                                maxHigh = dataPoint.high;
                            if (dataPoint.high < minHigh)
                                minHigh = dataPoint.high;
                            if (dataPoint.low > maxLow)
                                maxLow = dataPoint.low;
                            if (dataPoint.low < minLow)
                                minLow = dataPoint.low;
                            if (dataPoint.close > maxClose)
                                maxClose = dataPoint.close;
                            if (dataPoint.close < minClose)
                                minClose = dataPoint.close;
                            if (dataPoint.weightedAverage > maxWeightedAvg)
                                maxWeightedAvg = dataPoint.weightedAverage;
                            if (dataPoint.weightedAverage < minWeightedAvg)
                                minWeightedAvg = dataPoint.weightedAverage;
                            if (dataPoint.volume > maxVol)
                                maxVol = dataPoint.volume;
                            if (dataPoint.volume < minVol)
                                minVol = dataPoint.volume;
                            if (dataPoint.quoteVolume > maxQuoteVol)
                                maxQuoteVol = dataPoint.quoteVolume;
                            if (dataPoint.quoteVolume < minQuoteVol)
                                minQuoteVol = dataPoint.quoteVolume;

                            rawData.Add(dataPoint);
                        }
                    }
                }

                //format/supplement data
                var formattedData =
                    $"{_dataFormatter.GetHeader<NormalizedOhlcRecord>()}" +
                    $"{Environment.NewLine}" +
                    $"{string.Join(Environment.NewLine, _dataFormatter.Format(rawData))}";

                //write batch of new data to file. Create new file every day
                await File.WriteAllTextAsync(_outputPath, formattedData, stoppingToken);

                //prep min-max data
                var minModel = new OhlcRecordBase(0, minOpen, minHigh, minLow, minClose, minWeightedAvg, minVol, minQuoteVol);
                var maxModel = new OhlcRecordBase(0, maxOpen, maxHigh, maxLow, maxClose, maxWeightedAvg, maxVol, maxQuoteVol);
                //persist min-max data
                await File.WriteAllTextAsync(_minMaxPath, JsonConvert.SerializeObject(new MinMaxModel(minModel, maxModel)), stoppingToken);
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to fetch OHLC data for training ");
            }
        }
    }
}
