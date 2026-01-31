using OoplesFinance.TradingApp.Maui.Models;
using OoplesFinance.TradingApp.Maui.Services;

namespace TradingApp.Tests.Mocks;

/// <summary>
/// Mock portfolio service for unit testing.
/// </summary>
public class MockPortfolioService : IPortfolioService
{
    private readonly List<Position> _positions = new();
    private AccountInfo _accountInfo = new()
    {
        PortfolioValue = 100000m,
        Cash = 25000m,
        BuyingPower = 50000m,
        TodayPnL = 500m,
        TotalPnL = 5000m
    };

    public void SetPositions(List<Position> positions) => _positions.Clear();
    public void SetAccountInfo(AccountInfo info) => _accountInfo = info;

    public Task<AccountInfo> GetAccountAsync() => Task.FromResult(_accountInfo);

    public Task<IReadOnlyList<Position>> GetPositionsAsync() =>
        Task.FromResult<IReadOnlyList<Position>>(_positions.AsReadOnly());

    public Task<Position?> GetPositionAsync(string symbol) =>
        Task.FromResult(_positions.FirstOrDefault(p => p.Symbol == symbol));

    public Task RefreshAsync() => Task.CompletedTask;
}

/// <summary>
/// Mock order service for unit testing.
/// </summary>
public class MockOrderService : IOrderService
{
    private readonly List<Order> _orders = new();
    private readonly List<Order> _orderHistory = new();
    private int _orderCounter = 1;

    public event EventHandler<Order>? OrderUpdated;

    public void AddOrder(Order order) => _orders.Add(order);
    public void ClearOrders() => _orders.Clear();

    public Task<Order> SubmitOrderAsync(OrderRequest request)
    {
        var order = new Order
        {
            OrderId = $"mock-order-{_orderCounter++}",
            Symbol = request.Symbol,
            Side = request.Side,
            Quantity = request.Quantity,
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            Status = OrderStatus.Pending,
            SubmittedAt = DateTime.UtcNow
        };

        _orders.Add(order);
        return Task.FromResult(order);
    }

    public Task<bool> CancelOrderAsync(string orderId)
    {
        var order = _orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order is not null)
        {
            order.Status = OrderStatus.Cancelled;
            _orders.Remove(order);
            _orderHistory.Add(order);
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<Order?> GetOrderAsync(string orderId) =>
        Task.FromResult(_orders.Concat(_orderHistory).FirstOrDefault(o => o.OrderId == orderId));

    public Task<IReadOnlyList<Order>> GetOpenOrdersAsync() =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.Where(o => o.Status == OrderStatus.Pending || o.Status == OrderStatus.PartiallyFilled).ToList().AsReadOnly());

    public Task<IReadOnlyList<Order>> GetOrderHistoryAsync(DateTime? from = null, DateTime? to = null) =>
        Task.FromResult<IReadOnlyList<Order>>(_orderHistory.AsReadOnly());

    public void SimulateFill(string orderId, decimal fillPrice)
    {
        var order = _orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order is not null)
        {
            order.Status = OrderStatus.Filled;
            order.FilledQuantity = order.Quantity;
            order.AverageFillPrice = fillPrice;
            order.FilledAt = DateTime.UtcNow;
            _orders.Remove(order);
            _orderHistory.Add(order);
            OrderUpdated?.Invoke(this, order);
        }
    }
}

/// <summary>
/// Mock market data service for unit testing.
/// </summary>
public class MockMarketDataService : IMarketDataService
{
    private readonly Dictionary<string, Quote> _quotes = new();
    private readonly Dictionary<string, List<Bar>> _bars = new();
    private MarketStatus _marketStatus = new() { IsOpen = true, NextOpen = DateTime.UtcNow.AddHours(8), NextClose = DateTime.UtcNow.AddHours(16) };

    public void SetQuote(string symbol, Quote quote) => _quotes[symbol] = quote;
    public void SetBars(string symbol, List<Bar> bars) => _bars[symbol] = bars;
    public void SetMarketStatus(MarketStatus status) => _marketStatus = status;

    public Task<Quote?> GetQuoteAsync(string symbol) =>
        Task.FromResult(_quotes.TryGetValue(symbol, out var quote) ? quote : null);

    public Task<IReadOnlyList<Quote>> GetQuotesAsync(IEnumerable<string> symbols) =>
        Task.FromResult<IReadOnlyList<Quote>>(symbols
            .Where(s => _quotes.ContainsKey(s))
            .Select(s => _quotes[s])
            .ToList()
            .AsReadOnly());

