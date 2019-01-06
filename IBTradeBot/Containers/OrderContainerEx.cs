using IBApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class OrderElement
    {
        public Order Order { get; set; }
        public OrderStatus OrderStatus { get; set; }
        public OrderState OrderState { get; set; }
        public Contract Contract { get; set; }
    }
    public class OrderContainerEx
    {
        public List<OrderElement> Orders { get; set;}
        public OrderContainerEx() => Orders = new List<OrderElement>();
    }
}
