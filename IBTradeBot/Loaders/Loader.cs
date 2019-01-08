using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Loaders
{
    public class Loader<T>
    {
        public List<T> Elements { get; set; } = new List<T>();

        public Loader(string file, string rootName)
        {
            JObject jsonData = JObject.Parse(File.ReadAllText(file));

            foreach (var elem in jsonData[rootName])
            {
                var accountParams = elem.ToObject<T>();
                Elements.Add(accountParams);
            }
        }
    }
}
