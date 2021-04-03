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

                    var ingestBuff = new DataBuffer<OhlcRecordBase>();
                    var preProcBuff = new DataBuffer<NormalizedOhlcRecord>();
                    var predBuff = new DataBuffer<PredictedClose>();

                    services
                        .AddSingleton<GlobalConfiguration>()
                        .AddSingleton<ICryptoDataClient, CryptoDataClient>()
                        .AddSingleton<IModelFormatter, CsvDataFormatter>()
                        .AddSingleton<IDataBufferWriter<OhlcRecordBase>>(ingestBuff)
                        .AddSingleton<IDataBufferWriter<NormalizedOhlcRecord>>(preProcBuff)
                        .AddSingleton<IDataBufferWriter<PredictedClose>>(predBuff)
                        .AddSingleton<IDataBufferReader<OhlcRecordBase>>(ingestBuff)
                        .AddSingleton<IDataBufferReader<NormalizedOhlcRecord>>(preProcBuff)
                        .AddSingleton<IDataBufferReader<PredictedClose>>(predBuff)
                        .AddSingleton<ICryptoDataNormalizer, CryptoDataNormalizer>()
                        .AddSingleton<IDataPersistence, DataPersistence>()
                        //.AddHostedService<FetchTrainingDataTask>()
                        .AddHostedService<DataIngestWorker>()
                        .AddHostedService<DataPreProcessingWorker>()
                        .AddHostedService<PredictionWorker>()
                        .AddHostedService<FolderCleanUpWorker>();
                });
    }
}
