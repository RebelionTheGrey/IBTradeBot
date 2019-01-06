using IBApi;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class PositionElement
    {
        public double Position { get; set; }
        public double AverageCost { get; set; }
        public Contract Contract { get; set; }
    }
    public class PositionContainerEx
    {
        public int Id { get; set; }
        public string Account { get; set; }
        public List<PositionElement> Positions { get; set; }
        public PositionContainerEx() => Positions = new List<PositionElement>();
    }
}
