using CryptoDataIngest.Models;
using CryptoDataIngest.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Workers
{
    internal class MostRecentModelTask : BackgroundService
    {
        private readonly IDataBufferWriter<ModelSource> _bufferOut;
        private readonly IModelSourceRepo _modelSourceRepo;
        private readonly GlobalConfiguration _config;

        public MostRecentModelTask(
            IDataBufferWriter<ModelSource> bufferOut,
            IModelSourceRepo modelSrcRepo,
            GlobalConfiguration config)
        {
            _config = config;
            _bufferOut = bufferOut;
            _modelSourceRepo = modelSrcRepo;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken = default)
        {
            //get existing model ts and paths
            var modelTimestampMap = _modelSourceRepo.ListModelSources();
            //if any
            if(modelTimestampMap.Count > 0)
            {
                var mostRecentModelTs = modelTimestampMap.Keys.Max();
                var timeDiffHours = (DateTime.Now - mostRecentModelTs);

                //model exists that is not older than retrain delay, add model source to out buffer
                if (timeDiffHours.TotalHours < _config.ModelRetrainDelayHours)
                    _bufferOut.AddData(new ModelSource(modelTimestampMap[mostRecentModelTs]), stoppingToken);
            }


            return Task.CompletedTask;
        }
    }
}
