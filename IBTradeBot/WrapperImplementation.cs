using IBApi;
using IBTradeBot.Containers;
using IBTradeBot.Loaders;
using IBTradeBot.Randomizer;
using Samples;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Every;

using PositionCollection = System.Collections.Concurrent.ConcurrentDictionary<string, IBTradeBot.Containers.PositionContainer>;

namespace IBTradeBot
{
    public class WrapperImplementation : DefaultEWrapper
    {
        private static string defaulthost = "127.0.0.1";
        private static int defaultPort = 7501;
        private static int defaultClientId = 0;

        private string host;
        private int port;
        private int clientId;

        private EClientSocket clientSocket;
        private EReaderSignal signal;
        private EReader reader;

        private Task messageProcessingTask;

        private Loader<AssetParameters> assetLoader;
        private Loader<AccountParameters> accountLoader;

        private int nextValidOrderId = 0;

        private ConcurrentDictionary<(int id, string account), PositionCollection> positions = new ConcurrentDictionary<(int id, string account), PositionCollection>();
        private ConcurrentDictionary<(int id, string account), PortfolioContainer> portfolios = new ConcurrentDictionary<(int id, string account), PortfolioContainer>();
        private ConcurrentDictionary<(int id, string account), PnLContainer> pnls = new ConcurrentDictionary<(int id, string account), PnLContainer>();
        private ConcurrentDictionary<int, OrderContainer> orders = new ConcurrentDictionary<int, OrderContainer>();
        private ConcurrentDictionary<int, ContractDetails> contracts = new ConcurrentDictionary<int, ContractDetails>();

        private List<string> orderTypes = new List<string>() { "ApiPending", "PendingSubmit", "PreSubmitted", "Submitted" };

        private object positionLocker = new object();
        private object orderLocker = new object();
        private object portfolioLocker = new object();

        private volatile bool stopTrading;

        private void Initialize()
        {
            assetLoader = new Loader<AssetParameters>("../../assets.json", "Assets");
            accountLoader = new Loader<AccountParameters>("../../accounts.json", "Accounts");

            stopTrading = false;

            Ever.y(DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Wednesday, DayOfWeek.Tuesday, DayOfWeek.Saturday).At(0, 30).Do(() => DayOpening());
            Ever.y(DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Wednesday, DayOfWeek.Tuesday, DayOfWeek.Saturday).At(23, 40).Do(() => DayClosing());
        }

        public WrapperImplementation() : this(defaulthost, defaultPort, defaultClientId) { } //done 2

        public WrapperImplementation(string host, int port, int? clientId) //done 2
        {
            this.host = host;
            this.port = port;

            this.clientId = clientId ?? defaultClientId;

            Initialize();

            signal = new EReaderMonitorSignal();
            clientSocket = new EClientSocket(this, signal);
        }

        public void Connect() => clientSocket.eConnect(host, port, clientId); //done 2
        public void StartMessageProcessing() //done 2
        {
            reader = new EReader(clientSocket, signal);
            reader.Start();

            messageProcessingTask = new Task(() => { while (clientSocket.IsConnected()) { signal.waitForSignal(); reader.processMsgs(); } });
            messageProcessingTask.Start();
        }

        public void DayOpening()
        {
            stopTrading = false;
        }

        public void DayClosing()
        {
            stopTrading = true;

            var assets = assetLoader.Accounts.Select(e => e.Symbol);
            var accounts = accountLoader.Accounts.Select(e => e.Account);

            lock (orderLocker)
            {
                foreach (var symbol in assets)
                {
                    foreach (var order in orders.Values)
                    {
                        if (order.Contract.Symbol == symbol && orderTypes.Contains(order.OrderState.Status)) { clientSocket.cancelOrder(order.Order.OrderId); }
                    }
                }

                foreach (var account in accounts)
                {
                    var currentCollection = positions.FirstOrDefault(k => k.Key.account == account).Value;

                    if (currentCollection != null)
                    {
                        foreach(var position in currentCollection)
                        {
                            if (assets.Contains(position.Key))
                            {
                                clientSocket.reqIds(-1);

                                var details = contracts.FirstOrDefault(c => c.Value.Contract.Symbol == position.Key).Value;

                                var orderAction = position.Value.Position > 0 ? "SELL" : "BUY";
                                var order = OrderSamples.MarketOrder(orderAction, Math.Abs(position.Value.Position));

                                clientSocket.placeOrder(nextValidOrderId, details.Contract, order);
                            }
                        }
                    }
                }
            }
        }

