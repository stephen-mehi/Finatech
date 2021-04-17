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
    internal class DataIngestWorker : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly ICryptoDataClient _dataClient;
        private readonly IDataBufferWriter<OhlcRecordBase> _bufferOut;
        private readonly TimeIntervalEnum _batchInterval;
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
            IDataBufferWriter<OhlcRecordBase> bufferOut,
            GlobalConfiguration config)
        {
            _config = config;
            _persistence = persistence;
            _logger = logger;
            _dataClient = dataClient;
            _bufferOut = bufferOut;

            _batchInterval = config.TimeInterval;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _rootEthDir = config.RootDataDirectory;
            _outputDir = config.RawDataPathDirectory;
            _lastTimeStampFilePath = Path.Combine(_rootEthDir, "LastTimeStamp.txt");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //create last timestamp file if it doesn't exist. init with 0
            if (!File.Exists(_lastTimeStampFilePath))
                await File.WriteAllTextAsync(_lastTimeStampFilePath, "0", stoppingToken);


            //continue reading batches of data from api until stopped
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //calculate current unix time
                    long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                    //read last logged data timestamp
                    long lastTimeStamp = long.Parse(File.ReadAllText(_lastTimeStampFilePath));
                    //if no last ts, just grab last intervals worth, with buffer time to ensure data grabbed
                    if (lastTimeStamp == 0)
                        lastTimeStamp = currentUnixTimestamp - (int)_batchInterval - 60;

                    //end time is at most 1000*interval
                    var endTime = lastTimeStamp + (1000 * (int)_batchInterval);
                    //cieling to current time if would extend into future
                    if (currentUnixTimestamp < endTime)
                        endTime = currentUnixTimestamp;

                    //fetch batch of data filtering for data occuring after last logged timestamp
                    var rawData = await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, _batchInterval, lastTimeStamp, endTime, stoppingToken);
                    //filter out bad data
                    var cleanedData = rawData.Where(x => x.date != 0);

                    //only process and post if some data found
                    if (cleanedData.Any())
                    {
                        //post to out buffer
                        foreach (var item in cleanedData)
                            _bufferOut.AddData(item, stoppingToken);

                        await _persistence.WriteToDirectoryAsync(_outputDir, cleanedData, stoppingToken);

                        //delete old last timestamp file
                        File.Delete(_lastTimeStampFilePath);
                        //create new one with last data timestamp read
                        await File.WriteAllTextAsync(_lastTimeStampFilePath, endTime.ToString(), stoppingToken);
                    }

                    //check for new data 4 times an interval
                    var delaySeconds = TimeSpan.FromSeconds((int)Math.Round(0.25 * (int)_config.TimeInterval));
                    //check every 60 seconds for new data 
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
