using CoinbasePro.Network.HttpClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal interface ICoinbaseProHttpClient : IHttpClient, IDisposable
    {

    }

    class CoinbaseProHttpClient : ICoinbaseProHttpClient
    {
        private bool _disposed;
        private System.Net.Http.HttpClient _client;

        public CoinbaseProHttpClient()
        {
            _client = new System.Net.Http.HttpClient();
        }

        public Task<string> ReadAsStringAsync(HttpResponseMessage httpRequestMessage)
        {
            return httpRequestMessage.Content.ReadAsStringAsync();
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage, CancellationToken cancellationToken)
        {
            return _client.SendAsync(httpRequestMessage, cancellationToken);
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage httpRequestMessage)
        {
            return _client.SendAsync(httpRequestMessage);
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
                _client.Dispose();
            }

            _disposed = true;
        }
    }
}
