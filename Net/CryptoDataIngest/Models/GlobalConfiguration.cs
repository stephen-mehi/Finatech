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
            TrainingDataDirectory = Path.Combine(RootDataDirectory, "TrainingData");
            PredictionDataDirectory = Path.Combine(RootDataDirectory, "Predictions");
            MinMaxDataDirectory = Path.Combine(RootDataDirectory, "MinMaxData");
            ModelDirectory = Path.Combine(RootDataDirectory, "Model");

            HyperParams =
                new HyperParameters(
                    neurons: 125,
                    activationFunc: "tanh",
                    lossFunc: "mse",
                    optimizer: "adam",
                    dropout: 0.2,
                    batchSize: 32,
                    epochs: 1,
                    lookback: 6,
                    lookForward: 1,
                    randomSeed: 202,
                    new List<string>() { "mse", "mae", "mape" });

        }

        public static IReadOnlyList<(DateTime start, DateTime end)> TrainingDataTimePeriods =>
            new List<(DateTime start, DateTime end)>()
            {
                //(new DateTime(2018, 5, 29), new DateTime(2018, 12, 31)),
                //(new DateTime(2019, 1, 12), new DateTime(2019, 4, 14)),
                //(new DateTime(2019, 5, 31), new DateTime(2019, 9, 23)),
                //(new DateTime(2019, 9, 28), new DateTime(2019, 12, 23)),
                //(new DateTime(2020, 1, 23), new DateTime(2020, 3, 1)),
                //(new DateTime(2020, 3, 9), new DateTime(2020, 4, 16)),
                //(new DateTime(2020, 5, 12), new DateTime(2020, 10, 26)),

                //(new DateTime(2017, 1, 1), DateTime.Now)
                (new DateTime(2021, 1, 1), DateTime.Now)
            };

        public long FileExpiration { get; } = 60 * 60 * 24 * 5;
        public IReadOnlyList<TimeIntervalEnum> TimeIntervals { get; } = new List<TimeIntervalEnum>() { TimeIntervalEnum.fiveMinute, TimeIntervalEnum.fifteenMinute, TimeIntervalEnum.thirtyMinute };
        public string RootDataDirectory { get; }
        public string RawDataPathDirectory { get; }
        public string ProcessedDataDirectory { get; }
        public string PredictionDataDirectory { get; }
        public string MinMaxDataDirectory { get; }
        public string TrainingDataDirectory { get; }
        public string ModelDirectory { get; }
        public int ModelRetrainDelayHours { get; } = 6;
        public HyperParameters HyperParams { get; }
        public object PythonLock { get; } = new object();
    }
}
