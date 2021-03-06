﻿using Every;
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

using Ganss.Excel;
using Ganss;
using System.Globalization;

namespace IBTradeBot
{
    public class ForexData
    {
        public DateTime TradeTime { get; set; }
        public double Open { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double Close { get; set; }
        public double OHLC { get { return (Open + High + Low + Close) / 4; } }
    }

    public class WrapperImplementationEx : DefaultEWrapper
    {
        private static string defaulthost = "127.0.0.1";
        private static int defaultPort = 7503;
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

        private ConcurrentDictionary<int, ContractDetails> contracts = new ConcurrentDictionary<int, ContractDetails>();
        private ConcurrentDictionary<string, Account> accountList = new ConcurrentDictionary<string, Account>();
        private ConcurrentDictionary<int, string> links = new ConcurrentDictionary<int, string>();
        private ConcurrentDictionary<int, OrderContainer> orders = new ConcurrentDictionary<int, OrderContainer>();

        private ConcurrentDictionary<int, OrderElement> hangingOrders = new ConcurrentDictionary<int, OrderElement>();
        private ConcurrentDictionary<int, string> orderToAccountMapping = new ConcurrentDictionary<int, string>();

        private ConcurrentDictionary<string, bool> symbolPermission = new ConcurrentDictionary<string, bool>();

        private List<string> orderTypes = new List<string>() { "ApiPending", "PendingSubmit", "PreSubmitted", "Submitted" };

        private volatile bool stopTrading = false;
        private object orderLocker = new object();

        private int maxDaysDeep = -2;
        private int currentDaysShift = 0;

        private ExcelMapper excelFile = new ExcelMapper();
        private List<ForexData> forexData = new List<ForexData>();
        

        private void LoadTradeData()
        {
           // assetLoader = new Loader<AssetParameters>("../../assets.json", "Assets");
           // accountLoader = new Loader<AccountParameters>("../../accounts.json", "Accounts");
        }

        private void Initialize()
        {
            LoadTradeData();

            stopTrading = false;

            //Ever.y(DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Wednesday, DayOfWeek.Tuesday, DayOfWeek.Saturday).At(16, 29).Do(() => DayOpening());
            //Ever.y(DayOfWeek.Monday, DayOfWeek.Thursday, DayOfWeek.Wednesday, DayOfWeek.Tuesday, DayOfWeek.Saturday).At(16, 30).Do(() => DayClosing());
        }

        private void RechargeSymbol(string symbol)
        {
            symbolPermission[symbol] = true;
        }

        public void DayOpening()
        {
            stopTrading = false;

            Console.WriteLine("Day've been opened");
        }

        public void DayClosing()
        {
            stopTrading = true;

            var symbols = assetLoader.Elements.Select(e => e.Symbol);

            foreach (var account in accountList)
            {
                foreach (var orderElement in account.Value.OrderContainer.Orders)
                {
                    if (symbols.Contains(orderElement.Contract.Symbol) && orderTypes.Contains(orderElement.OrderState.Status)) { clientSocket.cancelOrder(orderElement.Order.OrderId); }
                }

                foreach (var positionElement in account.Value.PositionContainer.Positions)
                {
                    if (symbols.Contains(positionElement.Contract.Symbol))
                    {
                        clientSocket.reqIds(-1);

                        var orderAction = positionElement.Position > 0 ? "SELL" : "BUY";
                        var order = OrderSamples.MarketOrder(orderAction, Math.Abs(positionElement.Position));

                        clientSocket.placeOrder(nextValidOrderId, positionElement.Contract, order);
                    }
                }
            }

            Console.WriteLine("Day've been closed");
        }

        public WrapperImplementationEx() : this(defaulthost, defaultPort, defaultClientId) { }

        public WrapperImplementationEx(string host, int port, int? clientId)
        {
            this.host = host;
            this.port = port;

            this.clientId = clientId ?? defaultClientId;

            Initialize();

            signal = new EReaderMonitorSignal();
            clientSocket = new EClientSocket(this, signal);
        }

        public void Connect() => clientSocket.eConnect(host, port, clientId);

        public void StartMessageProcessing()
        {
            reader = new EReader(clientSocket, signal);
            reader.Start();

            messageProcessingTask = new Task(() => { while (clientSocket.IsConnected()) { signal.waitForSignal(); reader.processMsgs(); } });
            messageProcessingTask.Start();
        }

        public void GetContract(string code, string type, string exchange, string currency)
        {
            var contract = new Contract()
            {
                Symbol = code,
                SecType = type,
                Exchange = exchange,
                Currency = currency
            };

            int id = RandomGen3.Next();

            symbolPermission.TryAdd(code, true);


            /*****
            clientSocket.reqContractDetails(id, contract);
            clientSocket.reqMktData(id, contract, string.Empty, false, false, null);
            *****/
        }

