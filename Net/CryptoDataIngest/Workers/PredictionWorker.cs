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
        private const int _batchSize = 6;
        private bool _disposed;

        public PredictionWorker(
            ILogger<PredictionWorker> logger,
            IDataBufferReader<NormalizedOhlcRecord> bufferIn,
            IDataBufferWriter<PredictedClose> bufferOut)
        {
            _bufferIn = bufferIn;
            _bufferOut = bufferOut;
            _logger = logger;
            _model = BaseModel.ModelFromJson(File.ReadAllText(@"C:\ProgramData\ETH\Model\model.json"));
            _model.LoadWeight(@"C:\ProgramData\ETH\Model\model.h5");
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
                    if (lookBackQueue.Count == _batchSize)
                    {
                        var dataInput = new NDarray(new[, ,] { { { lookBackQueue.ToList().Select(x => new[] { x.high, x.low, x.open, x.weightedAverage, x.close }).ToArray() } } });
                        var predictions = _model.Predict(dataInput).GetData<double>();

                        //ACT ON DATA AND PUSH INTO OUT BUFFER

                        lookBackQueue.Dequeue();
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
