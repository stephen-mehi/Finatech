using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using CryptoDataIngest.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Numpy;
using Python.Runtime;
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
                    np.arange(1);
                    PythonEngine.BeginAllowThreads();

                    //init buffers
                    var modelBuff = new DataBuffer<ModelSource>();
                    var sourceBuff = new DataBuffer<OhlcRecordBaseBatch>();
                    var ingestBuff = new DataBuffer<OhlcRecordBase>();
                    var preProcBuff = new DataBuffer<ScaledOhlcRecord>();
                    var predBuff = new DataBuffer<PredictedClose>();

                    var config = new GlobalConfiguration();

                    services
                        .AddSingleton(config)
                        .AddSingleton<IModelFormatter, CsvDataFormatter>()
                        .AddSingleton<IDataBufferWriter<ModelSource>>(modelBuff)
                        .AddSingleton<IDataBufferWriter<OhlcRecordBaseBatch>>(sourceBuff)
                        .AddSingleton<IDataBufferWriter<OhlcRecordBase>>(ingestBuff)
                        .AddSingleton<IDataBufferWriter<ScaledOhlcRecord>>(preProcBuff)
                        .AddSingleton<IDataBufferWriter<PredictedClose>>(predBuff)
                        .AddSingleton<IDataBufferReader<ModelSource>>(modelBuff)
                        .AddSingleton<IDataBufferReader<OhlcRecordBaseBatch>>(sourceBuff)
                        .AddSingleton<IDataBufferReader<OhlcRecordBase>>(ingestBuff)
                        .AddSingleton<IDataBufferReader<ScaledOhlcRecord>>(preProcBuff)
                        .AddSingleton<IDataBufferReader<PredictedClose>>(predBuff)
                        .AddSingleton<IDataPersistence, DataPersistence>()
                        .AddSingleton<ICryptoDataClient, CryptoDataClient>()
                        .AddSingleton<IDataConvolve, DataConvolve>()
                        .AddSingleton<IMinMaxSelectorProvider, MinMaxSelectorProvider>()
                        .AddSingleton<ITradingClientProvider, TradingClientProvider>()
                        .AddSingleton<IMinMaxScalerProvider, MinMaxScalerProvider>()
                        .AddHostedService<ModelTrainerWorker>()
                        .AddHostedService<FetchTrainingDataTask>()
                        .AddHostedService<DataIngestWorker>()
                        .AddHostedService<DataPreProcessingWorker>()
                        .AddHostedService<PredictionWorker>()
                        .AddHostedService<TradingWorker>()
                        .AddHostedService<FolderCleanUpWorker>();
                });
    }
}