        public void GetContract(string code, string type, string exchange, string currency) //done 2
        {
            var contract = new Contract()
            {
                Symbol = code,
                SecType = type,
                Exchange = exchange,
                Currency = currency
            };

            int id = RandomGen3.Next();

            clientSocket.reqContractDetails(id, contract);
            clientSocket.reqMktData(id, contract, string.Empty, false, false, null);
        }
        public override void contractDetails(int reqId, ContractDetails contractDetails) //done 2
        {
            contracts.TryAdd(reqId, contractDetails);
        }

        public override void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL) //done 2
        {
            var accountPnLPair = pnls.FirstOrDefault(e => e.Key.id == reqId);
            var accountPnL = accountPnLPair.Value;

            if (accountPnL == null)
                return;


            var newPnL = new PnLContainer()
            {
                DailyPnL = dailyPnL,
                RealizedPnL = realizedPnL,
                UnrealizedPnL = unrealizedPnL,
            };

            pnls.TryUpdate(accountPnLPair.Key, newPnL, accountPnL);
        }
        public override void nextValidId(int orderId) //done 2
        {
            nextValidOrderId = orderId;
        }

        public override void tickPrice(int tickerId, int field, double price, TickAttrib attribs) //done 2
        {
            lock (orderLocker)
            {
                if (stopTrading)
                    return;

                if (!contracts.ContainsKey(tickerId))
                    return;

                if (contracts.TryGetValue(tickerId, out var contractDetail))
                {
                    var symbol = contractDetail.Contract.Symbol;
                    var positionInAccount = new Dictionary<string, (double opened, double ordered)>();

                    foreach (var accountPosition in positions)
                    {
                        var currentPosition = accountPosition.Value.TryGetValue(symbol, out var position) ? position.Position : 0;
                        positionInAccount.Add(accountPosition.Key.account, (currentPosition, 0));
                    }

                    foreach (var order in orders)
                    {
                        if (order.Value.Order != null)
                        {
                            if (order.Value.Contract.Symbol == symbol && positionInAccount.ContainsKey(order.Value.Order.Account))
                            {
                                var currentOrdered = positionInAccount[order.Value.Order.Account].ordered;
                                var currentOpened = positionInAccount[order.Value.Order.Account].opened;

                                if (orderTypes.Contains(order.Value.OrderState.Status) && order.Value.OrderStatus != null && order.Value.Order.ParentId == 0) { currentOrdered += order.Value.OrderStatus.Remaining; }

                                positionInAccount[order.Value.Order.Account] = (currentOpened, currentOrdered);
                            }
                        }
                    }


                    var asset = assetLoader.Accounts.FirstOrDefault(e => e.Symbol == symbol);

                    foreach (var accountData in positions)
                    {
                        var totalPosition = positionInAccount[accountData.Key.account].opened + positionInAccount[accountData.Key.account].ordered;
                        var contract = contracts.FirstOrDefault(e => e.Value.Contract.Symbol == symbol).Value.Contract;
                        var validAccounts = accountLoader.Accounts.Select(e => e.Account);

                        //foreach (var clientAccount in accountLoader.Accounts.Select(e => e.Account))
                        //{
                        if (price >= asset.HighTakeprofit && price < (asset.HighTakeprofit + asset.HighStoploss) / 2 && totalPosition > -1 * asset.MaxPositionSize && validAccounts.Contains(accountData.Key.account))
                        {
                            clientSocket.reqIds(-1);

                            List<Order> bracket = OrderSamples.BracketOrder(nextValidOrderId, "SELL", asset.MaxPositionSize - Math.Abs(totalPosition), price, asset.Close, asset.HighStoploss);
                            bracket.ForEach(item => item.Account = accountData.Key.account);

                            foreach (var elem in bracket)
                                clientSocket.placeOrder(elem.OrderId, contract, elem);
                        }

                        if (price <= asset.LowTakeprofit && price > (asset.LowTakeprofit + asset.LowStoploss) / 2 && totalPosition < asset.MaxPositionSize && validAccounts.Contains(accountData.Key.account))
                        {
                            clientSocket.reqIds(-1);

                            List<Order> bracket = OrderSamples.BracketOrder(nextValidOrderId, "BUY", asset.MaxPositionSize - Math.Abs(totalPosition), price, asset.Close, asset.LowStoploss);
                            bracket.ForEach(item => item.Account = accountData.Key.account);

                            foreach (var elem in bracket)
                                clientSocket.placeOrder(elem.OrderId, contract, elem);


                        }
                        //}
                    }
                }
            }
        }

