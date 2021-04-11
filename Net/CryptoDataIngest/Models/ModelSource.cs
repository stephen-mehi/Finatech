using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class ModelSource
    {
        public ModelSource(string modelDir)
        {
            ModelDirectory = modelDir;
        }

        public string ModelDirectory { get; }
    }
}
