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
using Newtonsoft.Json;

namespace CryptoDataIngest.Workers
{
    internal class PredictionWorker : BackgroundService
    {

        private readonly ILogger<PredictionWorker> _logger;
        private readonly IDataBufferReader<ModelSource> _modelBufferIn;
        private readonly IDataBufferReader<(TimeIntervalEnum, ScaledOhlcRecord)> _bufferIn;
        private readonly IDataBufferWriter<PredictedClose> _bufferOut;
        private readonly Dictionary<TimeIntervalEnum, ModelSource> _models = new();
        private readonly int _lookBackBatchSize;
        private readonly IDataPersistence _persistence;
        private readonly IMinMaxScalerProvider _scalerProv;
        private readonly string _outputDir;
        private bool _disposed;
        private readonly GlobalConfiguration _config;
        private Task<ModelSource> _nextModelTask;
        private readonly IReadOnlyDictionary<TimeIntervalEnum, Queue<ScaledOhlcRecord>> _lookBackQueues;

        private readonly ConsoleColor _consoleColor = ConsoleColor.Blue;

        public PredictionWorker(
            ILogger<PredictionWorker> logger,
            IDataBufferReader<(TimeIntervalEnum, ScaledOhlcRecord)> bufferIn,
            IDataBufferReader<ModelSource> modelBufferIn,
            IDataBufferWriter<PredictedClose> bufferOut,
            GlobalConfiguration config,
            IDataPersistence persistence,
            IMinMaxScalerProvider scalerProv)
        {
            _config = config;
            _scalerProv = scalerProv;
            _persistence = persistence;
            _lookBackBatchSize = config.HyperParams.LookBack;
            _modelBufferIn = modelBufferIn;
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _logger = logger;
            _outputDir = config.PredictionDataDirectory;

            var localQueues =
                config
                .TimeIntervals
                .ToDictionary(x => x, x => new Queue<ScaledOhlcRecord>());

            _lookBackQueues = localQueues;
        }

        private void AddOrUpdateModels(TimeIntervalEnum interval, ModelSource model)
        {
            if (_models.ContainsKey(interval))
                _models[interval] = model;
            else
                _models.Add(interval, model);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ColorConsole.WriteLineWithTimestamp($"Waiting for first model info to be available in order to perform predictions..", _consoleColor);

            //initialize model
            await using var modelEnumerator = _modelBufferIn.GetDataAsync(stoppingToken).GetAsyncEnumerator(stoppingToken);
            await modelEnumerator.MoveNextAsync();
            var currentModelData = modelEnumerator.Current;

            ColorConsole.WriteLineWithTimestamp($"Got first available model info. For interval: {currentModelData.Interval}", _consoleColor);

            AddOrUpdateModels(currentModelData.Interval, currentModelData);

            ColorConsole.WriteLineWithTimestamp($"Completed loading model instance and weights for interval: {currentModelData.Interval}", _consoleColor);

            //kick off await for next incoming model
            _nextModelTask = Task.Run(async () => { await modelEnumerator.MoveNextAsync(); return modelEnumerator.Current; });

            await foreach (var dataPoint in _bufferIn.GetDataAsync(stoppingToken))
            {
                try
                {
                    if (stoppingToken.IsCancellationRequested)
                        break;

                    ColorConsole.WriteLineWithTimestamp($"Got datapoint for interval: {dataPoint.Item1}", _consoleColor);

                    //skip loop if received data for time interval we don't have a model for
                    if (_models.ContainsKey(dataPoint.Item1))
                        continue;

                    //check if next model is available
                    if (_nextModelTask.IsCompleted)
                    {
                        var nextModelData = _nextModelTask.Result;

                        ColorConsole.WriteLineWithTimestamp($"Next model info available for interval: {nextModelData.Interval}. Loading instance and weights...", _consoleColor);

                        AddOrUpdateModels(nextModelData.Interval, nextModelData);

                        ColorConsole.WriteLineWithTimestamp($"Completed loading wieghts for interval: {nextModelData.Interval}. Replacing old model with this one", _consoleColor);

                        //start getting next model
                        _nextModelTask = Task.Run(async () => { await modelEnumerator.MoveNextAsync(); return modelEnumerator.Current; });
                    }

                    if (!_lookBackQueues.TryGetValue(dataPoint.Item1, out var lookBackQueue))
                        throw new ArgumentException($"Failed to process data point, time interval not configured: {dataPoint.Item1}");

                    //add to batch
                    lookBackQueue.Enqueue(dataPoint.Item2);

                    //process if queue reaches batch size
                    if (lookBackQueue.Count == _lookBackBatchSize)
                    {
                        //copy lookback timesteps
                        var localLookBack = lookBackQueue.DequeueMany(_lookBackBatchSize).ToList();

                        var inputData = localLookBack.Select(x => new { x.high, x.low, x.open, x.weightedAverage, x.close });
                        //init 3d array 
                        var inputDataArray = new float[1, _lookBackBatchSize, 5];

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

                        var currentInterval = dataPoint.Item1;

                        //get minmax scaler
                        var minMaxData = JsonConvert.DeserializeObject<MinMaxModel>(File.ReadAllText(Path.Combine(_config.MinMaxDataDirectory, $"minmax_{currentInterval}.json")));
                        var scaler = _scalerProv.Get(minMaxData);

                        if (!_models.TryGetValue(currentInterval, out var targetModel))
                            throw new ArgumentException($"Failed to get model for interval: {currentInterval}. No model found for that interval type");

                        float closePrediction;

                        ColorConsole.WriteLineWithTimestamp($"Predicting next timestep for interval: {currentInterval}", _consoleColor);

                        //predict and get last column, i.e. the close price
                        lock(_config.PythonLock)
                        using (Py.GIL())
                        {
                            var model = BaseModel.ModelFromJson(File.ReadAllText(Path.Combine(targetModel.ModelDirectory, "model.json")));
                            model.LoadWeight(Path.Combine(targetModel.ModelDirectory, "weights.h5"));

                            var predictions = model.Predict(new NDarray(inputDataArray));
                            var predictionData = predictions.GetData<float>();
                            closePrediction = predictionData[4];
                        }

                        //calculate the unix time associated with prediction
                        long predictionUnixTime = localLookBack.Last().date + (int)currentInterval;
                        var denormalizedClose = scaler.DeScaleClose(new List<float>() { closePrediction }).Single();
                        var closePredictionModel = new PredictedClose(denormalizedClose, predictionUnixTime, currentInterval);

                        ColorConsole.WriteLineWithTimestamp($"Completed prediction for next timestep for interval: {currentInterval}. Posting to out buffer...", _consoleColor);

                        //post to out buffer
                        _bufferOut.AddData(closePredictionModel, stoppingToken);

                        ColorConsole.WriteLineWithTimestamp($"Completed posting to out buffer for interval: {currentInterval}. Persisting to file...", _consoleColor);

                        //write to file
                        await _persistence.WriteToDirectoryAsync(_outputDir, new List<PredictedClose>() { closePredictionModel }, stoppingToken);

                        ColorConsole.WriteLineWithTimestamp($"Completed persisting to file for interval: {currentInterval}", _consoleColor);
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
            }

            _disposed = true;
        }
    }
}
