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

        public IReadOnlyList<(DateTime start, DateTime end)> TrainingDataTimePeriods { get; } =
            new List<(DateTime start, DateTime end)>()
            {
                (new DateTime(2018, 5, 29), new DateTime(2018, 12, 31)),
                (new DateTime(2019, 1, 12), new DateTime(2019, 4, 14)),
                (new DateTime(2019, 5, 31), new DateTime(2019, 9, 23)),
                (new DateTime(2019, 9, 28), new DateTime(2019, 12, 23)),
                (new DateTime(2020, 1, 23), new DateTime(2020, 3, 1)),
                (new DateTime(2020, 3, 9), new DateTime(2020, 4, 16)),
                (new DateTime(2020, 5, 12), new DateTime(2020, 10, 26)),
                (new DateTime(2020, 11, 9), new DateTime(2021, 4, 7)),
            };

        public long FileExpiration { get; } = 60 * 60 * 24 * 5;
        public int LookBackBatchSize { get; } = 20;
        public TimeIntervalEnum TimeInterval { get; } = TimeIntervalEnum.fiveMinute;
        public string RootDataDirectory { get; }
        public string RawDataPathDirectory { get; }
        public string ProcessedDataDirectory { get; }
        public string PredictionDataDirectory { get; }
        public string MinMaxDataPath { get; }
        public string TrainingDataPath { get; }
    }
}
