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
    internal class DataIngestWorker : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly ICryptoDataClient _dataClient;
        private readonly IDataBufferWriter<(TimeIntervalEnum, OhlcRecordBase)> _bufferOut;
        private readonly IReadOnlyList<TimeIntervalEnum> _intervals;
        private readonly int _dataReadLoopDelay;
        private readonly IDataPersistence _persistence;
        private readonly string _cryptoSymbol;
        private readonly string _currencySymbol;
        private readonly string _outputDir;
        private readonly string _rootEthDir;
        private readonly string _lastTimeStampFilePath;
        private readonly GlobalConfiguration _config;
        private bool _disposed;

        public DataIngestWorker(
            ILogger<DataIngestWorker> logger,
            ICryptoDataClient dataClient,
            IDataPersistence persistence,
            IDataBufferWriter<(TimeIntervalEnum, OhlcRecordBase)> bufferOut,
            GlobalConfiguration config)
        {
            _config = config;
            _persistence = persistence;
            _logger = logger;
            _dataClient = dataClient;
            _bufferOut = bufferOut;

            _intervals = config.TimeIntervals;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _rootEthDir = config.RootDataDirectory;
            _outputDir = config.RawDataPathDirectory;
            _lastTimeStampFilePath = Path.Combine(_rootEthDir, "LastTimeStamp.txt");

            _dataReadLoopDelay = _intervals.Select(x => (int)x).Min();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //create last timestamp file if it doesn't exist. init with 0
            if (!File.Exists(_lastTimeStampFilePath))
                await File.WriteAllTextAsync(_lastTimeStampFilePath, JsonConvert.SerializeObject(_config.TimeIntervals.ToDictionary(x => x, x => (long)0)), stoppingToken);


            //continue reading batches of data from api until stopped
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //read last logged data timestamp
                    var lastTimeStampMap = JsonConvert.DeserializeObject<Dictionary<TimeIntervalEnum, long>>(File.ReadAllText(_lastTimeStampFilePath));

                    foreach (var interval in _intervals)
                    {
                        //calculate current unix time
                        long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                        var lastTimeStamp = lastTimeStampMap[interval];

                        //if no last ts, just grab last intervals worth, with buffer time to ensure data grabbed
                        if (lastTimeStamp == 0)
                            lastTimeStamp = currentUnixTimestamp - (int)interval - 60;

                        //end time is at most 1000*interval
                        var endTime = lastTimeStamp + (1000 * (int)interval);
                        //cieling to current time if would extend into future
                        if (currentUnixTimestamp < endTime)
                            endTime = currentUnixTimestamp;

                        //fetch batch of data filtering for data occuring after last logged timestamp
                        var rawData = await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, interval, lastTimeStamp, endTime, stoppingToken);
                        //filter out bad data
                        var cleanedData = rawData.Where(x => x.date != 0);

                        //only process and post if some data found
                        if (cleanedData.Any())
                        {
                            //post to out buffer
                            foreach (var item in cleanedData)
                                _bufferOut.AddData((interval, item), stoppingToken);

                            await _persistence.WriteToDirectoryAsync(_outputDir, cleanedData, stoppingToken);

                            //delete old last timestamp file
                            File.Delete(_lastTimeStampFilePath);
                            //create new one with last data timestamp read
                            lastTimeStampMap[interval] = endTime;
                            await File.WriteAllTextAsync(_lastTimeStampFilePath, JsonConvert.SerializeObject(lastTimeStampMap), stoppingToken);

                        }
                    }

                    //check for new data based on the minimum configured time interval 
                    var delaySeconds = TimeSpan.FromSeconds(_dataReadLoopDelay);
                    await Task.Delay(delaySeconds, stoppingToken);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to run OHLC data ingest. Error occurred during ingest worker loop. ");
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
                _bufferOut.Dispose();
            }

            _disposed = true;
        }
    }
}
