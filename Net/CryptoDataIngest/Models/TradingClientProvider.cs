using CoinbasePro;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal interface ITradingClientProvider
    {
        ITradingClient GetClient(bool isSandbox);
    }

    internal class TradingClientProvider : ITradingClientProvider
    {
        public ITradingClient GetClient(bool isSandbox)
        {
            var client = new TradingClient("api key", "api secret", "password", new CoinbaseProHttpClient(), true);
            return client;
        }
    }
}
