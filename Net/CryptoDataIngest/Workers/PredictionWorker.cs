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
using Python.Runtime;

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
        private readonly ICryptoDataNormalizer _normalizer;
        private readonly string _outputDir;
        private bool _disposed;

        public PredictionWorker(
            ILogger<PredictionWorker> logger,
            IDataBufferReader<NormalizedOhlcRecord> bufferIn,
            IDataBufferWriter<PredictedClose> bufferOut,
            GlobalConfiguration config,
            IDataPersistence persistence,
            ICryptoDataNormalizer normalizer)
        {
            _normalizer = normalizer;
            _persistence = persistence;
            _timeInterval = config.TimeInterval;
            _lookBackBatchSize = config.LookBackBatchSize;
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _logger = logger;
            using (Py.GIL())
            {
                _model = BaseModel.ModelFromJson(File.ReadAllText(@"C:\ProgramData\ETH\Model\model.json"));
                _model.LoadWeight(@"C:\ProgramData\ETH\Model\model.h5");
            }
            _outputDir = config.PredictionDataDirectory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {

            var lookBackQueue = new Queue<NormalizedOhlcRecord>();

            await foreach (var dataPoint in _bufferIn.GetDataAsync(stoppingToken))
            {
                try
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    //add to batch
                    lookBackQueue.Enqueue(dataPoint);

                    //process if queue reaches batch size
                    if (lookBackQueue.Count == _lookBackBatchSize)
                    {
                        //copy lookback timesteps
                        var localLookBack = lookBackQueue.DequeueMany(_lookBackBatchSize).ToList();

                        var inputData = localLookBack.Select(x => new { x.high, x.low, x.open, x.weightedAverage, x.close });
                        //init 3d array 
                        var inputDataArray = new double[1, _lookBackBatchSize, 5];

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
                        using (Py.GIL())
                        {
                            var predictions = _model.Predict(new NDarray(inputDataArray));
                            var predictionData = predictions.GetData<float>();
                            var closePrediction = predictionData[4];
                            //calculate the unix time associated with prediction
                            long predictionUnixTime = localLookBack.Last().date + (int)_timeInterval;

                            var denormalizedClose = _normalizer.DenormalizeClose(new List<double>() { closePrediction }).Single();

                            var closePredictionModel = new PredictedClose(denormalizedClose, predictionUnixTime);

                            //post to out buffer
                            _bufferOut.AddData(closePredictionModel, stoppingToken);

                            //write to file
                            await _persistence.WriteToDirectoryAsync(_outputDir, new List<PredictedClose>() { closePredictionModel }, stoppingToken);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, $"Failed to run OHLC data prediction. Error occurred during prediction worker loop. ");
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
                _bufferIn.Dispose();
                _bufferOut.Dispose();
                _model.Dispose();
            }

            _disposed = true;
        }
    }
}
