using CryptoDataIngest.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface IModelSourceRepo
    {
        IEnumerable<ModelSource> ListModelSources();
    }

    internal class ModelSourceRepo : IModelSourceRepo
    {

        private string _modelRootDir;

        public ModelSourceRepo(
            GlobalConfiguration config)
        {
            _modelRootDir = config.ModelDirectory;
        }

        public IEnumerable<ModelSource> ListModelSources()
        {
            //get existing model ts and paths
            var modelTimestampMap =
                Directory
                .EnumerateDirectories(_modelRootDir)
                .SelectMany(intervalFolder =>
                {
                    var interval =
                        Enum.Parse<TimeIntervalEnum>(Path.GetFileName(intervalFolder));

                    var modelInfos =
                        Directory
                        .EnumerateDirectories(intervalFolder)
                        .Select(timestampedFolder =>
                        {
                            var y = Path.GetFileName(timestampedFolder).Split("_", StringSplitOptions.RemoveEmptyEntries);
                            var timeStamp = new DateTime(int.Parse(y[2]), int.Parse(y[0]), int.Parse(y[1]), int.Parse(y[3]), int.Parse(y[4]), int.Parse(y[5]));
                            return new ModelSource(timeStamp, timestampedFolder, interval);
                        });


                    return modelInfos;
                });

            return modelTimestampMap;
        }
    }
}
