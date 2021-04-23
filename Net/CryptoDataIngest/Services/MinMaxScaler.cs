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
    internal interface IMinMaxScalerProvider
    {
        IMinMaxScaler Get(MinMaxModel minMaxData);
    }

    internal class MinMaxScalerProvider : IMinMaxScalerProvider
    {
        public IMinMaxScaler Get(MinMaxModel minMaxData)
        {
            return new MinMaxScaler(minMaxData);
        }
    }

    internal interface IMinMaxScaler
    {
        IEnumerable<ScaledOhlcRecord> Scale(IReadOnlyList<OhlcRecordBase> data);
        IEnumerable<float> DeScaleClose(IReadOnlyList<float> data);
    }

    internal class MinMaxScaler : IMinMaxScaler
    {
        private readonly OhlcRecordBase _min;
        private readonly OhlcRecordBase _max;

        public MinMaxScaler(
            MinMaxModel minMaxModel)
        {
            _min = minMaxModel.Minimum;

            float maxOpen = minMaxModel.Maximum.open + minMaxModel.Maximum.open;
            float maxHigh = minMaxModel.Maximum.high + minMaxModel.Maximum.high;
            float maxLow = minMaxModel.Maximum.low + minMaxModel.Maximum.low;
            float maxClose = minMaxModel.Maximum.close + minMaxModel.Maximum.close;
            float maxWeighted = minMaxModel.Maximum.weightedAverage + minMaxModel.Maximum.weightedAverage;
            float maxVol = minMaxModel.Maximum.volume + minMaxModel.Maximum.volume;
            float maxQuoteVol = minMaxModel.Maximum.quoteVolume + minMaxModel.Maximum.quoteVolume;

            _max = new OhlcRecordBase(default, maxOpen, maxHigh, maxLow, maxClose, maxWeighted, maxVol, maxQuoteVol);
        }

        private static IReadOnlyList<float> ScaleInternal(IEnumerable<float> input, float min, float max)
        {
            float rangeDiff = max - min;
            var output = input.Select(x => (x - min) / (rangeDiff)).ToList();
            return output;
        }

        private static IReadOnlyList<float> DeScaleInternal(IEnumerable<float> input, float min, float max)
        {
            float rangeDiff = max - min;
            var output = input.Select(x => (x * rangeDiff) + min).ToList();
            return output;
        }

        public IEnumerable<float> DeScaleClose(IReadOnlyList<float> closeData)
        {
            var closes = DeScaleInternal(closeData, _min.close, _max.close);
            return closes;
        }

        /// <summary>
        /// unit variance normalization according to y = (x-MIN)/MAX-MIN
        /// </summary>
        public IEnumerable<ScaledOhlcRecord> Scale(IReadOnlyList<OhlcRecordBase> data)
        {
            var opens = ScaleInternal(data.Select(x => x.open), _min.open, _max.open);
            var highs = ScaleInternal(data.Select(x => x.high), _min.high, _max.high);
            var lows = ScaleInternal(data.Select(x => x.low), _min.low, _max.low);
            var closes = ScaleInternal(data.Select(x => x.close), _min.close, _max.close);
            var volWeightedAvgs = ScaleInternal(data.Select(x => x.weightedAverage), _min.weightedAverage, _max.weightedAverage);
            var volumes = ScaleInternal(data.Select(x => x.volume), _min.volume, _max.volume);
            var quoteVol = ScaleInternal(data.Select(x => x.quoteVolume), _min.quoteVolume, _max.quoteVolume);

            for (int i = 0; i < data.Count; i++)
            {
                var o = data[i];
                var output =
                    new ScaledOhlcRecord(
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
