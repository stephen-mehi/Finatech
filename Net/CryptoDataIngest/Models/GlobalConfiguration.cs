using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class GlobalConfiguration
    {
        public GlobalConfiguration()
        {
            RootDataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ETH");
            ProcessedDataDirectory = Path.Combine(RootDataDirectory, "NormalizedData");
            RawDataPathDirectory = Path.Combine(RootDataDirectory, "Data");
            TrainingDataPath = Path.Combine(RootDataDirectory, "TrainingData", "training.csv");
            PredictionDataDirectory = Path.Combine(RootDataDirectory, "Predictions");
            MinMaxDataPath = Path.Combine(RootDataDirectory, "MinMaxData", "minmax.json");
        }


        public long FileExpiration { get; } = 60 * 60 * 24 * 5;
        public int LookBackBatchSize { get; } = 6;
        public TimeIntervalEnum TimeInterval { get; } = TimeIntervalEnum.fiveMinute;
        public string RootDataDirectory { get; }
        public string RawDataPathDirectory { get; }
        public string ProcessedDataDirectory { get; }
        public string PredictionDataDirectory { get; }
        public string MinMaxDataPath { get; }
        public string TrainingDataPath { get; }
    }
}
