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
        IEnumerable<NormalizedOhlcRecord> Normalize(IReadOnlyList<OhlcRecordBase> data);
        IEnumerable<double> DenormalizeClose(IReadOnlyList<double> data);
    }

    internal class CryptoDataNormalizer : ICryptoDataNormalizer
    {
        private readonly OhlcRecordBase _min;
        private readonly OhlcRecordBase _max;

        public CryptoDataNormalizer(
            MinMaxModel minMaxModel)
        {
            _min = minMaxModel.Minimum;
            _max = minMaxModel.Maximum;
        }

        private static IReadOnlyList<double> NormalizeInternal(IEnumerable<double> input, double min, double max)
        {
            double rangeDiff = max - min;
            var output = input.Select(x => (x - min) / (rangeDiff)).ToList();
            return output;
        }

        private static IReadOnlyList<double> DenormalizeInternal(IEnumerable<double> input, double min, double max)
        {
            double rangeDiff = max - min;
            var output = input.Select(x => (x * rangeDiff) + min).ToList();
            return output;
        }

        public IEnumerable<double> DenormalizeClose(IReadOnlyList<double> closeData)
        {
            var closes = DenormalizeInternal(closeData, _min.close, _max.close);
            return closes;
        }

        /// <summary>
        /// unit variance normalization according to y = (x-MIN)/MAX-MIN
        /// </summary>
        public IEnumerable<NormalizedOhlcRecord> Normalize(IReadOnlyList<OhlcRecordBase> data)
        {
            var opens = NormalizeInternal(data.Select(x => x.open), _min.open, _max.open);
            var highs = NormalizeInternal(data.Select(x => x.high), _min.high, _max.high);
            var lows = NormalizeInternal(data.Select(x => x.low), _min.low, _max.low);
            var closes = NormalizeInternal(data.Select(x => x.close), _min.close, _max.close);
            var volWeightedAvgs = NormalizeInternal(data.Select(x => x.weightedAverage), _min.weightedAverage, _max.weightedAverage);
            var volumes = NormalizeInternal(data.Select(x => x.volume), _min.volume, _max.volume);
            var quoteVol = NormalizeInternal(data.Select(x => x.quoteVolume), _min.quoteVolume, _max.quoteVolume);

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
