using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using CryptoDataIngest.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
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
                    //init buffers
                    var ingestBuff = new DataBuffer<OhlcRecordBase>();
                    var preProcBuff = new DataBuffer<NormalizedOhlcRecord>();
                    var predBuff = new DataBuffer<PredictedClose>();

                    var config = new GlobalConfiguration();

                    bool trainingIngest = false;

                    services
                        .AddSingleton(config)
                        .AddSingleton<ICryptoDataClient, CryptoDataClient>()
                        .AddSingleton<IModelFormatter, CsvDataFormatter>()
                        .AddSingleton<IDataBufferWriter<OhlcRecordBase>>(ingestBuff)
                        .AddSingleton<IDataBufferWriter<NormalizedOhlcRecord>>(preProcBuff)
                        .AddSingleton<IDataBufferWriter<PredictedClose>>(predBuff)
                        .AddSingleton<IDataBufferReader<OhlcRecordBase>>(ingestBuff)
                        .AddSingleton<IDataBufferReader<NormalizedOhlcRecord>>(preProcBuff)
                        .AddSingleton<IDataBufferReader<PredictedClose>>(predBuff)
                        .AddSingleton<IDataPersistence, DataPersistence>()
                        .AddSingleton<ITradingClientProvider, TradingClientProvider>();


                    if (trainingIngest)
                    {
                        services
                            .AddHostedService<FetchTrainingDataTask>();
                    }
                    else
                    {
                        //get global max and min 
                        var minMaxData = JsonConvert.DeserializeObject<MinMaxModel>(File.ReadAllText(config.MinMaxDataPath));
                        services
                            .AddSingleton(minMaxData)
                            .AddSingleton<ICryptoDataNormalizer>(new CryptoDataNormalizer(minMaxData))
                            .AddHostedService<DataIngestWorker>()
                            .AddHostedService<DataPreProcessingWorker>()
                            .AddHostedService<PredictionWorker>()
                            .AddHostedService<TradingWorker>()
                            .AddHostedService<FolderCleanUpWorker>();
                    }
                });
    }
}
