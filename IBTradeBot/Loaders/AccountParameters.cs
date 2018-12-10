using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Loaders
{
    public class AccountParameters
    {
        public string Account { get; set; }
        public string BaseCurrency { get; set; }
        public double MaxAccountPositionSize { get; set; }
    }
}
