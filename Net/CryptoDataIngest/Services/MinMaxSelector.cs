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

        private double _maxOpen;
        private double _maxHigh;
        private double _maxLow;
        private double _maxClose;
        private double _maxWeightedAvg;
        private double _maxVol;
        private double _maxQuoteVol;

        private double _minOpen;
        private double _minHigh;
        private double _minLow;
        private double _minClose;
        private double _minWeightedAvg;
        private double _minVol;
        private double _minQuoteVol;

        public MinMaxSelector()
        {
            _maxOpen = _maxHigh = _maxLow = _maxClose = _maxWeightedAvg = _maxVol = _maxQuoteVol = double.MinValue;
            _minOpen = _minHigh = _minLow = _minClose = _minWeightedAvg = _minVol = _minQuoteVol = double.MaxValue;
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
