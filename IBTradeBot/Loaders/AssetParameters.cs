using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Loaders
{
    public class AssetParameters
    {
        public string Symbol { get; set; }
        public string Currency { get; set; }
        public string SecType { get; set; }
        public string Exchange { get; set; }
        public double LowStoploss { get; set; }
        public double HighStoploss { get; set; }
        public double LowTakeprofit { get; set; }
        public double HighTakeprofit { get; set; }
        public double Close { get; set; }
        public double MaxPositionSize { get; set; }
    }
}
