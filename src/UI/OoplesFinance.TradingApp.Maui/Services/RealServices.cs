using System.Text.Json.Serialization;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Cloud;
using OoplesFinance.StockIndicators.Builder.Trading;
using OoplesFinance.StockIndicators.Builder.Trading.Brokers;
using OoplesFinance.StockIndicators.Builder.Trading.Brokers.InteractiveBrokers;
using OoplesFinance.StockIndicators.Builder.Trading.Brokers.Binance;
using OoplesFinance.StockIndicators.Builder.MarketData;
using OoplesFinance.TradingApp.Maui.Models;
// Type aliases to disambiguate between library and local models
using MauiQuote = OoplesFinance.TradingApp.Maui.Models.Quote;
using MauiBar = OoplesFinance.TradingApp.Maui.Models.Bar;
using MauiOrder = OoplesFinance.TradingApp.Maui.Models.Order;
using MauiOrderStatus = OoplesFinance.TradingApp.Maui.Models.OrderStatus;

namespace OoplesFinance.TradingApp.Maui.Services;

/// <summary>
/// Real portfolio service backed by Supabase and broker APIs.
/// </summary>
public sealed class RealPortfolioService : IPortfolioService
{
    private readonly SupabaseClient _supabase;
    private readonly IBrokerFactory _brokerFactory;
    private IBroker? _activeBroker;
    private string? _activeBrokerConnectionId;

