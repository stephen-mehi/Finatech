using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CryptoDataIngest.Models
{
    internal static class ColorConsole
    {
        private static readonly object locker = new object();
        public static void WriteLine(string message, ConsoleColor messageColor = default(ConsoleColor))
        {
            lock (locker)
            {
                Console.ForegroundColor = messageColor;
                Console.WriteLine(message);
                Console.ResetColor();
            }
        }
        public static void WriteLineWithTimestamp(string message, ConsoleColor messageColor = default(ConsoleColor))
        {
            lock (locker)
            {
                Console.ForegroundColor = messageColor;
                Console.WriteLine($"{DateTime.Now} > {message}");
                Console.ResetColor();
            }
        }
    }
}
