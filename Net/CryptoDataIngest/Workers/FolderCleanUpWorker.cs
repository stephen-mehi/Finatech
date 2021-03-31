using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Workers
{
    internal class FolderCleanUpWorker : BackgroundService
    {
        private readonly ILogger<DataIngestWorker> _logger;
        private readonly string _outputDir;
        private readonly string _rootEthDir;

        public FolderCleanUpWorker(
            ILogger<DataIngestWorker> logger)
        {
            _logger = logger;
            _rootEthDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ETH");
            _outputDir = Path.Combine(_rootEthDir, "Data");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            //create root data dir if doesn't exist
            Directory.CreateDirectory(_outputDir);

            //continue reading batches of data from api until stopped
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    //calculate current unix time
                    long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                    //get files older than a week
                    var oldFiles = 
                        Directory
                        .GetFiles(_outputDir)
                        .Where(f => currentUnixTimestamp - long.Parse(Path.GetFileNameWithoutExtension(f)) > 604800);

                    //delete all old files
                    foreach (var path in oldFiles)
                        File.Delete(path);
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to run folder cleanup. Error occurred during folder clean up worker loop. ");
                }

                //run every day
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}
