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
            string phrase = Environment.GetEnvironmentVariable("CBP_PHRASE", EnvironmentVariableTarget.Machine);
            string secret = Environment.GetEnvironmentVariable("CBP_SECRET", EnvironmentVariableTarget.Machine);
            string key = Environment.GetEnvironmentVariable("CBP_KEY", EnvironmentVariableTarget.Machine);

            var client = new TradingClient(key, secret, phrase, new CoinbaseProHttpClient(), true);
            return client;
        }
    }
}
