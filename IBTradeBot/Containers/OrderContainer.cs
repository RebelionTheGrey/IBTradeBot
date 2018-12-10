using IBApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class OrderContainer //: BaseContainer
    {
        public Order Order { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public OrderState OrderState { get; set; }
        public Contract Contract { get; set; }
    }
}
