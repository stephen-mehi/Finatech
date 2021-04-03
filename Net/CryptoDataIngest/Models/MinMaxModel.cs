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
            double min,
            double max)
        {
            Minimum = min;
            Maximum = max;
        }

        [JsonProperty]
        public double Minimum { get; private set; }
        [JsonProperty]
        public double Maximum { get; private set; }

    }
}