        public override void contractDetails(int reqId, ContractDetails contractDetails)
        {
            contracts.TryAdd(reqId, contractDetails);
        }

        public override void managedAccounts(string accountsList)
        {
            var splittedAccounts = accountsList.Split(',').Where(e => e.Length > 0).ToList();

            splittedAccounts.ForEach(account =>
            {
                var positionId = RandomGen3.Next();
                var portfolioId = RandomGen3.Next();
                var pnlId = RandomGen3.Next();

                links.TryAdd(positionId, account);
                links.TryAdd(portfolioId, account);
                links.TryAdd(pnlId, account);

                var newAccount = new Account()
                {
                    AccountName = account,
                    PnLContainer = new PnLContainerEx() { Id = pnlId },
                    PortfolioContainer = new PortfolioContainerEx() { Id = portfolioId },
                    PositionContainer = new PositionContainerEx() { Id = positionId },
                    OrderContainer = new OrderContainerEx()                    
                };

                accountList.TryAdd(account, newAccount);

                clientSocket.reqAccountUpdatesMulti(portfolioId, account, "", true);
                clientSocket.reqPnL(pnlId, account, "");
                clientSocket.reqPositionsMulti(positionId, account, "");
            });

            foreach (var elem in assetLoader.Elements) { GetContract(elem.Symbol, elem.SecType, elem.Exchange, elem.Currency); }

            clientSocket.reqMarketDataType(1);
            clientSocket.reqAllOpenOrders();
            clientSocket.reqIds(-1);
        }

        public override void pnl(int reqId, double dailyPnL, double unrealizedPnL, double realizedPnL)
        {
            links.TryGetValue(reqId, out var accountName);
            accountList.TryGetValue(accountName, out var account);

            account.Sync.WaitOne();

            account.PnLContainer.DailyPnL = dailyPnL;
            account.PnLContainer.RealizedPnL = realizedPnL;
            account.PnLContainer.UnrealizedPnL = unrealizedPnL;

            account.Sync.ReleaseMutex();
        }

        public override void nextValidId(int orderId) => nextValidOrderId = orderId;

        public override void positionMulti(int reqId, string account, string modelCode, Contract contract, double pos, double avgCost)
        {
            Console.WriteLine("Position Multi. Request: " + reqId + ", Account: " + account + ", ModelCode: " + modelCode + ", Symbol: " + contract.Symbol + ", SecType: " + contract.SecType + ", Currency: " + contract.Currency + ", Position: " + pos + ", Avg cost: " + avgCost + "\n");

            accountList.TryGetValue(account, out var currentAccount);

            currentAccount.Sync.WaitOne();

            var accountPositions = currentAccount.PositionContainer;
            var positionElement = accountPositions.Positions.FirstOrDefault(e => e.Contract.Symbol == contract.Symbol);

            if (positionElement == null)
            {
                positionElement = new PositionElement();
                accountPositions.Positions.Add(positionElement);
            }

            positionElement.AverageCost = avgCost;
            positionElement.Position = pos;
            positionElement.Contract = contract;

            currentAccount.Sync.ReleaseMutex();
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

            lock (orderLocker)
            {
                if (orderToAccountMapping.TryGetValue(orderId, out var accountName))
                {
                    var account = accountList[accountName];

                    account.Sync.WaitOne();

                    var placedOrder = account.OrderContainer.Orders.FirstOrDefault(e => e.Order.OrderId == orderId);
                    placedOrder.OrderStatus = newStatus;

                    orderToAccountMapping.TryRemove(orderId, out var elem);

                    account.Sync.ReleaseMutex();
                }
                else
                {
                    var newOrder = new OrderElement()
                    {
                        Order = null,
                        OrderState = null,
                        Contract = null,
                        OrderStatus = newStatus,
                    };

                    hangingOrders.TryAdd(orderId, newOrder);
                }
            }

            Console.WriteLine("OrderStatus. Id: " + orderId + ", Status: " + status + ", Filled" + filled + ", Remaining: " + remaining
    + ", AvgFillPrice: " + avgFillPrice + ", PermId: " + permId + ", ParentId: " + parentId + ", LastFillPrice: " + lastFillPrice + ", ClientId: " + clientId + ", WhyHeld: " + whyHeld + ", MktCapPrice: " + mktCapPrice);
        }

