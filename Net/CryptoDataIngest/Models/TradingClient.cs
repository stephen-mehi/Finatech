using CoinbasePro;
using CoinbasePro.Network.Authentication;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal interface ITradingClient : IDisposable
    {
        Task BuyAsync(double dollarAmount, CancellationToken ct = default);
        Task SellAsync(double dollarAmount, CancellationToken ct = default);
    }

    internal class TradingClient : ITradingClient
    {
        private bool _disposed;
        private CoinbaseProClient _client;
        private ICoinbaseProHttpClient _httpClient;

        public TradingClient(
            string apiKey, 
            string secret, 
            string passphrase,
            ICoinbaseProHttpClient httpClient, 
            bool sandbox)
        {
            var authenticator = new Authenticator(apiKey, secret, passphrase);
            _client = new CoinbaseProClient(authenticator, httpClient, sandbox);
        }

        public Task BuyAsync(double dollarAmount, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task SellAsync(double dollarAmount, CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
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
                _httpClient.Dispose();
            }

            _disposed = true;
        }
    }
}
