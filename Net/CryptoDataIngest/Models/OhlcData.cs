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
            float open,
            float high,
            float low,
            float close,
            float weightedAverage,
            float volume,
            float quoteVolume)
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
        public float open { get; private set; }
        [JsonProperty]
        public float high { get; private set; }
        [JsonProperty]
        public float low { get; private set; }
        [JsonProperty]
        public float close { get; private set; }
        [JsonProperty]
        public float weightedAverage { get; private set; }
        [JsonProperty]
        public float volume { get; private set; }
        [JsonProperty]
        public float quoteVolume { get; private set; }
    }

    internal class OhlcRecordBaseBatch
    {
        public OhlcRecordBaseBatch(
            IEnumerable<OhlcRecordBase> data,
            TimeIntervalEnum interval)
        {
            Data = data;
            Interval = interval;
        }

        public IEnumerable<OhlcRecordBase> Data { get; }
        public TimeIntervalEnum Interval { get; }
    }

    internal class ScaledOhlcRecord : OhlcRecordBase
    {
        public ScaledOhlcRecord(
            long date,
            float open,
            float high,
            float low,
            float close,
            float weightedAverage,
            float volume,
            float quoteVolume) 
            : base(date, open, high, low, close, weightedAverage, volume, quoteVolume)
        {
        }
    }

    internal class PredictedClose
    {
        [JsonConstructor]
        private PredictedClose() { }

        public PredictedClose(
            float close,
            long unixTime,
            TimeIntervalEnum interval)
        {
            Close = close;
            UnixTime = unixTime;
            TimeInterval = interval.ToString();
        }

        [JsonProperty]
        public float Close { get; private set; }
        [JsonProperty]
        public long UnixTime { get; private set; }
        public string TimeInterval { get; private set; }
    }

}