    public Task<IReadOnlyList<Bar>> GetHistoricalBarsAsync(string symbol, DateTime start, DateTime end, BarInterval interval)
    {
        if (_bars.TryGetValue(symbol, out var bars))
        {
            return Task.FromResult<IReadOnlyList<Bar>>(bars
                .Where(b => b.Timestamp >= start && b.Timestamp <= end)
                .ToList()
                .AsReadOnly());
        }
        return Task.FromResult<IReadOnlyList<Bar>>(Array.Empty<Bar>());
    }

    public Task<MarketStatus> GetMarketStatusAsync() => Task.FromResult(_marketStatus);

    public Task SubscribeToQuotesAsync(IEnumerable<string> symbols, Action<Quote> onQuote) => Task.CompletedTask;
    public Task UnsubscribeFromQuotesAsync(IEnumerable<string> symbols) => Task.CompletedTask;
}

/// <summary>
/// Mock alert service for unit testing.
/// </summary>
public class MockAlertService : IAlertService
{
    private readonly List<Alert> _alerts = new();
    private int _alertCounter = 1;

    public event EventHandler<Alert>? AlertTriggered;

    public void AddAlert(Alert alert) => _alerts.Add(alert);
    public void ClearAlerts() => _alerts.Clear();

    public Task<string> CreateAlertAsync(string symbol, string condition, decimal targetPrice)
    {
        var alert = new Alert
        {
            Id = $"alert-{_alertCounter++}",
            Symbol = symbol,
            Condition = condition,
            TargetPrice = targetPrice,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        _alerts.Add(alert);
        return Task.FromResult(alert.Id);
    }

    public Task DeleteAlertAsync(string alertId)
    {
        _alerts.RemoveAll(a => a.Id == alertId);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Alert>> GetAllAlertsAsync() =>
        Task.FromResult<IReadOnlyList<Alert>>(_alerts.AsReadOnly());

    public Task<IReadOnlyList<Alert>> GetActiveAlertsAsync() =>
        Task.FromResult<IReadOnlyList<Alert>>(_alerts.Where(a => a.IsActive).ToList().AsReadOnly());

    public Task UpdateAlertAsync(string alertId, bool isActive)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert is not null)
            alert.IsActive = isActive;
        return Task.CompletedTask;
    }

    public void SimulateTrigger(string alertId)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert is not null)
        {
            alert.IsTriggered = true;
            alert.TriggeredAt = DateTime.UtcNow;
            AlertTriggered?.Invoke(this, alert);
        }
    }
}

/// <summary>
/// Mock settings service for unit testing.
/// </summary>
public class MockSettingsService : ISettingsService
{
    private readonly List<string> _watchlist = new() { "AAPL", "MSFT", "GOOGL" };
    private readonly Dictionary<string, object> _settings = new();

    public void SetWatchlist(List<string> symbols)
    {
        _watchlist.Clear();
        _watchlist.AddRange(symbols);
    }

    public Task<IReadOnlyList<string>> GetWatchlistAsync() =>
        Task.FromResult<IReadOnlyList<string>>(_watchlist.AsReadOnly());

    public Task AddToWatchlistAsync(string symbol)
    {
        if (!_watchlist.Contains(symbol))
            _watchlist.Add(symbol);
        return Task.CompletedTask;
    }

    public Task RemoveFromWatchlistAsync(string symbol)
    {
        _watchlist.Remove(symbol);
        return Task.CompletedTask;
    }

    public Task<T?> GetSettingAsync<T>(string key) =>
        Task.FromResult(_settings.TryGetValue(key, out var value) ? (T)value : default);

    public Task SetSettingAsync<T>(string key, T value)
    {
        _settings[key] = value!;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Mock notification service for unit testing.
/// </summary>
public class MockNotificationService : INotificationService
{
    public List<(string Title, string Message)> SentNotifications { get; } = new();

    public Task ShowLocalNotificationAsync(string title, string message)
    {
        SentNotifications.Add((title, message));
        return Task.CompletedTask;
    }

    public Task ShowLocalNotificationAsync(string title, string message, Dictionary<string, string>? data)
    {
        SentNotifications.Add((title, message));
        return Task.CompletedTask;
    }

    public Task RequestPermissionAsync() => Task.CompletedTask;
    public Task<string?> RegisterAsync() => Task.FromResult<string?>("mock-device-token");
    public Task UnregisterAsync() => Task.CompletedTask;
    public Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime) => Task.CompletedTask;
    public Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime, Dictionary<string, string>? data) => Task.CompletedTask;
    public Task CancelAllScheduledNotificationsAsync() => Task.CompletedTask;
}
