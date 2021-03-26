using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal enum TimeIntervalEnum
    {
        none = 0,
        fiveMinute = 300,
        fifteenMinute = 900,
        thirtyMinute = 1800,
        twoHour = 7200,
        fourHour = 14400,
        oneDay = 86400
    }
}