    public RealPortfolioService(SupabaseClient supabase, IBrokerFactory brokerFactory)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _brokerFactory = brokerFactory ?? throw new ArgumentNullException(nameof(brokerFactory));
    }

    public async Task<AccountInfo> GetAccountAsync()
    {
        await EnsureBrokerConnectedAsync().ConfigureAwait(false);

        if (_activeBroker is null)
        {
            return new AccountInfo
            {
                PortfolioValue = 0,
                Cash = 0,
                BuyingPower = 0,
                TodayPnL = 0,
                TotalPnL = 0
            };
        }

        var brokerAccount = await _activeBroker.GetAccountAsync().ConfigureAwait(false);

        // Get positions for total PnL calculation
        var positions = await GetPositionsAsync().ConfigureAwait(false);
        var totalUnrealizedPnL = positions.Sum(p => p.UnrealizedPnL);
        var totalDayPnL = positions.Sum(p => p.DayPnL);

        return new AccountInfo
        {
            PortfolioValue = brokerAccount.Equity,
            Cash = brokerAccount.Cash,
            BuyingPower = brokerAccount.BuyingPower,
            TodayPnL = totalDayPnL,
            TotalPnL = totalUnrealizedPnL
        };
    }

    public async Task<List<Position>> GetPositionsAsync()
    {
        await EnsureBrokerConnectedAsync().ConfigureAwait(false);

        if (_activeBroker is null)
        {
            return new List<Position>();
        }

        var brokerPositions = await _activeBroker.GetPositionsAsync().ConfigureAwait(false);

        var positions = brokerPositions.Select(p => new Position
        {
            Symbol = p.Symbol,
            CompanyName = p.Symbol, // Would need market data lookup for real name
            Quantity = (int)p.Quantity,
            AveragePrice = p.AverageEntryPrice,
            CurrentPrice = p.CurrentPrice,
            MarketValue = p.CurrentPrice * p.Quantity,
            UnrealizedPnL = p.UnrealizedPnL,
            DayPnL = 0 // Would need previous close price
        }).ToList();

        // Sync positions to Supabase for persistence
        await SyncPositionsToSupabaseAsync(positions).ConfigureAwait(false);

        return positions;
    }

    private async Task EnsureBrokerConnectedAsync()
    {
        if (_activeBroker is not null)
            return;

        // Get active broker connection from Supabase
        var connections = await _supabase
            .From<BrokerConnectionRecord>("broker_connections")
            .Select("*")
            .Eq("is_active", true)
            .ExecuteAsync()
            .ConfigureAwait(false);

        var activeConnection = connections.FirstOrDefault();
        if (activeConnection is not null)
        {
            _activeBrokerConnectionId = activeConnection.Id;
            _activeBroker = await _brokerFactory.CreateBrokerAsync(
                activeConnection.Broker,
                activeConnection.AccountType == "paper"
            ).ConfigureAwait(false);
        }
    }

    private async Task SyncPositionsToSupabaseAsync(List<Position> positions)
    {
        if (string.IsNullOrEmpty(_activeBrokerConnectionId))
            return;

        foreach (var position in positions)
        {
            var record = new PositionRecord
            {
                UserId = _supabase.UserId ?? string.Empty,
                BrokerConnectionId = _activeBrokerConnectionId,
                Symbol = position.Symbol,
                Quantity = position.Quantity,
                AverageEntryPrice = position.AveragePrice,
                CurrentPrice = position.CurrentPrice,
                MarketValue = position.MarketValue,
                CostBasis = position.AveragePrice * position.Quantity,
                UnrealizedPnl = position.UnrealizedPnL,
                DayPnl = position.DayPnL
            };

            await _supabase.UpsertAsync("positions", record, "user_id,broker_connection_id,symbol").ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Real market data service backed by Alpaca or other providers.
/// </summary>
public sealed class RealMarketDataService : IMarketDataService
{
    private readonly IMarketDataProvider _marketDataProvider;
    private readonly Dictionary<string, MauiQuote> _quoteCache = new();
    private readonly TimeSpan _cacheExpiry = TimeSpan.FromSeconds(5);
    private DateTime _lastCacheUpdate = DateTime.MinValue;

    public RealMarketDataService(IMarketDataProvider marketDataProvider)
    {
        _marketDataProvider = marketDataProvider ?? throw new ArgumentNullException(nameof(marketDataProvider));
    }

    public async Task<MauiQuote?> GetQuoteAsync(string symbol)
    {
        // Check cache
        if (DateTime.UtcNow - _lastCacheUpdate < _cacheExpiry &&
            _quoteCache.TryGetValue(symbol.ToUpperInvariant(), out var cached))
        {
            return cached;
        }

        // Get snapshot for both current quote and previous close
        var snapshot = await _marketDataProvider.GetSnapshotAsync(symbol).ConfigureAwait(false);
        if (snapshot is null)
            return null;

        var currentPrice = snapshot.CurrentPrice;
        var previousClose = snapshot.PreviousBar?.Close ?? currentPrice;
        var change = currentPrice - previousClose;
        var changePercent = previousClose > 0 ? change / previousClose : 0m;

        var quote = new MauiQuote
        {
            Symbol = symbol.ToUpperInvariant(),
            CompanyName = symbol.ToUpperInvariant(), // Would need company info lookup
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

        _quoteCache[symbol.ToUpperInvariant()] = quote;
        _lastCacheUpdate = DateTime.UtcNow;

        return quote;
    }

    public Task<MarketStatus> GetMarketStatusAsync()
    {
        // US market hours (simplified)
        var now = DateTime.Now;
        var estNow = TimeZoneInfo.ConvertTime(now, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));

        var isWeekday = estNow.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
        var marketOpen = new TimeSpan(9, 30, 0);
        var marketClose = new TimeSpan(16, 0, 0);
        var isMarketHours = estNow.TimeOfDay >= marketOpen && estNow.TimeOfDay <= marketClose;

        var isOpen = isWeekday && isMarketHours;

        // Calculate next open and next close
        var nextOpen = isOpen ? (DateTime?)null : GetNextMarketOpen(estNow);
        var nextClose = isOpen ? estNow.Date + marketClose : (DateTime?)null;

        return Task.FromResult(new MarketStatus
        {
            IsOpen = isOpen,
            NextOpen = nextOpen,
            NextClose = nextClose
        });
    }

    public async Task<List<MauiBar>> GetHistoricalBarsAsync(string symbol, string timeframe, int count)
    {
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

        var marketBars = await _marketDataProvider.GetHistoricalBarsAsync(symbol, startDate, endDate, interval).ConfigureAwait(false);

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

    private static DateTime GetNextMarketOpen(DateTime from)
    {
        var next = from.Date.AddDays(1).AddHours(9).AddMinutes(30);

        // Skip weekends
        while (next.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            next = next.AddDays(1);
        }

        return next;
    }
}

/// <summary>
/// Real order service backed by Supabase and broker APIs.
/// </summary>
public sealed class RealOrderService : IOrderService
{
    private readonly SupabaseClient _supabase;
    private readonly IBrokerFactory _brokerFactory;
    private IBroker? _activeBroker;
    private string? _activeBrokerConnectionId;

    public RealOrderService(SupabaseClient supabase, IBrokerFactory brokerFactory)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _brokerFactory = brokerFactory ?? throw new ArgumentNullException(nameof(brokerFactory));
    }

    public async Task<List<MauiOrder>> GetOpenOrdersAsync()
    {
        // Get from Supabase (synced from broker)
        var orders = await _supabase
            .From<OrderRecord>("orders")
            .Select("*")
            .In("status", new object[] { "new", "pending_new", "accepted", "partial" })
            .Order("created_at", false)
            .ExecuteAsync()
            .ConfigureAwait(false);

        return orders.Select(MapToOrder).ToList();
    }

    public async Task<List<MauiOrder>> GetRecentOrdersAsync(int count)
    {
        var orders = await _supabase
            .From<OrderRecord>("orders")
            .Select("*")
            .Order("created_at", false)
            .Limit(count)
            .ExecuteAsync()
            .ConfigureAwait(false);

        return orders.Select(MapToOrder).ToList();
    }

    public async Task<OrderResult> SubmitOrderAsync(OrderRequest request)
    {
        await EnsureBrokerConnectedAsync().ConfigureAwait(false);

        if (_activeBroker is null)
        {
            return new OrderResult { Success = false, Message = "No broker connected" };
        }

        try
        {
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

            var brokerOrder = await _activeBroker.SubmitOrderAsync(tradeRequest).ConfigureAwait(false);

            // Save to Supabase
            var orderRecord = new OrderRecord
            {
                UserId = _supabase.UserId ?? string.Empty,
                BrokerConnectionId = _activeBrokerConnectionId ?? string.Empty,
                BrokerOrderId = brokerOrder.OrderId,
                Symbol = request.Symbol,
                Side = request.Side.ToLowerInvariant(),
                OrderType = request.OrderType ?? "market",
                TimeInForce = request.TimeInForce ?? "day",
                Quantity = request.Quantity,
                LimitPrice = request.LimitPrice,
                StopPrice = request.StopPrice,
                Status = brokerOrder.Status.ToString().ToLowerInvariant(),
                CreatedAt = DateTime.UtcNow,
                SubmittedAt = DateTime.UtcNow
            };

            await _supabase.InsertAsync("orders", orderRecord).ConfigureAwait(false);

            return new OrderResult
            {
                Success = true,
                OrderId = brokerOrder.OrderId
            };
        }
        catch (Exception ex)
        {
            return new OrderResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }

    public async Task<bool> CancelOrderAsync(string orderId)
    {
        await EnsureBrokerConnectedAsync().ConfigureAwait(false);

        if (_activeBroker is null)
            return false;

        var success = await _activeBroker.CancelOrderAsync(orderId).ConfigureAwait(false);

        if (success)
        {
            // Update status in Supabase
            await _supabase.UpdateAsync<OrderRecord>(
                "orders",
                new { status = "cancelled", cancelled_at = DateTime.UtcNow },
                $"broker_order_id=eq.{orderId}"
            ).ConfigureAwait(false);
        }

        return success;
    }

    public async Task<bool> ClosePositionAsync(string symbol)
    {
        await EnsureBrokerConnectedAsync().ConfigureAwait(false);

        if (_activeBroker is null)
            return false;

        try
        {
            await _activeBroker.ClosePositionAsync(symbol).ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task EnsureBrokerConnectedAsync()
    {
        if (_activeBroker is not null)
            return;

        var connections = await _supabase
            .From<BrokerConnectionRecord>("broker_connections")
            .Select("*")
            .Eq("is_active", true)
            .ExecuteAsync()
            .ConfigureAwait(false);

        var activeConnection = connections.FirstOrDefault();
        if (activeConnection is not null)
        {
            _activeBrokerConnectionId = activeConnection.Id;
            _activeBroker = await _brokerFactory.CreateBrokerAsync(
                activeConnection.Broker,
                activeConnection.AccountType == "paper"
            ).ConfigureAwait(false);
        }
    }

    private static MauiOrder MapToOrder(OrderRecord record) => new()
    {
        OrderId = record.BrokerOrderId,
        Symbol = record.Symbol,
        Side = record.Side,
        OrderType = record.OrderType,
        Quantity = record.Quantity,
        FilledQuantity = record.FilledQuantity,
        LimitPrice = record.LimitPrice,
        StopPrice = record.StopPrice,
        AverageFillPrice = record.AverageFillPrice,
        Status = ParseOrderStatus(record.Status),
        SubmittedAt = record.SubmittedAt ?? record.CreatedAt,
        FilledAt = record.FilledAt
    };

    private static MauiOrderStatus ParseOrderStatus(string status) => status.ToLowerInvariant() switch
    {
        "new" or "pending_new" or "accepted" => MauiOrderStatus.Pending,
        "partial" or "partially_filled" => MauiOrderStatus.PartiallyFilled,
        "filled" => MauiOrderStatus.Filled,
        "cancelled" or "canceled" => MauiOrderStatus.Cancelled,
        "rejected" => MauiOrderStatus.Rejected,
        "expired" => MauiOrderStatus.Expired,
        _ => MauiOrderStatus.Pending
    };
}

/// <summary>
/// Real alert service backed by Supabase with real-time subscriptions.
/// </summary>
public sealed class RealAlertService : IAlertService
{
    private readonly SupabaseClient _supabase;

    public RealAlertService(SupabaseClient supabase)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
    }

    public async Task<List<Alert>> GetActiveAlertsAsync()
    {
        var alerts = await _supabase
            .From<PriceAlertRecord>("price_alerts")
            .Select("*")
            .Eq("is_active", true)
            .Order("created_at", false)
            .ExecuteAsync()
            .ConfigureAwait(false);

        return alerts.Select(MapToAlert).ToList();
    }

    public async Task<List<Alert>> GetAllAlertsAsync()
    {
        var alerts = await _supabase
            .From<PriceAlertRecord>("price_alerts")
            .Select("*")
            .Order("created_at", false)
            .ExecuteAsync()
            .ConfigureAwait(false);

        return alerts.Select(MapToAlert).ToList();
    }

    public async Task<string> CreateAlertAsync(string symbol, string condition, decimal targetPrice)
    {
        var record = new PriceAlertRecord
        {
            UserId = _supabase.UserId ?? string.Empty,
            Symbol = symbol.ToUpperInvariant(),
            Condition = condition.ToLowerInvariant().Replace(" ", "_"),
            TargetPrice = targetPrice,
            IsActive = true,
            NotifyPush = true,
            CreatedAt = DateTime.UtcNow
        };

        var inserted = await _supabase.InsertAsync("price_alerts", record).ConfigureAwait(false);
        return inserted?.Id ?? Guid.NewGuid().ToString();
    }

    public async Task UpdateAlertAsync(string alertId, bool isActive)
    {
        await _supabase.UpdateAsync<PriceAlertRecord>(
            "price_alerts",
            new { is_active = isActive },
            $"id=eq.{alertId}"
        ).ConfigureAwait(false);
    }

    public async Task DeleteAlertAsync(string alertId)
    {
        await _supabase.DeleteAsync("price_alerts", $"id=eq.{alertId}").ConfigureAwait(false);
    }

    private static Alert MapToAlert(PriceAlertRecord record) => new()
    {
        Id = record.Id,
        Symbol = record.Symbol,
        Condition = record.Condition.Replace("_", " "),
        TargetPrice = record.TargetPrice ?? 0,
        IsActive = record.IsActive,
        CreatedAt = record.CreatedAt
    };
}

/// <summary>
/// Real broker connection service backed by Supabase.
/// </summary>
public sealed class RealBrokerConnectionService : IBrokerConnectionService
{
    private readonly SupabaseClient _supabase;
    private readonly IBrokerFactory _brokerFactory;
    private bool _isConnected;
    private string _connectedBroker = string.Empty;

    public RealBrokerConnectionService(SupabaseClient supabase, IBrokerFactory brokerFactory)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
        _brokerFactory = brokerFactory ?? throw new ArgumentNullException(nameof(brokerFactory));
    }

    public async Task<bool> ConnectAsync(string broker, string apiKey, string apiSecret, bool isPaperTrading)
    {
        try
        {
            // Validate credentials by creating and testing broker connection
            var testBroker = await _brokerFactory.CreateBrokerAsync(broker, isPaperTrading, apiKey, apiSecret).ConfigureAwait(false);
            var account = await testBroker.GetAccountAsync().ConfigureAwait(false);

            // Save to Supabase (credentials should be encrypted)
            var record = new BrokerConnectionRecord
            {
                UserId = _supabase.UserId ?? string.Empty,
                Broker = broker.ToLowerInvariant(),
                AccountId = account.AccountId,
                AccountType = isPaperTrading ? "paper" : "live",
                IsActive = true,
                LastSyncAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            // Deactivate other connections first
            await _supabase.UpdateAsync<BrokerConnectionRecord>(
                "broker_connections",
                new { is_active = false },
                $"user_id=eq.{_supabase.UserId}"
            ).ConfigureAwait(false);

            await _supabase.InsertAsync("broker_connections", record).ConfigureAwait(false);

            // Store credentials securely on device
            await SecureStorage.SetAsync($"broker_{broker}_apikey", apiKey).ConfigureAwait(false);
            await SecureStorage.SetAsync($"broker_{broker}_apisecret", apiSecret).ConfigureAwait(false);

            _isConnected = true;
            _connectedBroker = broker;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        if (!string.IsNullOrEmpty(_connectedBroker))
        {
            SecureStorage.Remove($"broker_{_connectedBroker}_apikey");
            SecureStorage.Remove($"broker_{_connectedBroker}_apisecret");
        }

        await _supabase.UpdateAsync<BrokerConnectionRecord>(
            "broker_connections",
            new { is_active = false },
            $"user_id=eq.{_supabase.UserId}"
        ).ConfigureAwait(false);

        _isConnected = false;
        _connectedBroker = string.Empty;
    }

    public async Task<bool> IsConnectedAsync()
    {
        if (_isConnected)
            return true;

        // Check Supabase for active connection
        var connections = await _supabase
            .From<BrokerConnectionRecord>("broker_connections")
            .Select("*")
            .Eq("is_active", true)
            .ExecuteAsync()
            .ConfigureAwait(false);

        if (connections.Any())
        {
            _isConnected = true;
            _connectedBroker = connections.First().Broker;
        }

        return _isConnected;
    }

    public async Task<string> GetConnectedBrokerAsync()
    {
        await IsConnectedAsync().ConfigureAwait(false);
        return _connectedBroker;
    }
}

/// <summary>
/// Factory for creating broker instances.
/// </summary>
public interface IBrokerFactory
{
    Task<IBroker> CreateBrokerAsync(string broker, bool isPaper, string? apiKey = null, string? apiSecret = null);
}

/// <summary>
/// Default broker factory implementation.
/// </summary>
public sealed class BrokerFactory : IBrokerFactory
{
    public async Task<IBroker> CreateBrokerAsync(string broker, bool isPaper, string? apiKey = null, string? apiSecret = null)
    {
        // Get credentials from secure storage if not provided
        apiKey ??= await SecureStorage.GetAsync($"broker_{broker}_apikey").ConfigureAwait(false);
        apiSecret ??= await SecureStorage.GetAsync($"broker_{broker}_apisecret").ConfigureAwait(false);

        return broker.ToLowerInvariant() switch
        {
            "alpaca" => new AlpacaBroker(new AlpacaOptions
            {
                ApiKey = apiKey,
                ApiSecret = apiSecret,
                UsePaper = isPaper
            }),
            "interactive_brokers" or "ib" => new IBBroker(new IBOptions
            {
                ClientId = 1,
                UsePaperTrading = isPaper
            }),
            "binance" => new BinanceBroker(new BinanceOptions
            {
                ApiKey = apiKey ?? string.Empty,
                ApiSecret = apiSecret ?? string.Empty,
                UseTestnet = isPaper
            }),
            _ => throw new NotSupportedException($"Broker not supported: {broker}")
        };
    }
}

#region Database Record Types

internal sealed class BrokerConnectionRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("broker")]
    public string Broker { get; set; } = string.Empty;

    [JsonPropertyName("account_id")]
    public string AccountId { get; set; } = string.Empty;

    [JsonPropertyName("account_type")]
    public string AccountType { get; set; } = "paper";

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("last_sync_at")]
    public DateTime? LastSyncAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

internal sealed class PositionRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("broker_connection_id")]
    public string BrokerConnectionId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("average_entry_price")]
    public decimal AverageEntryPrice { get; set; }

    [JsonPropertyName("current_price")]
    public decimal? CurrentPrice { get; set; }

    [JsonPropertyName("market_value")]
    public decimal? MarketValue { get; set; }

    [JsonPropertyName("cost_basis")]
    public decimal CostBasis { get; set; }

    [JsonPropertyName("unrealized_pnl")]
    public decimal? UnrealizedPnl { get; set; }

    [JsonPropertyName("day_pnl")]
    public decimal? DayPnl { get; set; }
}

