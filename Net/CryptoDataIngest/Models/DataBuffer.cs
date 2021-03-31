using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal interface IDataBuffer<T>
    {
        BlockingCollection<T> GetDataBuffer();
    }

    internal class DataBuffer<T> : IDataBuffer<T>
    {
        private readonly BlockingCollection<T> _dataBuffer = new();

        public BlockingCollection<T> GetDataBuffer() => _dataBuffer;
    }
}
