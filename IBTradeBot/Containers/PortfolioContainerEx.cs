using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class PortfolioContainerEx
    {
        public int Id { get; set; }
        public double MarketPrice { get; set; }
        public double MarketValue { get; set; }
        public double AverageCost { get; set; }
    }
}
