using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using CryptoDataIngest.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CryptoDataIngest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureServices((hostContext, services) =>
                {
                    services
                        .AddSingleton<ICryptoDataClient, CryptoDataClient>()
                        .AddSingleton<IModelFormatter, CsvDataFormatter>()
                        .AddSingleton<IDataBuffer<OhlcRecordBase>, DataBuffer<OhlcRecordBase>>()
                        .AddSingleton<IDataBuffer<NormalizedOhlcRecord>, DataBuffer<NormalizedOhlcRecord>>()
                        .AddSingleton<IDataBuffer<PredictedClose>, DataBuffer<PredictedClose>>()
                        .AddSingleton<ICryptoDataNormalizer, CryptoDataNormalizer>()
                        //.AddHostedService<FetchTrainingDataTask>()
                        //.AddHostedService<DataIngestWorker>()
                        //.AddHostedService<DataPreProcessingWorker>()
                        .AddHostedService<PredictionWorker>();
                        //.AddHostedService<FolderCleanUpWorker>();
                });
    }
}
