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
            double[,,] inputs,
            double[,,] outputs)
        {
            Inputs = inputs;
            Outputs = outputs;
        }

        public double[,,] Inputs { get; }
        public double[,,] Outputs { get; }
    }

    internal interface IDataConvolve
    {
        LstmInputOutput Convolve(IEnumerable<OhlcRecordBase> data, int lookBack, int lookForward);
    }

    internal class DataConvolve : IDataConvolve
    {
        public LstmInputOutput Convolve(IEnumerable<OhlcRecordBase> data, int lookBack, int lookForward)
        {
            var localData = data.ToList();

            int nConvolutions = localData.Count - lookBack - 1;
            //init 3d array 
            var inputDataArray = new double[nConvolutions, lookBack, 5];
            var outputDataArray = new double[nConvolutions, 1, 5];

            for (int i = 0; i < nConvolutions; i++)
            {
                int inputEndIndex = i + lookBack - 1;
                int outputStartIndex = inputEndIndex + 1;
                int outputEndIndex = outputStartIndex + lookForward;

                //populate input
                for (int j = 0; j < lookBack; j++)
                {
                    var currentItem = localData[i + j];

                    inputDataArray[i, j, 0] = currentItem.high;
                    inputDataArray[i, j, 1] = currentItem.low;
                    inputDataArray[i, j, 2] = currentItem.open;
                    inputDataArray[i, j, 3] = currentItem.weightedAverage;
                    inputDataArray[i, j, 4] = currentItem.close;
                }


                for (int k = 0; k < outputEndIndex - outputStartIndex; k++)
                {
                    outputDataArray[i, k, 0] = localData[outputStartIndex + k].high;
                    outputDataArray[i, k, 1] = localData[outputStartIndex + k].low;
                    outputDataArray[i, k, 2] = localData[outputStartIndex + k].open;
                    outputDataArray[i, k, 3] = localData[outputStartIndex + k].weightedAverage;
                    outputDataArray[i, k, 4] = localData[outputStartIndex + k].close;
                }
            }

            return new LstmInputOutput(inputDataArray, outputDataArray);
        }
    }
}
