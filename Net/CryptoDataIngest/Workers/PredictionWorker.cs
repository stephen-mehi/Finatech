using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Keras.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Numpy;

namespace CryptoDataIngest.Workers
{
    internal class PredictionWorker : BackgroundService
    {

        private readonly ILogger<PredictionWorker> _logger;
        private readonly IDataBufferReader<NormalizedOhlcRecord> _bufferIn;
        private readonly IDataBufferWriter<PredictedClose> _bufferOut;
        private readonly BaseModel _model;
        private readonly int _lookBackBatchSize;
        private readonly TimeIntervalEnum _timeInterval;
        private readonly IDataPersistence _persistence;
        private readonly string _outputDir;
        private bool _disposed;

        public PredictionWorker(
            ILogger<PredictionWorker> logger,
            IDataBufferReader<NormalizedOhlcRecord> bufferIn,
            IDataBufferWriter<PredictedClose> bufferOut,
            GlobalConfiguration config,
            IDataPersistence persistence)
        {
            _persistence = persistence;
            _timeInterval = config.TimeInterval;
            _lookBackBatchSize = config.LookBackBatchSize;
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _logger = logger;
            _model = BaseModel.ModelFromJson(File.ReadAllText(@"C:\ProgramData\ETH\Model\model.json"));
            _model.LoadWeight(@"C:\ProgramData\ETH\Model\model.h5");
            _outputDir = config.PredictionDataDirectory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var lookBackQueue = new Queue<NormalizedOhlcRecord>();

                await foreach (NormalizedOhlcRecord data in _bufferIn.GetDataAsync(stoppingToken))
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    //add to batch
                    lookBackQueue.Enqueue(data);

                    //process if queue reaches batch size
                    if (lookBackQueue.Count == _lookBackBatchSize)
                    {
                        //copy lookback timesteps
                        var localLookBack = lookBackQueue.ToList();

                        var inputData = localLookBack.Select(x => new { x.high, x.low, x.open, x.weightedAverage, x.close });
                        //init 3d array 
                        var inputDataArray =  new double[1, _lookBackBatchSize, 5];

                        //assign values to array
                        int index = 0;
                        foreach (var item in inputData)
                        {
                            inputDataArray[0, index, 0] = item.high;
                            inputDataArray[0, index, 1] = item.low;
                            inputDataArray[0, index, 2] = item.open;
                            inputDataArray[0, index, 3] = item.weightedAverage;
                            inputDataArray[0, index, 4] = item.close;
                            index++;
                        }

                        //predict and get last column, i.e. the close price
                        var closePrediction = _model.Predict(new NDarray(inputDataArray)).GetData<double>().Last();
                        //calculate the unix time associated with prediction
                        long predictionUnixTime = localLookBack.Last().date + (int)_timeInterval;
                        var closePredictionModel = new PredictedClose(closePrediction, predictionUnixTime);
                        //post to out buffer
                        _bufferOut.AddData(closePredictionModel);
                        //dequeue oldest look back data point. Keep in mind look back data are processed with sliding window approach
                        lookBackQueue.Dequeue();

                        //write to file
                        await _persistence.WriteToDirectoryAsync(_outputDir, new List<PredictedClose>() { closePredictionModel }, stoppingToken);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run OHLC data prediction. Error occurred during prediction worker loop. ");
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
                _bufferIn.Dispose();
                _bufferOut.Dispose();
            }

            _disposed = true;
        }
    }
}
