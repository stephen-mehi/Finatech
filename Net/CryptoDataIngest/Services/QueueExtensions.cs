using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal static class QueueExtensions
    {
        public static IEnumerable<T> DequeueAll<T>(this Queue<T> queue)
        {
            while (queue.TryDequeue(out var data))
                yield return data;
        }
    }
}