        public override void managedAccounts(string accountsList) //done 2
        {
            var splittedAccounts = accountsList.Split(',').Where(e => e.Length > 0).ToList();

            splittedAccounts.ForEach(account =>
            {
                var positionId = RandomGen3.Next();
                var portfolioId = RandomGen3.Next();
                var pnlId = RandomGen3.Next();

                positions.TryAdd((positionId, account), new PositionCollection());
                portfolios.TryAdd((portfolioId, account), new PortfolioContainer());
                pnls.TryAdd((pnlId, account), new PnLContainer());

                clientSocket.reqAccountUpdatesMulti(portfolioId, account, "", true);
                clientSocket.reqPnL(pnlId, account, "");
                clientSocket.reqPositionsMulti(positionId, account, "");
            });

            foreach (var elem in assetLoader.Accounts) { GetContract(elem.Symbol, elem.SecType, elem.Exchange, elem.Currency); }

            clientSocket.reqMarketDataType(1);
            clientSocket.reqAllOpenOrders();
            clientSocket.reqIds(-1);
        }
        public override void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) //done 2
        {
            var keywords = new List<string>() { "MarketPrice", "MarketValue", "AverageCost" };
           // Console.WriteLine("Account Updaate Multi. Request: " + reqId + ", Account: " + account + ", ModelCode: " + modelCode + ", Key: " + key + ", Value: " + value + ", Currency: " + currency + "\n");


            if (!keywords.Contains(key))
                return;

            var portfolioKey = (reqId, account);

            var newPortfolio = new PortfolioContainer()
            {
                AverageCost = 0,
                MarketPrice = 0,
                MarketValue = 0,
            };

            var propertyInfo = newPortfolio.GetType().GetProperty(key);
            propertyInfo.SetValue(newPortfolio, double.Parse(value));

            lock (portfolioLocker)
            {
                portfolios.AddOrUpdate(portfolioKey, newPortfolio, (oldKey, oldPortfolio) =>
                {
                    var oldPropertyInfo = oldPortfolio.GetType().GetProperty(key);
                    oldPropertyInfo.SetValue(oldPortfolio, double.Parse(value));

                    Console.WriteLine("Account Updaate Multi. Request: " + reqId + ", Account: " + account + ", ModelCode: " + modelCode + ", Key: " + key + ", Value: " + value + ", Currency: " + currency + "\n");

                    return oldPortfolio;
                });
            }           
        }
        public override void positionMulti(int reqId, string account, string modelCode, Contract contract, double pos, double avgCost) //done 2
        {
            Console.WriteLine("Position Multi. Request: " + reqId + ", Account: " + account + ", ModelCode: " + modelCode + ", Symbol: " + contract.Symbol + ", SecType: " + contract.SecType + ", Currency: " + contract.Currency + ", Position: " + pos + ", Avg cost: " + avgCost + "\n");

            var positionKey = (reqId, account);

            if (!positions.ContainsKey(positionKey)) 
                return;

            if (positions.TryGetValue(positionKey, out var positionCollection))
            {
                var newPosition = new PositionContainer()
                {
                    AverageCost = avgCost,
                    Contract = contract,
                    Position = pos,
                };

                lock (positionLocker)
                {
                    positionCollection.AddOrUpdate(contract.Symbol, newPosition, (oldKey, oldPosition) =>
                    {
                        oldPosition.AverageCost = avgCost;
                        oldPosition.Position = pos;

                        return oldPosition;
                    });
                }
            }
        }

        public override void openOrder(int orderId, Contract contract, Order order, OrderState orderState) //done 2
        {
            var newOrder =  new OrderContainer()
            {
                Contract = contract,
                Order = order,
                OrderState = orderState,
                OrderStatus = null,
            };

            lock (orderLocker)
            {
                orders.AddOrUpdate(orderId, newOrder, (oldKey, oldOrder) =>
                {
                    oldOrder.OrderState = orderState;
                    oldOrder.Order = order;

                    return oldOrder;
                });
            }

            Console.WriteLine("OpenOrder. ID: " + orderId + ", " + contract.Symbol + ", " + contract.SecType + " @ " + contract.Exchange + ": " + order.Action + ", " + order.OrderType + " " + order.TotalQuantity + ", " + orderState.Status);
            if (order.WhatIf)
            {
                Console.WriteLine("What-If. ID: " + orderId +
                    ", InitMarginBefore: " + Util.formatDoubleString(orderState.InitMarginBefore) + ", MaintMarginBefore: " + Util.formatDoubleString(orderState.MaintMarginBefore) + " EquityWithLoanBefore: " + Util.formatDoubleString(orderState.EquityWithLoanBefore) +
                    ", InitMarginChange: " + Util.formatDoubleString(orderState.InitMarginChange) + ", MaintMarginChange: " + Util.formatDoubleString(orderState.MaintMarginChange) + " EquityWithLoanChange: " + Util.formatDoubleString(orderState.EquityWithLoanChange) +
                    ", InitMarginAfter: " + Util.formatDoubleString(orderState.InitMarginAfter) + ", MaintMarginAfter: " + Util.formatDoubleString(orderState.MaintMarginAfter) + " EquityWithLoanAfter: " + Util.formatDoubleString(orderState.EquityWithLoanAfter));
            }
        }
        public override void orderStatus(int orderId, string status, double filled, double remaining, double avgFillPrice, int permId, int parentId, double lastFillPrice, int clientId, string whyHeld, double mktCapPrice)
        {
            var newStatus = new OrderStatus()
            {
                AvgFillPrice = avgFillPrice,
                ClientId = clientId,
                Filled = filled,
                Remaining = remaining,
                LastFillPrice = lastFillPrice,
                MktCapPrice = mktCapPrice,
                ParentId = parentId,
                PermId = permId,
                Status = status,
                WhyHeld = whyHeld
            };

            var newOrder = new OrderContainer()
            {
                Order = null,
                OrderState = null,
                Contract = null,
                OrderStatus = newStatus,
            };

            lock (orderLocker)
            {
                orders.AddOrUpdate(orderId, newOrder, (oldKey, oldOrder) =>
                {
                    oldOrder.OrderStatus = newStatus;
                    return oldOrder;
                });
            }

            Console.WriteLine("OrderStatus. Id: " + orderId + ", Status: " + status + ", Filled" + filled + ", Remaining: " + remaining
    + ", AvgFillPrice: " + avgFillPrice + ", PermId: " + permId + ", ParentId: " + parentId + ", LastFillPrice: " + lastFillPrice + ", ClientId: " + clientId + ", WhyHeld: " + whyHeld + ", MktCapPrice: " + mktCapPrice);
        }
    }
}

