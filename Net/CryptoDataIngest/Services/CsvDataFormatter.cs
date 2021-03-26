using CryptoDataIngest.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace CryptoDataIngest.Services
{
    internal interface IModelFormatter
    {
        IEnumerable<string> Format<T>(IEnumerable<T> data);
        string GetHeader<T>();
    }

    internal class CsvDataFormatter : IModelFormatter
    {
        public string GetHeader<T>()
        {
            return 
                string
                .Join(",", typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .OrderBy(x => x.Name)
                .Select(x => x.Name));
        }

        public IEnumerable<string> Format<T>(IEnumerable<T> data)
        {
            foreach (var record in data)
                yield return 
                    string
                    .Join(",", typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .OrderBy(x => x.Name)
                    .Select(x => x.GetValue(record)));
        }
    }
}
