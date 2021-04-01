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
        private readonly IModelFormatter _dataFormatter;
        private readonly IDataBufferWriter<OhlcRecordBase> _bufferOut;
        private readonly TimeIntervalEnum _batchInterval;
        private readonly string _cryptoSymbol;
        private readonly string _currencySymbol;
        private readonly string _outputDir;
        private readonly string _rootEthDir;
        private readonly string _lastTimeStampFilePath;
        private readonly long _fileExpiration = 60*60*24*5;
        private bool _disposed;

        public DataIngestWorker(
            ILogger<DataIngestWorker> logger,
            ICryptoDataClient dataClient,
            IModelFormatter dataFormatter,
            IDataBufferWriter<OhlcRecordBase> bufferOut)
        {
            _logger = logger;
            _dataFormatter = dataFormatter;
            _dataClient = dataClient;
            _bufferOut = bufferOut;

            _batchInterval = TimeIntervalEnum.fiveMinute;
            _cryptoSymbol = "ETH";
            _currencySymbol = "USDT";

            _rootEthDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ETH");
            _outputDir = Path.Combine(_rootEthDir, "Data");
            _lastTimeStampFilePath = Path.Combine(_rootEthDir, "LastTimeStamp.txt");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //create root data dir if doesn't exist
            Directory.CreateDirectory(_outputDir);

            //create last timestamp file if it doesn't exist. init with 0
            if (!File.Exists(_lastTimeStampFilePath))
                await File.WriteAllTextAsync(_lastTimeStampFilePath, "0", stoppingToken);

            try
            {
                //continue reading batches of data from api until stopped
                while (!stoppingToken.IsCancellationRequested)
                {
                    //calculate current unix time
                    long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                    //read last logged data timestamp
                    long lastTimeStamp = long.Parse(File.ReadAllText(_lastTimeStampFilePath));
                    //if no last ts, just grap last intervals worth
                    if (lastTimeStamp == 0)
                        lastTimeStamp = currentUnixTimestamp - (int)_batchInterval;

                    //get all file names and parse to unix time
                    var fileTimeStamps =
                        Directory
                        .GetFiles(_outputDir)
                        .Select(p => long.Parse(Path.GetFileNameWithoutExtension(p)))
                        .ToList();

                    //end time is at most 1000*interval
                    var endTime = lastTimeStamp + (1000 * (int)_batchInterval);
                    //cieling to current time if would extend into future
                    if (currentUnixTimestamp < endTime)
                        endTime = currentUnixTimestamp;

                    //fetch batch of data filtering for data occuring after last logged timestamp
                    var rawData = await _dataClient.GetDataAsync(_cryptoSymbol, _currencySymbol, _batchInterval, lastTimeStamp, endTime,  stoppingToken);

                    //post data to buffer on thread
                    var postToBufferTask = Task.Run(() =>
                    {
                        foreach (var item in rawData)
                            _bufferOut.AddData(item, stoppingToken);

                    }, stoppingToken);

                    //format/supplement data
                    var formattedData = $"{string.Join(Environment.NewLine, _dataFormatter.Format(rawData))}{Environment.NewLine}";

                    //try to get max 
                    long fileTimestamp = default;
                    long mostRecentFileTimeStamp = fileTimeStamps.DefaultIfEmpty().Max();

                    //create new file with current timestamp if no previous files or if most recent file was made more than a day ago
                    if (fileTimeStamps.Count == 0 || currentUnixTimestamp - mostRecentFileTimeStamp > _fileExpiration)
                    {
                        formattedData = $"{_dataFormatter.GetHeader<OhlcRecordBase>()}{Environment.NewLine}{formattedData}";
                        fileTimestamp = currentUnixTimestamp;
                    }
                    else
                        fileTimestamp = mostRecentFileTimeStamp;

                    //write batch of new data to file. Create new file every day
                    await File.AppendAllTextAsync(Path.Combine(_outputDir, $"{fileTimestamp}.csv"), formattedData, stoppingToken);

                    //delete old last timestamp file
                    File.Delete(_lastTimeStampFilePath);
                    //create new one with last data timestamp read
                    await File.WriteAllTextAsync(_lastTimeStampFilePath, endTime.ToString(), stoppingToken);

                    //finally await buffer posting
                    await postToBufferTask;

                    await Task.Delay(TimeSpan.FromSeconds((int)_batchInterval), stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run OHLC data ingest. Error occurred during ingest worker loop. ");
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
