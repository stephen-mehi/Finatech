using CryptoDataIngest.Models;
using Numpy;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal class LstmInputOutput
    {
        public LstmInputOutput(
            NDarray inputs,
            NDarray outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }

        public NDarray Inputs { get; }
        public NDarray Outputs { get; }
    }

    internal interface IDataConvolve
    {
        LstmInputOutput Convolve(IEnumerable<SourceOhlcRecordBase> data, int lookBack, int lookForward);
    }

    internal class DataConvolve : IDataConvolve
    {
        public LstmInputOutput Convolve(IEnumerable<SourceOhlcRecordBase> data, int lookBack, int lookForward)
        {
            var localData = data.ToList();

            //init 3d array 
            var inputDataArray = new double[1, lookBack, 5];
            var outputDataArray = new double[1, lookBack, 1];

            for (int i = 0; i < localData.Count - lookBack - 1; i++)
            {
                int inputEndIndex = i + lookBack - 1;
                int outputStartIndex = inputEndIndex + 1;
                int outputEndIndex = outputStartIndex + lookForward - 1;

                for (int j = i; j < inputEndIndex; j++)
                {
                    var currentItem = localData[j];

                    inputDataArray[i, j, 0] = currentItem.high;
                    inputDataArray[i, j, 1] = currentItem.low;
                    inputDataArray[i, j, 2] = currentItem.open;
                    inputDataArray[i, j, 3] = currentItem.weightedAverage;
                    inputDataArray[i, j, 4] = currentItem.close;
                }

                for (int k = outputStartIndex; k < outputEndIndex; k++)
                    outputDataArray[i, k, 0] = localData[k].close;
            }

            return new LstmInputOutput(new NDarray(inputDataArray), new NDarray(outputDataArray));
        }
    }
}
