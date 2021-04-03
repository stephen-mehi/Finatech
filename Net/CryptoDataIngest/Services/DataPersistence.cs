using CryptoDataIngest.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface IDataPersistence
    {
        Task WriteToDirectoryAsync<T>(string path, IEnumerable<T> data, CancellationToken ct = default);
    }

    class DataPersistence : IDataPersistence
    {
        private readonly IModelFormatter _formatter;
        private readonly long _fileExpirationUnixTime;

        public DataPersistence(
            IModelFormatter formatter,
            GlobalConfiguration config)
        {
            _formatter = formatter;
            _fileExpirationUnixTime = config.FileExpiration;
        }
        
        public async Task WriteToDirectoryAsync<T>(string directory, IEnumerable<T> data, CancellationToken ct = default)
        {
            Directory.CreateDirectory(directory);

            //calculate current unix time
            long currentUnixTimestamp = (long)(DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            var fileTimeStamps =
                Directory
                .GetFiles(directory)
                .Select(p => long.Parse(Path.GetFileNameWithoutExtension(p)))
                .ToList();

            //format/supplement data
            var formattedData = $"{string.Join(Environment.NewLine, _formatter.Format(data))}{Environment.NewLine}";

            //try to get max 
            long fileTimestamp = default;
            long mostRecentFileTimeStamp = fileTimeStamps.DefaultIfEmpty().Max();

            //create new file with current timestamp if no previous files or if most recent file was made more than a day ago
            if (fileTimeStamps.Count == 0 || currentUnixTimestamp - mostRecentFileTimeStamp > _fileExpirationUnixTime)
            {
                formattedData = $"{_formatter.GetHeader<T>()}{Environment.NewLine}{formattedData}";
                fileTimestamp = currentUnixTimestamp;
            }
            else
                fileTimestamp = mostRecentFileTimeStamp;

            //write batch of new data to file. Create new file every day
            await File.AppendAllTextAsync(Path.Combine(directory, $"{fileTimestamp}.csv"), formattedData, ct);
        }
    }
}
