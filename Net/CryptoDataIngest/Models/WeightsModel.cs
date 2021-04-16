using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class WeightsModel
    {
        public WeightsModel(
            int[] shape,
            IEnumerable<float> values)
        {
            Shape = shape;
            Values = values.ToArray();
        }

        public int[] Shape { get; }
        public float[] Values { get; }
    }
}
