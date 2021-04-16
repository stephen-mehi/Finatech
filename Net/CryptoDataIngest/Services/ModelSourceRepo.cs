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
        IReadOnlyDictionary<DateTime, string> ListModelSources();
    }

    internal class ModelSourceRepo : IModelSourceRepo
    {

        private string _modelRootDir;

        public ModelSourceRepo(
            GlobalConfiguration config)
        {
            _modelRootDir = config.ModelDirectory;
        }

        public IReadOnlyDictionary<DateTime, string> ListModelSources()
        {
            //get existing model ts and paths
            var modelTimestampMap =
                Directory
                .EnumerateDirectories(_modelRootDir)
                .ToDictionary(x =>
                {
                    var y =
                        Path
                        .GetFileName(x)
                        .Split("_", StringSplitOptions.RemoveEmptyEntries);

                    var date = new DateTime(int.Parse(y[2]), int.Parse(y[0]), int.Parse(y[1]), int.Parse(y[3]), int.Parse(y[4]), int.Parse(y[5]));

                    return date;
                }, x => x);

            return modelTimestampMap;
        }
    }
}