        public override void openOrder(int orderId, Contract contract, Order order, OrderState orderState)
        {
            /*****
            if (accountList.Count == 0)
                return;

            var newOrder = new OrderElement()
            {
                Contract = contract,
                Order = order,
                OrderState = orderState,
                OrderStatus = null,
            };

            lock (orderLocker)
            {
                if (hangingOrders.TryGetValue(orderId, out var hangingOrder))
                {
                    newOrder.OrderStatus = hangingOrder.OrderStatus;
                    hangingOrders.TryRemove(orderId, out var elem);
                }
                else
                {
                    orderToAccountMapping.TryAdd(orderId, order.Account);
                }

                accountList[order.Account].OrderContainer.Orders.Add(newOrder);
            }

            symbolPermission[contract.Symbol] = true;

            Console.WriteLine("OpenOrder. ID: " + orderId + ", " + contract.Symbol + ", " + contract.SecType + " @ " + contract.Exchange + ": " + order.Action + ", " + order.OrderType + " " + order.TotalQuantity + ", " + orderState.Status);
        }

        public override void tickPrice(int tickerId, int field, double price, TickAttrib attribs) 
        {

            if (!contracts.ContainsKey(tickerId) || stopTrading)
                return;

            if (contracts.TryGetValue(tickerId, out var contractDetail))
            {
                var symbol = contractDetail.Contract.Symbol;
                var contract = contractDetail.Contract;
                var asset = assetLoader.Elements.FirstOrDefault(e => e.Symbol == symbol);

                Console.WriteLine($"Tick received: {symbol} to {contractDetail.Contract.Currency}, {price}, {DateTime.Now}");

                foreach (var account in accountList)
                {
                    var accountName = account.Key;
                    var accountValue = account.Value;
                    var validAccounts = accountLoader.Elements.Select(e => e.Account);

                    account.Value.Sync.WaitOne();

                    if (price >= asset.HighTakeprofit && price < asset.HighTakeprofit + (asset.HighStoploss - asset.HighTakeprofit) / 4 && validAccounts.Contains(accountName) && symbolPermission[symbol])// && totalPosition > -1 * asset.MaxPositionSize && validAccounts.Contains(accountData.Key.account))
                    {
                        var totalPosition = account.Value.GetTotalPosition(symbol);
                        symbolPermission[symbol] = false;
                        Once.After(10).Seconds.Do((s) => RechargeSymbol(s.Metadata), symbol);

                        clientSocket.reqIds(-1);

                        if (asset.MaxPositionSize - Math.Abs(totalPosition) > 0)
                        {
                            List<Order> bracket = OrderSamples.BracketOrder(nextValidOrderId, "SELL", asset.MaxPositionSize - Math.Abs(totalPosition), asset.HighTakeprofit, asset.Close, asset.HighStoploss);
                            bracket.ForEach(item => { item.Account = accountName; item.Tif = "GTC"; });

                            foreach (var elem in bracket)
                                clientSocket.placeOrder(elem.OrderId, contract, elem);
                        }
                    }

                    if (price <= asset.LowTakeprofit && price > asset.LowTakeprofit - (asset.LowTakeprofit - asset.LowStoploss) / 4 && validAccounts.Contains(accountName) && symbolPermission[symbol])// && totalPosition < asset.MaxPositionSize && validAccounts.Contains(accountData.Key.account))
                    {
                        var totalPosition = account.Value.GetTotalPosition(symbol);
                        symbolPermission[symbol] = false;
                        Once.After(10).Seconds.Do((s) => RechargeSymbol(s.Metadata), symbol);

                        clientSocket.reqIds(-1);

                        if (asset.MaxPositionSize - Math.Abs(totalPosition) > 0)
                        {

                            List<Order> bracket = OrderSamples.BracketOrder(nextValidOrderId, "BUY", asset.MaxPositionSize - Math.Abs(totalPosition), asset.LowTakeprofit, asset.Close, asset.LowStoploss);
                            bracket.ForEach(item => { item.Account = accountName; item.Tif = "GTC"; });

                            foreach (var elem in bracket)
                                clientSocket.placeOrder(elem.OrderId, contract, elem);
                        }

                    }

                    account.Value.Sync.ReleaseMutex();
                }
            }

            *****/
        }

        public override void accountUpdateMulti(int reqId, string account, string modelCode, string key, string value, string currency) //done 2
        {
            var keywords = new List<string>() { "MarketPrice", "MarketValue", "AverageCost" };

            if (!keywords.Contains(key))
                return;

            var currentAccount = accountList[account];

            currentAccount.Sync.WaitOne();

            var propertyInfo = currentAccount.PortfolioContainer.GetType().GetProperty(key);
            propertyInfo.SetValue(currentAccount.PortfolioContainer, double.Parse(value));

            currentAccount.Sync.ReleaseMutex();
        }
    }
}
