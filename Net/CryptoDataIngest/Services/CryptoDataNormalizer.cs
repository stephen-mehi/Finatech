using CryptoDataIngest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface ICryptoDataNormalizer
    {
        IEnumerable<NormalizedOhlcRecord> Normalize(IReadOnlyList<OhlcRecordBase> data);
    }

    internal class CryptoDataNormalizer : ICryptoDataNormalizer
    {

        private static IReadOnlyList<double> NormalizeInternal(IEnumerable<double> input)
        {
            double min = input.Min();
            double max = input.Max();
            double rangeDiff = max - min;

            var output = input.Select(x => (x - min) / (rangeDiff)).ToList();

            return output;
        }

        /// <summary>
        /// unit variance normalization according to y = (x-MIN)/MAX-MIN
        /// </summary>
        public IEnumerable<NormalizedOhlcRecord> Normalize(IReadOnlyList<OhlcRecordBase> data)
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
