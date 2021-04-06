using CryptoDataIngest.Services;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class OhlcRecordBase
    {
        [JsonConstructor]
        private OhlcRecordBase() { }

        public OhlcRecordBase(
            long date,
            double open,
            double high,
            double low,
            double close,
            double weightedAverage,
            double volume,
            double quoteVolume)
        {
            this.date = date;
            this.open = open;
            this.high = high;
            this.low = low;
            this.close = close;
            this.weightedAverage = weightedAverage;
            this.volume = volume;
            this.quoteVolume = quoteVolume;
        }

        [JsonProperty]
        public long date { get; private set; }
        [JsonProperty]
        public double open { get; private set; }
        [JsonProperty]
        public double high { get; private set; }
        [JsonProperty]
        public double low { get; private set; }
        [JsonProperty]
        public double close { get; private set; }
        [JsonProperty]
        public double weightedAverage { get; private set; }
        [JsonProperty]
        public double volume { get; private set; }
        [JsonProperty]
        public double quoteVolume { get; private set; }
    }

    internal class NormalizedOhlcRecord : OhlcRecordBase
    {
        public NormalizedOhlcRecord(
            long date,
            double open,
            double high,
            double low, 
            double close, 
            double weightedAverage,
            double volume,
            double quoteVolume) 
            : base(date, open, high, low, close, weightedAverage, volume, quoteVolume)
        {
        }
    }

    internal class PredictedClose
    {
        [JsonConstructor]
        private PredictedClose() { }

        public PredictedClose(
            double close,
            long unixTime)
        {
            Close = close;
            UnixTime = unixTime;
        }

        [JsonProperty]
        public double Close { get; private set; }
        [JsonProperty]
        public long UnixTime { get; private set; }
    }

}

