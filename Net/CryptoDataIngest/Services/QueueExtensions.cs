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

        public static IEnumerable<T> DequeueMany<T>(this Queue<T> queue, int amount)
        {
            for (int i = 0; i < amount; i++)
            {
                queue.TryDequeue(out var data);

                if (data == null)
                    break;

                yield return data;
            }

        }
    }
}
