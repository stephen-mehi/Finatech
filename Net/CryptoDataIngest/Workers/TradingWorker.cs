using CoinbasePro;
using CryptoDataIngest.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Workers
{
    internal class TradingWorker : BackgroundService
    {
        private readonly ILogger<TradingWorker> _logger;
        private readonly IDataBufferReader<PredictedClose> _bufferIn;
        private ITradingClient _tradingClient;

        public TradingWorker(
            ILogger<TradingWorker> logger,
            IDataBufferReader<PredictedClose> bufferIn)
        {
            _logger = logger;
            _bufferIn = bufferIn;
            //_tradingClient = tradingClient;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var prediction in _bufferIn.GetDataAsync(stoppingToken))
            {
                //if()
            }
        }
    }
}
