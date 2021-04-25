using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal class ModelSource
    {
        public ModelSource(
            DateTime timestamp,
            string path,
            TimeIntervalEnum interval)
        {
            Timestamp = timestamp;
            ModelDirectory = path;
            Interval = interval;
        }

        public DateTime Timestamp { get; }
        public string ModelDirectory { get; }
        public TimeIntervalEnum Interval { get; }
    }
}
