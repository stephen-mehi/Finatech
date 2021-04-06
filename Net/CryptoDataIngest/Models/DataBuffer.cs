using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal interface IDataBufferReader<T> : IDisposable
    {
        IAsyncEnumerable<T> GetDataAsync(CancellationToken ct = default);
    }

    internal interface IDataBufferWriter<T> : IDisposable
    {
        void AddData(T data, CancellationToken ct = default);
    }

    internal class DataBuffer<T> : IDataBufferReader<T>, IDataBufferWriter<T>
    {
        private bool _disposed;
        private readonly BlockingCollection<T> _dataBuffer = new();

        public async IAsyncEnumerable<T> GetDataAsync([EnumeratorCancellation]CancellationToken ct = default)
        {
            while (!ct.IsCancellationRequested && !_dataBuffer.IsCompleted)
            {
                T data = default;
                bool foundData = default;

                try
                {
                    foundData = _dataBuffer.TryTake(out data);
                }
                //ignore taking an item if the collection is 'complete' since will just exit on next loop
                catch (InvalidOperationException) { }

                if (foundData)
                    yield return data;
                else
                    await Task.Delay(500, ct);
            }
        }

        public void AddData(T data, CancellationToken ct = default)
        {
            _dataBuffer.Add(data, ct);
        }

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // Dispose managed state (managed objects).
                _dataBuffer.Dispose();
            }

            _disposed = true;
        }
    }
}
