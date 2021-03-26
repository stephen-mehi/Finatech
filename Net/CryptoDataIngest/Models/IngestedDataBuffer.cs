using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal interface IIngestedDataBuffer<T>
    {
        BlockingCollection<T> GetDataBuffer();
    }

    internal class IngestedDataBuffer<T> : IIngestedDataBuffer<T>
    {
        private readonly BlockingCollection<T> _dataBuffer = new BlockingCollection<T>();

        public BlockingCollection<T> GetDataBuffer() => _dataBuffer;
    }
}