internal sealed class OrderRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("broker_connection_id")]
    public string BrokerConnectionId { get; set; } = string.Empty;

    [JsonPropertyName("broker_order_id")]
    public string BrokerOrderId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("side")]
    public string Side { get; set; } = string.Empty;

    [JsonPropertyName("order_type")]
    public string OrderType { get; set; } = "market";

    [JsonPropertyName("time_in_force")]
    public string TimeInForce { get; set; } = "day";

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("filled_quantity")]
    public decimal FilledQuantity { get; set; }

    [JsonPropertyName("limit_price")]
    public decimal? LimitPrice { get; set; }

    [JsonPropertyName("stop_price")]
    public decimal? StopPrice { get; set; }

    [JsonPropertyName("average_fill_price")]
    public decimal? AverageFillPrice { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "new";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    [JsonPropertyName("submitted_at")]
    public DateTime? SubmittedAt { get; set; }

    [JsonPropertyName("filled_at")]
    public DateTime? FilledAt { get; set; }

    [JsonPropertyName("cancelled_at")]
    public DateTime? CancelledAt { get; set; }
}

internal sealed class PriceAlertRecord
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("symbol")]
    public string Symbol { get; set; } = string.Empty;

    [JsonPropertyName("condition")]
    public string Condition { get; set; } = string.Empty;

    [JsonPropertyName("target_price")]
    public decimal? TargetPrice { get; set; }

    [JsonPropertyName("is_active")]
    public bool IsActive { get; set; }

    [JsonPropertyName("is_triggered")]
    public bool IsTriggered { get; set; }

    [JsonPropertyName("notify_push")]
    public bool NotifyPush { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }
}

#endregion
