using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Keras;
using Keras.Layers;
using Keras.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Numpy;
using Python.Runtime;

namespace CryptoDataIngest.Workers
{
    internal class ModelTrainerWorker : BackgroundService
    {

        private readonly IDataBufferReader<OhlcRecordBaseBatch> _bufferIn;
        private readonly IDataBufferWriter<ModelSource> _bufferOut;
        private readonly ILogger _logger;
        private readonly IMinMaxSelectorProvider _minMaxSelectorProv;
        private readonly IMinMaxScalerProvider _scalerProv;
        private readonly IDataConvolve _convolver;
        private readonly GlobalConfiguration _config;
        private bool _disposed;

        public ModelTrainerWorker(
            ILogger<ModelTrainerWorker> logger,
            IMinMaxSelectorProvider minMaxSelectorProv,
            IMinMaxScalerProvider scalerProv,
            IDataBufferReader<OhlcRecordBaseBatch> bufferIn,
            IDataBufferWriter<ModelSource> bufferOut,
            IDataConvolve convolver,
            GlobalConfiguration config)
        {
            _bufferOut = bufferOut;
            _scalerProv = scalerProv;
            _logger = logger;
            _bufferIn = bufferIn;
            _minMaxSelectorProv = minMaxSelectorProv;
            _config = config;
            _convolver = convolver;
        }

        protected override async Task ExecuteAsync(CancellationToken ct = default)
        {
            Directory.CreateDirectory(_config.ModelDirectory);

            //pull all data in
            await foreach (var dataBatch in _bufferIn.GetDataAsync(ct))
            {
                try
                {
                    var minMaxSelector = _minMaxSelectorProv.Get();
                    var localData = new List<OhlcRecordBase>();

                    foreach (var item in dataBatch.Data)
                    {
                        localData.Add(item);
                        minMaxSelector.Assess(item);
                    }

                    //get scaler
                    var minMaxModel = minMaxSelector.GetCurrentMinMax();
                    var scaler = _scalerProv.Get(minMaxModel);

                    var scaledData = scaler.Scale(localData);
                    var hParams = _config.HyperParams;

                    var inputOutput = _convolver.Convolve(scaledData, hParams.LookBack, hParams.LookForward);
                    string outputDir = string.Empty;

                    using (Py.GIL())
                    {
                        Keras.
                        //prepare inputs and outputs
                        var inputs = new NDarray(inputOutput.Inputs);
                        var outputs = new NDarray(inputOutput.Outputs);

                        var model = new Sequential();
                        var lstm =
                            new LSTM(
                                units: hParams.Neurons,
                                return_sequences: false,
                                input_shape: new Shape(new int[] { inputs.shape[1], inputs.shape[2] }),
                                activation: hParams.ActivationFunction);

                        model.Add(lstm);

                        model.Add(new Dropout(hParams.Dropout));
                        model.Add(new Dense(inputs.shape[2]));
                        model.Add(new Activation(hParams.ActivationFunction));
                        model.Compile(loss: hParams.LossFunction, optimizer: hParams.Optimizer, metrics: new string[] { "mae" });

                        var history =
                            model.Fit(
                                x: inputs,
                                y: outputs,
                                epochs: hParams.Epochs,
                                batch_size: hParams.BatchSize,
                                verbose: 1,
                                shuffle: false);

                        //create timestamped directory
                        var dt = DateTime.Now;
                        string ts = $"{dt.Month}_{dt.Day}_{dt.Year}_{dt.Hour}_{dt.Minute}_{dt.Second}";
                        outputDir = Path.Combine(_config.ModelDirectory, ts);
                        Directory.CreateDirectory(outputDir);

                        //output history, weights, and model
                        await File.WriteAllTextAsync(Path.Combine(outputDir, "history.json"), JsonConvert.SerializeObject(history.HistoryLogs.Values), ct);
                        await File.WriteAllTextAsync(Path.Combine(outputDir, "model.json"), model.ToJson(), ct);
                        model.SaveWeight(Path.Combine(outputDir, "weights.h5"));
                    }

                    //write new model info to out buffer
                    _bufferOut.AddData(new ModelSource(outputDir), ct);
                }
                catch (Exception e)
                {
                    _logger.LogError($"Failed while attempting to train new model. Message: {e.Message}. Stack: {e.StackTrace}");
                }
                finally
                {
                    await Task.Delay(5000, ct);
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
