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
        IEnumerable<double> DeScaleClose(IReadOnlyList<double> data);
    }

    internal class MinMaxScaler : IMinMaxScaler
    {
        private readonly OhlcRecordBase _min;
        private readonly OhlcRecordBase _max;

        public MinMaxScaler(
            MinMaxModel minMaxModel)
        {
            _min = minMaxModel.Minimum;

            double percentAddition = 0.05;

            //set max as 10% higher than actual max since having toruble predicting around max
            double maxOpen = minMaxModel.Maximum.open + minMaxModel.Maximum.open * percentAddition;
            double maxHigh = minMaxModel.Maximum.high + minMaxModel.Maximum.high * percentAddition;
            double maxLow = minMaxModel.Maximum.low + minMaxModel.Maximum.low * percentAddition;
            double maxClose = minMaxModel.Maximum.close + minMaxModel.Maximum.close * percentAddition;
            double maxWeighted = minMaxModel.Maximum.weightedAverage + minMaxModel.Maximum.weightedAverage * percentAddition;
            double maxVol = minMaxModel.Maximum.volume + minMaxModel.Maximum.volume * percentAddition;
            double maxQuoteVol = minMaxModel.Maximum.quoteVolume + minMaxModel.Maximum.quoteVolume * percentAddition;

            _max = new OhlcRecordBase(default, maxOpen, maxHigh, maxLow, maxClose, maxWeighted, maxVol, maxQuoteVol);
        }

        private static IReadOnlyList<double> ScaleInternal(IEnumerable<double> input, double min, double max)
        {
            double rangeDiff = max - min;
            var output = input.Select(x => (x - min) / (rangeDiff)).ToList();
            return output;
        }

        private static IReadOnlyList<double> DeScaleInternal(IEnumerable<double> input, double min, double max)
        {
            double rangeDiff = max - min;
            var output = input.Select(x => (x * rangeDiff) + min).ToList();
            return output;
        }

        public IEnumerable<double> DeScaleClose(IReadOnlyList<double> closeData)
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
