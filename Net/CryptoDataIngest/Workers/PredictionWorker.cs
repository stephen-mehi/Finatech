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
        private readonly BlockingCollection<NormalizedOhlcRecord> _bufferIn;
        private readonly BlockingCollection<PredictedClose> _bufferOut;
        private readonly BaseModel _model;
        private const int _batchSize = 6;

        public PredictionWorker(
            ILogger<PredictionWorker> logger,
            IDataBuffer<NormalizedOhlcRecord> bufferIn,
            IDataBuffer<PredictedClose> bufferOut)
        {
            _bufferIn = bufferIn.GetDataBuffer();
            _bufferOut = bufferOut.GetDataBuffer();
            _logger = logger;
            _model = BaseModel.ModelFromJson(File.ReadAllText(@"C:\ProgramData\ETH\Model\model.json"));
            _model.LoadWeight(@"C:\ProgramData\ETH\Model\model.h5");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var lookBackQueue = new Queue<NormalizedOhlcRecord>();

                //continue reading batches of data from api until stopped
                while (!_bufferIn.IsCompleted && !stoppingToken.IsCancellationRequested)
                {
                    NormalizedOhlcRecord data = default;
                    bool foundData = default;

                    try
                    {
                        foundData = _bufferIn.TryTake(out data);
                    }
                    //ignore taking an item if the collection is 'complete' since will just exit on next loop
                    catch (InvalidOperationException) { }

                    //operate on data if found
                    if (!foundData)
                        await Task.Delay(250, stoppingToken);
                    else
                    {
                        //add to batch
                        lookBackQueue.Enqueue(data);

                        //process if queue reaches batch size
                        if (lookBackQueue.Count == _batchSize)
                        {
                            var dataInput = 
                                new NDarray(new[] { lookBackQueue.DequeueAll().Select(x => new[] { x.high, x.low, x.open, x.weightedAverage }).ToArray() });

                           var predictions = _model.Predict(dataInput);

                        }
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"Failed to run OHLC data prediction. Error occurred during prediction worker loop. ");
            }
            finally
            {
                _bufferOut.CompleteAdding();
            }
        }
    }
}
