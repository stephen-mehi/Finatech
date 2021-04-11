using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class HyperParameters
    {
        public HyperParameters(
            int neurons,
            string activationFunc,
            string lossFunc,
            string optimizer,
            double dropout,
            int batchSize,
            int epochs,
            int lookback,
            int lookForward,
            int randomSeed)
        {
            Neurons = neurons;
            ActivationFunction = activationFunc;
            LossFunction = lossFunc;
            Optimizer = optimizer;
            Dropout = dropout;
            BatchSize = batchSize;
            Epochs = epochs;
            LookBack = lookback;
            LookForward = lookForward;
            RandomSeed = randomSeed;
        }

        public int Neurons { get;  }
        public string ActivationFunction { get;  }
        public string LossFunction { get;  }
        public string Optimizer { get; }
        public double Dropout { get; }
        public int BatchSize { get; }
        public int Epochs { get; }
        public int LookBack { get; }
        public int LookForward { get; }
        public int RandomSeed { get; }
    }
}
