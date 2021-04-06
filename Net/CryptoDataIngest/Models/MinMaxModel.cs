using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class MinMaxModel
    {
        [JsonConstructor]
        private MinMaxModel() { }

        public MinMaxModel(
            OhlcRecordBase min,
            OhlcRecordBase max)
        {
            Minimum = min;
            Maximum = max;
        }

        [JsonProperty]
        public OhlcRecordBase Minimum { get; private set; }
        [JsonProperty]
        public OhlcRecordBase Maximum { get; private set; }

    }
}
