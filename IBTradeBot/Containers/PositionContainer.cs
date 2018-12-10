using IBApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class PositionContainer //: BaseContainer
    {
        public double Position { get; set; }
        public double AverageCost { get; set; }
        public Contract Contract { get; set; }
    }
}
