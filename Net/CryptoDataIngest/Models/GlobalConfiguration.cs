﻿using System;
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
            ModelDirectory = Path.Combine(RootDataDirectory, "Model", "04_10_2021_11_50_24");

            HyperParams = new HyperParameters(512, "tanh", "mse", "adam", 0.2, 25, 125, 12, 1, 202);

            //TODO: ADD MOST RECENT TIME RANGE
        }

        public static IReadOnlyList<(DateTime start, DateTime end)> TrainingDataTimePeriods  => 
            new List<(DateTime start, DateTime end)>()
            {
                //(new DateTime(2018, 5, 29), new DateTime(2018, 12, 31)),
                //(new DateTime(2019, 1, 12), new DateTime(2019, 4, 14)),
                //(new DateTime(2019, 5, 31), new DateTime(2019, 9, 23)),
                //(new DateTime(2019, 9, 28), new DateTime(2019, 12, 23)),
                //(new DateTime(2020, 1, 23), new DateTime(2020, 3, 1)),
                //(new DateTime(2020, 3, 9), new DateTime(2020, 4, 16)),
                //(new DateTime(2020, 5, 12), new DateTime(2020, 10, 26)),

                (new DateTime(2021, 1, 1), DateTime.Now)
            };

        public long FileExpiration { get; } = 60 * 60 * 24 * 5;
        public TimeIntervalEnum TimeInterval { get; } = TimeIntervalEnum.fiveMinute;
        public string RootDataDirectory { get; }
        public string RawDataPathDirectory { get; }
        public string ProcessedDataDirectory { get; }
        public string PredictionDataDirectory { get; }
        public string MinMaxDataPath { get; }
        public string TrainingDataPath { get; }
        public string ModelDirectory{ get; }
        public HyperParameters HyperParams { get; }
    }
}
