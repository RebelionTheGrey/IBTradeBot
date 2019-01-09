using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IBTradeBot.Containers
{
    public class Account
    {
        private List<string> orderTypes = new List<string>() { "ApiPending", "PendingSubmit", "PreSubmitted", "Submitted" };

        public Mutex Sync { get; set; }
        public string AccountName { get; set; }
        public PnLContainerEx PnLContainer { get; set; }
        public PortfolioContainerEx PortfolioContainer { get; set; }
        public PositionContainerEx PositionContainer { get; set; }
        public OrderContainerEx OrderContainer { get; set; }

        public Account() => Sync = new Mutex(false);

        public double GetTotalPosition(string symbol)
        {
            return GetOrderedPosition(symbol) + GetOpenedPosition(symbol);
        }

        public double GetOrderedPosition(string symbol)
        {
            //Sync.WaitOne();

            var items = OrderContainer.Orders.Where(e => e.OrderStatus == null || orderTypes.Contains(e.OrderStatus.Status));
            var orderedPosition = items.Where(e => e.Order.ParentId == 0 && e.Contract.Symbol == symbol).Select(o =>
            {
                return o.OrderStatus != null ? o.OrderStatus.Remaining : o.Order.TotalQuantity;
            }).Sum();

            //Sync.ReleaseMutex();

            return orderedPosition;
        }

        public double GetOpenedPosition(string symbol)
        {
            //Sync.WaitOne();

            var item = PositionContainer.Positions.FirstOrDefault(e => e.Contract.Symbol == symbol);

            //Sync.ReleaseMutex();

            return item != null ? item.Position : 0.0d;
        }
    }
}
