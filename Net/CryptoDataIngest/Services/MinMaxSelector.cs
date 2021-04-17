using CryptoDataIngest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface IMinMaxSelectorProvider
    {
        IMinMaxSelector Get();
    }

    internal class MinMaxSelectorProvider : IMinMaxSelectorProvider
    {
        public IMinMaxSelector Get()
        {
            return new MinMaxSelector();
        }
    }

    internal interface IMinMaxSelector
    {
        void Assess(OhlcRecordBase record);
        MinMaxModel GetCurrentMinMax();
    }

    internal class MinMaxSelector : IMinMaxSelector
    {
        private readonly object _locker = new ();

        private float _maxOpen;
        private float _maxHigh;
        private float _maxLow;
        private float _maxClose;
        private float _maxWeightedAvg;
        private float _maxVol;
        private float _maxQuoteVol;

        private float _minOpen;
        private float _minHigh;
        private float _minLow;
        private float _minClose;
        private float _minWeightedAvg;
        private float _minVol;
        private float _minQuoteVol;

        public MinMaxSelector()
        {
            _maxOpen = _maxHigh = _maxLow = _maxClose = _maxWeightedAvg = _maxVol = _maxQuoteVol = float.MinValue;
            _minOpen = _minHigh = _minLow = _minClose = _minWeightedAvg = _minVol = _minQuoteVol = float.MaxValue;
        }

        public void Assess(OhlcRecordBase dataPoint)
        {
            lock (_locker)
            {
                //set mins and maxes
                if (dataPoint.open > _maxOpen)
                    _maxOpen = dataPoint.open;
                if (dataPoint.open < _minOpen)
                    _minOpen = dataPoint.open;
                if (dataPoint.high > _maxHigh)
                    _maxHigh = dataPoint.high;
                if (dataPoint.high < _minHigh)
                    _minHigh = dataPoint.high;
                if (dataPoint.low > _maxLow)
                    _maxLow = dataPoint.low;
                if (dataPoint.low < _minLow)
                    _minLow = dataPoint.low;
                if (dataPoint.close > _maxClose)
                    _maxClose = dataPoint.close;
                if (dataPoint.close < _minClose)
                    _minClose = dataPoint.close;
                if (dataPoint.weightedAverage > _maxWeightedAvg)
                    _maxWeightedAvg = dataPoint.weightedAverage;
                if (dataPoint.weightedAverage < _minWeightedAvg)
                    _minWeightedAvg = dataPoint.weightedAverage;
                if (dataPoint.volume > _maxVol)
                    _maxVol = dataPoint.volume;
                if (dataPoint.volume < _minVol)
                    _minVol = dataPoint.volume;
                if (dataPoint.quoteVolume > _maxQuoteVol)
                    _maxQuoteVol = dataPoint.quoteVolume;
                if (dataPoint.quoteVolume < _minQuoteVol)
                    _minQuoteVol = dataPoint.quoteVolume;
            }
        }

        public MinMaxModel GetCurrentMinMax()
        {
            var max = new OhlcRecordBase(default, _maxOpen, _maxHigh, _maxLow, _maxClose, _maxWeightedAvg, _maxVol, _maxQuoteVol);
            var min = new OhlcRecordBase(default, _minOpen, _minHigh, _minLow, _minClose, _minWeightedAvg, _minVol, _minQuoteVol);
            return new MinMaxModel(min, max);
        }
    }
}
