using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IBTradeBot.Containers
{
    public class PnLContainerEx
    {
        public int Id { get; set; }
        public double RealizedPnL { get; set; }
        public double UnrealizedPnL { get; set; }
        public double DailyPnL { get; set; }
    }
}