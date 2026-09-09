using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.MarketData;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.TradingApp.Maui.Models;

// Type aliases to disambiguate between library and local models
using MauiQuote = OoplesFinance.TradingApp.Maui.Models.Quote;
using MauiBar = OoplesFinance.TradingApp.Maui.Models.Bar;
using MauiOrder = OoplesFinance.TradingApp.Maui.Models.Order;
using MauiOrderStatus = OoplesFinance.TradingApp.Maui.Models.OrderStatus;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Direct Alpaca portfolio service without Supabase dependency.
/// Ideal for POC/demo purposes.
/// </summary>
public sealed class DirectAlpacaPortfolioService : IPortfolioService, IDisposable
{
    private readonly AlpacaBroker _broker;
    private bool _disposed;

    public DirectAlpacaPortfolioService(AlpacaOptions options)
    {
        _broker = new AlpacaBroker(options);
    }

    public async Task<AccountInfo> GetAccountAsync()
    {
        try
        {
            App.LogError("DirectAlpacaPortfolioService.GetAccountAsync", "Fetching account from Alpaca...");
            var account = await _broker.GetAccountAsync().ConfigureAwait(false);
            App.LogError("DirectAlpacaPortfolioService.GetAccountAsync", $"Account: Equity=${account.Equity:N2}, Cash=${account.Cash:N2}, BuyingPower=${account.BuyingPower:N2}");

            var positions = await GetPositionsAsync().ConfigureAwait(false);
            var totalUnrealizedPnL = positions.Sum(p => p.UnrealizedPnL);

            return new AccountInfo
            {
                PortfolioValue = account.Equity,
                Cash = account.Cash,
                BuyingPower = account.BuyingPower,
                TodayPnL = account.DayPnL,
                TotalPnL = totalUnrealizedPnL
            };
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaPortfolioService.GetAccountAsync", ex);
            return new AccountInfo();
        }
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        try
        {
            App.LogError("DirectAlpacaPortfolioService.GetPositionsAsync", "Fetching positions from Alpaca...");
            var brokerPositions = await _broker.GetPositionsAsync().ConfigureAwait(false);
            App.LogError("DirectAlpacaPortfolioService.GetPositionsAsync", $"Found {brokerPositions.Count} positions");

            foreach (var p in brokerPositions)
            {
                App.LogError("DirectAlpacaPortfolioService.GetPositionsAsync", $"  Position: {p.Symbol} x{p.Quantity} @ ${p.CurrentPrice:N2} (PnL: ${p.UnrealizedPnL:N2})");
            }

            return brokerPositions.Select(p => new Position
            {
                Symbol = p.Symbol,
                CompanyName = p.Symbol,
                Quantity = (int)p.Quantity,
                AveragePrice = p.AverageEntryPrice,
                CurrentPrice = p.CurrentPrice,
                MarketValue = p.MarketValue,
                UnrealizedPnL = p.UnrealizedPnL,
                DayPnL = 0
            }).ToList();
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaPortfolioService.GetPositionsAsync", ex);
            return new List<Position>();
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _broker.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Direct Alpaca market data service without Supabase dependency.
/// </summary>
public sealed class DirectAlpacaMarketDataService : IMarketDataService, IDisposable
{
    private readonly AlpacaMarketDataProvider _provider;
    private bool _disposed;

    public DirectAlpacaMarketDataService(AlpacaOptions options)
    {
        _provider = new AlpacaMarketDataProvider(options, enableStreaming: false);
    }

    public async Task<MauiQuote?> GetQuoteAsync(string symbol)
    {
        try
        {
            App.LogError("DirectAlpacaMarketDataService.GetQuoteAsync", $"Fetching quote for {symbol}...");
            var snapshot = await _provider.GetSnapshotAsync(symbol).ConfigureAwait(false);
            if (snapshot is null)
            {
                App.LogError("DirectAlpacaMarketDataService.GetQuoteAsync", $"No snapshot returned for {symbol}");
                return null;
            }

            var currentPrice = snapshot.CurrentPrice;
            var previousClose = snapshot.PreviousBar?.Close ?? currentPrice;
            var change = currentPrice - previousClose;
            var changePercent = previousClose > 0 ? change / previousClose : 0m;

            App.LogError("DirectAlpacaMarketDataService.GetQuoteAsync", $"Quote {symbol}: ${currentPrice:N2} (change: {changePercent:P2})");

            return new MauiQuote
            {
                Symbol = symbol.ToUpperInvariant(),
                CompanyName = symbol.ToUpperInvariant(),
                LastPrice = currentPrice,
                Change = change,
                ChangePercent = changePercent,
                BidPrice = snapshot.LatestQuote?.Bid ?? currentPrice,
                AskPrice = snapshot.LatestQuote?.Ask ?? currentPrice,
                PreviousClose = previousClose,
                High = snapshot.DailyBar?.High ?? currentPrice,
                Low = snapshot.DailyBar?.Low ?? currentPrice,
                Open = snapshot.DailyBar?.Open ?? currentPrice,
                Volume = snapshot.DailyBar?.Volume ?? 0,
                Timestamp = snapshot.LatestQuote?.Timestamp ?? DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaMarketDataService.GetQuoteAsync", ex);
            return null;
        }
    }

    public Task<MarketStatus> GetMarketStatusAsync()
    {
        var now = DateTime.Now;
        try
        {
            var estNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

            var isWeekday = estNow.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
            var marketOpen = new TimeSpan(9, 30, 0);
            var marketClose = new TimeSpan(16, 0, 0);
            var isMarketHours = estNow.TimeOfDay >= marketOpen && estNow.TimeOfDay <= marketClose;

            var isOpen = isWeekday && isMarketHours;
            var nextOpen = isOpen ? (DateTime?)null : GetNextMarketOpen(estNow);
            var nextClose = isOpen ? estNow.Date + marketClose : (DateTime?)null;

            return Task.FromResult(new MarketStatus
            {
                IsOpen = isOpen,
                NextOpen = nextOpen,
                NextClose = nextClose
            });
        }
        catch
        {
            // Fallback if timezone conversion fails
            return Task.FromResult(new MarketStatus
            {
                IsOpen = true,
                NextOpen = null,
                NextClose = null
            });
        }
    }

    public async Task<List<MauiBar>> GetHistoricalBarsAsync(string symbol, string timeframe, int count)
    {
        try
        {
            App.LogError("DirectAlpacaMarketDataService.GetHistoricalBarsAsync", $"Fetching {timeframe} bars for {symbol}...");
            var endDate = DateTime.UtcNow;
            var startDate = timeframe switch
            {
                "1D" => endDate.AddDays(-1),
                "1W" => endDate.AddDays(-7),
                "1M" => endDate.AddMonths(-1),
                "3M" => endDate.AddMonths(-3),
                "1Y" => endDate.AddYears(-1),
                _ => endDate.AddYears(-5)
            };

            var interval = timeframe switch
            {
                "1D" => BarTimeframe.Minute5,
                "1W" => BarTimeframe.Hour1,
                "1M" => BarTimeframe.Day,
                "3M" => BarTimeframe.Day,
                "1Y" => BarTimeframe.Day,
                _ => BarTimeframe.Week
            };

            var marketBars = await _provider.GetHistoricalBarsAsync(symbol, startDate, endDate, interval).ConfigureAwait(false);
            App.LogError("DirectAlpacaMarketDataService.GetHistoricalBarsAsync", $"Received {marketBars.Count} bars for {symbol}");

            return marketBars.Select(b => new MauiBar
            {
                Timestamp = b.Timestamp,
                Open = b.Open,
                High = b.High,
                Low = b.Low,
                Close = b.Close,
                Volume = b.Volume
            }).ToList();
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaMarketDataService.GetHistoricalBarsAsync", ex);
            return new List<MauiBar>();
        }
    }

    private static DateTime GetNextMarketOpen(DateTime from)
    {
        var next = from.Date.AddDays(1).AddHours(9).AddMinutes(30);
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }
        return next;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _provider.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Direct Alpaca order service without Supabase dependency.
/// </summary>
public sealed class DirectAlpacaOrderService : IOrderService, IDisposable
{
    private readonly AlpacaBroker _broker;
    private readonly List<MauiOrder> _orderHistory = new();
    private readonly object _lock = new();
    private bool _disposed;

    public DirectAlpacaOrderService(AlpacaOptions options)
    {
        _broker = new AlpacaBroker(options);
    }

    public Task<List<MauiOrder>> GetOpenOrdersAsync()
    {
        // For now, return orders from history that are still pending
        lock (_lock)
        {
            var openOrders = _orderHistory
                .Where(o => o.Status == MauiOrderStatus.Pending || o.Status == MauiOrderStatus.PartiallyFilled)
                .ToList();
            return Task.FromResult(openOrders);
        }
    }

    public Task<List<MauiOrder>> GetRecentOrdersAsync(int count)
    {
        lock (_lock)
        {
            var recent = _orderHistory
                .OrderByDescending(o => o.SubmittedAt)
                .Take(count)
                .ToList();
            return Task.FromResult(recent);
        }
    }

    public async Task<OrderResult> SubmitOrderAsync(OrderRequest request)
    {
        try
        {
            App.LogError("DirectAlpacaOrderService.SubmitOrderAsync", $"Submitting {request.Side} order: {request.Quantity} {request.Symbol} @ {request.OrderType ?? "market"}");

            var tradeRequest = new ExtendedTradeRequest
            {
                Symbol = request.Symbol,
                Action = request.Side.ToLowerInvariant() == "buy" ? TradeAction.MarketBuy : TradeAction.MarketSell,
                Quantity = request.Quantity,
                OrderType = request.OrderType?.ToLowerInvariant() switch
                {
                    "limit" => OrderType.Limit,
                    "stop" => OrderType.Stop,
                    "stop_limit" => OrderType.StopLimit,
                    _ => OrderType.Market
                },
                LimitPrice = request.LimitPrice,
                StopPrice = request.StopPrice,
                TimeInForce = request.TimeInForce?.ToLowerInvariant() switch
                {
                    "gtc" => TimeInForce.GTC,
                    "ioc" => TimeInForce.IOC,
                    "fok" => TimeInForce.FOK,
                    _ => TimeInForce.Day
                }
            };

            var brokerOrder = await _broker.SubmitOrderAsync(tradeRequest).ConfigureAwait(false);
            App.LogError("DirectAlpacaOrderService.SubmitOrderAsync", $"Order submitted: ID={brokerOrder.OrderId}, Status={brokerOrder.Status}");

            // Track order locally
            var order = new MauiOrder
            {
                OrderId = brokerOrder.OrderId,
                Symbol = request.Symbol,
                Side = request.Side,
                OrderType = request.OrderType ?? "market",
                Quantity = request.Quantity,
                LimitPrice = request.LimitPrice,
                StopPrice = request.StopPrice,
                Status = MapBrokerStatus(brokerOrder.Status),
                SubmittedAt = DateTime.UtcNow
            };

            lock (_lock)
            {
                _orderHistory.Add(order);
            }

            return new OrderResult
            {
                Success = true,
                OrderId = brokerOrder.OrderId
            };
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaOrderService.SubmitOrderAsync", ex);
            return new OrderResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId)
    {
        try
        {
            var success = await _broker.CancelOrderAsync(orderId).ConfigureAwait(false);

            if (success)
            {
                lock (_lock)
                {
                    var order = _orderHistory.FirstOrDefault(o => o.OrderId == orderId);
                    if (order is not null)
                    {
                        order.Status = MauiOrderStatus.Cancelled;
                    }
                }
            }

            return success;
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaOrderService.CancelOrderAsync", ex);
            return false;
        }
    }

    public async Task<bool> ClosePositionAsync(string symbol)
    {
        try
        {
            await _broker.ClosePositionAsync(symbol).ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaOrderService.ClosePositionAsync", ex);
            return false;
        }
    }

    private static MauiOrderStatus MapBrokerStatus(BrokerOrderStatus status) => status switch
    {
        BrokerOrderStatus.New or BrokerOrderStatus.PendingNew => MauiOrderStatus.Pending,
        BrokerOrderStatus.PartiallyFilled => MauiOrderStatus.PartiallyFilled,
        BrokerOrderStatus.Filled => MauiOrderStatus.Filled,
        BrokerOrderStatus.Cancelled => MauiOrderStatus.Cancelled,
        BrokerOrderStatus.Rejected => MauiOrderStatus.Rejected,
        BrokerOrderStatus.Expired => MauiOrderStatus.Expired,
        _ => MauiOrderStatus.Pending
    };

    public void Dispose()
    {
        if (!_disposed)
        {
            _broker.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// In-memory alert service for POC without Supabase.
/// </summary>
public sealed class InMemoryAlertService : IAlertService
{
    private readonly List<Alert> _alerts = new();
    private readonly object _lock = new();
    private int _nextId = 1;

    public Task<List<Alert>> GetActiveAlertsAsync()
    {
        lock (_lock)
        {
            var active = _alerts.Where(a => a.IsActive).ToList();
            return Task.FromResult(active);
        }
    }

    public Task<List<Alert>> GetAllAlertsAsync()
    {
        lock (_lock)
        {
            return Task.FromResult(_alerts.ToList());
        }
    }

    public Task<string> CreateAlertAsync(string symbol, string condition, decimal targetPrice)
    {
        lock (_lock)
        {
            var id = _nextId++.ToString();
            var alert = new Alert
            {
                Id = id,
                Symbol = symbol.ToUpperInvariant(),
                Condition = condition,
                TargetPrice = targetPrice,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _alerts.Add(alert);
            return Task.FromResult(id);
        }
    }

    public Task UpdateAlertAsync(string alertId, bool isActive)
    {
        lock (_lock)
        {
            var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
            if (alert is not null)
            {
                alert.IsActive = isActive;
            }
        }
        return Task.CompletedTask;
    }

    public Task DeleteAlertAsync(string alertId)
    {
        lock (_lock)
        {
            _alerts.RemoveAll(a => a.Id == alertId);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Direct Alpaca broker connection service for POC.
/// </summary>
public sealed class DirectAlpacaBrokerConnectionService : IBrokerConnectionService
{
    private readonly AlpacaOptions _options;
    private bool _isConnected;

    public DirectAlpacaBrokerConnectionService(AlpacaOptions options)
    {
        _options = options;
        _isConnected = !string.IsNullOrEmpty(_options.ApiKey);
    }

    public async Task<bool> ConnectAsync(string broker, string apiKey, string apiSecret, bool isPaperTrading)
    {
        try
        {
            if (broker.ToLowerInvariant() != "alpaca")
            {
                return false;
            }

            // Validate by creating a test broker
            var testOptions = new AlpacaOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = isPaperTrading
            };

            using var testBroker = new AlpacaBroker(testOptions);
            var account = await testBroker.GetAccountAsync().ConfigureAwait(false);

            // Save credentials to secure storage
            await SecureStorage.SetAsync("alpaca_apikey", apiKey).ConfigureAwait(false);
            await SecureStorage.SetAsync("alpaca_apisecret", apiSecret).ConfigureAwait(false);
            Preferences.Set("alpaca_paper", isPaperTrading);

            _isConnected = true;
            return true;
        }
        catch (Exception ex)
        {
            App.LogException("DirectAlpacaBrokerConnectionService.ConnectAsync", ex);
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        SecureStorage.Remove("alpaca_apikey");
        SecureStorage.Remove("alpaca_apisecret");
        Preferences.Remove("alpaca_paper");
        _isConnected = false;
        return Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync()
    {
        return Task.FromResult(_isConnected);
    }

    public Task<string> GetConnectedBrokerAsync()
    {
        return Task.FromResult(_isConnected ? "Alpaca" : string.Empty);
    }
}
