using CryptoDataIngest.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface ICryptoDataClient
    {
        Task<IReadOnlyList<SourceOhlcRecordBase>> GetDataAsync(
            string cryptoSymbol,
            string currencySymbol,
            TimeIntervalEnum interval,
            long start,
            long end,
            CancellationToken ct = default);
    }

    internal class CryptoDataClient : ICryptoDataClient
    {
        public async Task<IReadOnlyList<SourceOhlcRecordBase>> GetDataAsync(
            string cryptoSymbol,
            string currencySymbol,
            TimeIntervalEnum interval,
            long start,
            long end,
            CancellationToken ct = default)
        {
            string url = $"https://poloniex.com/public?command=returnChartData&currencyPair={currencySymbol}_{cryptoSymbol}&start={start}&end={end}&period={(int)interval}";

            using var client = new HttpClient();

            var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            resp.EnsureSuccessStatusCode();

            var data = await resp.Content.ReadAsStreamAsync(ct);

            using var sr = new StreamReader(data);
            using var jr = new JsonTextReader(sr);
            var jd = new JsonSerializer();

            var output = jd.Deserialize<List<SourceOhlcRecordBase>>(jr);

            return output;
        }
    }
}
