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
            var models =
                _modelSourceRepo
                .ListModelSources()
                .GroupBy(x => x.Interval)
                .Select(x => x.OrderByDescending(x => x.Timestamp).FirstOrDefault());

            //get existing model ts and paths
            var recentModels =
                models
                .Where(x => (DateTime.Now - x.Timestamp).TotalHours < _config.ModelRetrainDelayHours);

            foreach (var model in recentModels)
                _bufferOut.AddData(model, stoppingToken);

            return Task.CompletedTask;
        }
    }
}
