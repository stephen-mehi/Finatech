using CryptoDataIngest.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface ICryptoDataNormalizer
    {
        IEnumerable<NormalizedOhlcRecord> NormalizeAsync(IReadOnlyList<OhlcRecordBase> data, CancellationToken ct = default);
    }

    internal class CryptoDataNormalizer : ICryptoDataNormalizer
    {

        private static IReadOnlyList<double> NormalizeInternal(IEnumerable<double> input)
        {
            var local = input.ToList();
            double min = local.Min();
            double max = local.Max();
            double rangeDiff = max - min;
            var output = input.Select(x => (x - min) / (rangeDiff)).ToList();

            return output;
        }

        /// <summary>
        /// unit variance normalization according to y = (x-MIN)/MAX-MIN
        /// </summary>
        public IEnumerable<NormalizedOhlcRecord> NormalizeAsync(IReadOnlyList<OhlcRecordBase> data, CancellationToken ct = default)
        {
            var opens = NormalizeInternal(data.Select(x => x.open));
            var highs = NormalizeInternal(data.Select(x => x.high));
            var lows = NormalizeInternal(data.Select(x => x.low));
            var closes = NormalizeInternal(data.Select(x => x.close));
            var volWeightedAvgs = NormalizeInternal(data.Select(x => x.weightedAverage));
            var volumes = NormalizeInternal(data.Select(x => x.volume));
            var quoteVol = NormalizeInternal(data.Select(x => x.quoteVolume));

            for (int i = 0; i < data.Count; i++)
            {
                var o = data[i];
                var output =
                    new NormalizedOhlcRecord(
                        o.date,
                        opens[i],
                        highs[i],
                        lows[i],
                        closes[i],
                        volWeightedAvgs[i],
                        volumes[i],
                        quoteVol[i]);

                yield return output;
            }
        }
    }
}
