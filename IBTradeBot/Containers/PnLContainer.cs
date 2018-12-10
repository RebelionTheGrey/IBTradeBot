using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class PnLContainer //: BaseContainer
    {
        public double RealizedPnL { get; set; }
        public double UnrealizedPnL { get; set; }
        public double DailyPnL { get; set; }
    }
}
