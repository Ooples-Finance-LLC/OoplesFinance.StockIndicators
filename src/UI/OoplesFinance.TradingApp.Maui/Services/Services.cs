using OoplesFinance.TradingApp.Maui.Models;

namespace OoplesFinance.TradingApp.Maui.Services;

#region Service Interfaces

public interface ISettingsService
{
    Task<string> GetThemeAsync();
    Task SetThemeAsync(string theme);
    Task<List<string>> GetWatchlistAsync();
    Task AddToWatchlistAsync(string symbol);
    Task RemoveFromWatchlistAsync(string symbol);
    Task<bool> GetBiometricEnabledAsync();
    Task SetBiometricEnabledAsync(bool enabled);
    Task<bool> GetPushNotificationsEnabledAsync();
    Task SetPushNotificationsEnabledAsync(bool enabled);
}

public interface IBrokerConnectionService
{
    Task<bool> ConnectAsync(string broker, string apiKey, string apiSecret, bool isPaperTrading);
    Task DisconnectAsync();
    Task<bool> IsConnectedAsync();
    Task<string> GetConnectedBrokerAsync();
}

public interface IPortfolioService
{
    Task<AccountInfo> GetAccountAsync();
    Task<List<Position>> GetPositionsAsync();
}

public interface IMarketDataService
{
    Task<Quote?> GetQuoteAsync(string symbol);
    Task<MarketStatus> GetMarketStatusAsync();
    Task<List<Bar>> GetHistoricalBarsAsync(string symbol, string timeframe, int count);
}

public interface IOrderService
{
    Task<List<Order>> GetOpenOrdersAsync();
    Task<List<Order>> GetRecentOrdersAsync(int count);
    Task<OrderResult> SubmitOrderAsync(OrderRequest request);
    Task<bool> CancelOrderAsync(string orderId);
    Task<bool> ClosePositionAsync(string symbol);
}

public interface IAlertService
{
    Task<List<Alert>> GetActiveAlertsAsync();
    Task<List<Alert>> GetAllAlertsAsync();
    Task<string> CreateAlertAsync(string symbol, string condition, decimal targetPrice);
    Task UpdateAlertAsync(string alertId, bool isActive);
    Task DeleteAlertAsync(string alertId);
}

public interface INotificationService
{
    Task RequestPermissionAsync();
    Task ShowLocalNotificationAsync(string title, string message);
    Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime);
}

public interface IStrategyMonitorService
{
    Task<List<StrategyStatus>> GetActiveStrategiesAsync();
    Task<bool> StartStrategyAsync(string strategyId);
    Task<bool> StopStrategyAsync(string strategyId);
}

/// <summary>
/// AI-powered analysis service for trading signals and market insights.
/// Leverages ML components like RegimeDetector, PriceAnomalyDetector, and StrategySelector.
/// </summary>
public interface IAIAnalysisService
{
    /// <summary>
    /// Generates AI trading signals based on technical indicators and market analysis.
    /// </summary>
    Task<AITradingSignal> GenerateSignalAsync(string symbol, List<Bar> bars);

    /// <summary>
    /// Analyzes the current market regime.
    /// </summary>
    MarketRegimeAnalysis AnalyzeMarketRegime(List<Bar> bars);

    /// <summary>
    /// Detects price anomalies in the data.
    /// </summary>
    List<DetectedAnomaly> DetectAnomalies(List<Bar> bars);

    /// <summary>
    /// Gets AI-powered trading recommendations.
    /// </summary>
    Task<AIRecommendation> GetRecommendationAsync(string symbol, List<Bar> bars, IndicatorResults indicators);

    /// <summary>
    /// Calculates a confidence score for a potential trade.
    /// </summary>
    double CalculateTradeConfidence(List<Bar> bars, IndicatorResults indicators, TradeSide side);
}

/// <summary>
/// AI trading signal with confidence and reasoning.
/// </summary>
public sealed class AITradingSignal
{
    public string Symbol { get; set; } = string.Empty;
    public SignalType Signal { get; set; }
    public double Confidence { get; set; }
    public string Reasoning { get; set; } = string.Empty;
    public decimal SuggestedEntryPrice { get; set; }
    public decimal SuggestedStopLoss { get; set; }
    public decimal SuggestedTakeProfit { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<string> IndicatorSignals { get; set; } = new();
    public MarketRegimeAnalysis RegimeAnalysis { get; set; } = new();
}

/// <summary>
/// Signal type enumeration.
/// </summary>
public enum SignalType
{
    StrongBuy,
    Buy,
    Hold,
    Sell,
    StrongSell
}

/// <summary>
/// Trade side enumeration.
/// </summary>
public enum TradeSide
{
    Buy,
    Sell
}

/// <summary>
/// Market regime analysis result for the app.
/// </summary>
public sealed class MarketRegimeAnalysis
{
    public string Regime { get; set; } = "Unknown";
    public string Trend { get; set; } = "Unknown";
    public string Volatility { get; set; } = "Normal";
    public double Confidence { get; set; }
    public double Momentum { get; set; }
    public double TrendStrength { get; set; }
    public Dictionary<string, double> RegimeProbabilities { get; set; } = new();
}

/// <summary>
/// Detected anomaly for the app.
/// </summary>
public sealed class DetectedAnomaly
{
    public int Index { get; set; }
    public DateTime Timestamp { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public double Score { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

/// <summary>
/// AI-powered trading recommendation.
/// </summary>
public sealed class AIRecommendation
{
    public string Symbol { get; set; } = string.Empty;
    public string Action { get; set; } = "Hold";
    public double Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<string> BullishFactors { get; set; } = new();
    public List<string> BearishFactors { get; set; } = new();
    public decimal RiskLevel { get; set; }
    public string TimeHorizon { get; set; } = "Short-term";
    public decimal SuggestedPositionSize { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Indicator calculation service using v2 Builder API for high-performance computations.
/// </summary>
public interface IIndicatorService
{
    /// <summary>
    /// Calculates all common indicators for the given price bars.
    /// Uses the v2 Builder API with facade pattern for optimal performance.
    /// </summary>
    IndicatorResults CalculateIndicators(List<Bar> bars, IndicatorSettings? settings = null);

    /// <summary>
    /// Calculates a specific indicator.
    /// </summary>
    List<double> CalculateSma(List<Bar> bars, int length);
    List<double> CalculateEma(List<Bar> bars, int length);
    RsiResult CalculateRsi(List<Bar> bars, int length = 14);
    MacdIndicatorResult CalculateMacd(List<Bar> bars, int fastLength = 12, int slowLength = 26, int signalLength = 9);
    BollingerBandsIndicatorResult CalculateBollingerBands(List<Bar> bars, int length = 20, double stdDevMult = 2);
    StochasticIndicatorResult CalculateStochastic(List<Bar> bars, int kLength = 14, int dLength = 3);
    List<double> CalculateAtr(List<Bar> bars, int length = 14);
}

/// <summary>
/// Settings for indicator calculations.
/// </summary>
public sealed class IndicatorSettings
{
    public int SmaLength { get; set; } = 20;
    public int EmaLength { get; set; } = 20;
    public int RsiLength { get; set; } = 14;
    public int MacdFastLength { get; set; } = 12;
    public int MacdSlowLength { get; set; } = 26;
    public int MacdSignalLength { get; set; } = 9;
    public int BollingerLength { get; set; } = 20;
    public double BollingerStdDev { get; set; } = 2;
    public int StochasticKLength { get; set; } = 14;
    public int StochasticDLength { get; set; } = 3;
    public int AtrLength { get; set; } = 14;
}

/// <summary>
/// Results from indicator calculations.
/// </summary>
public sealed class IndicatorResults
{
    public List<double> Sma { get; set; } = new();
    public List<double> Ema { get; set; } = new();
    public RsiResult Rsi { get; set; } = new();
    public MacdIndicatorResult Macd { get; set; } = new();
    public BollingerBandsIndicatorResult BollingerBands { get; set; } = new();
    public StochasticIndicatorResult Stochastic { get; set; } = new();
    public List<double> Atr { get; set; } = new();
    public int DataCount { get; set; }
}

/// <summary>
/// RSI indicator result.
/// </summary>
public sealed class RsiResult
{
    public List<double> Values { get; set; } = new();
    public List<double> Signal { get; set; } = new();
}

/// <summary>
/// MACD indicator result.
/// </summary>
public sealed class MacdIndicatorResult
{
    public List<double> MacdLine { get; set; } = new();
    public List<double> SignalLine { get; set; } = new();
    public List<double> Histogram { get; set; } = new();
}

/// <summary>
/// Bollinger Bands indicator result.
/// </summary>
public sealed class BollingerBandsIndicatorResult
{
    public List<double> Upper { get; set; } = new();
    public List<double> Middle { get; set; } = new();
    public List<double> Lower { get; set; } = new();
}

/// <summary>
/// Stochastic indicator result.
/// </summary>
public sealed class StochasticIndicatorResult
{
    public List<double> K { get; set; } = new();
    public List<double> D { get; set; } = new();
}

#endregion

#region Service Implementations

public class SettingsService : ISettingsService
{
    public Task<string> GetThemeAsync() =>
        Task.FromResult(Preferences.Get("theme", "Dark"));

    public Task SetThemeAsync(string theme)
    {
        Preferences.Set("theme", theme);
        return Task.CompletedTask;
    }

    public Task<List<string>> GetWatchlistAsync()
    {
        var json = Preferences.Get("watchlist", "[]");
        var list = System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new();

        // Seed with defaults if empty
        if (list.Count == 0)
        {
            list = new List<string> { "AAPL", "MSFT", "GOOGL", "AMZN", "NVDA" };
            Preferences.Set("watchlist", System.Text.Json.JsonSerializer.Serialize(list));
        }

        return Task.FromResult(list);
    }

    public async Task AddToWatchlistAsync(string symbol)
    {
        var list = await GetWatchlistAsync();
        if (!list.Contains(symbol.ToUpperInvariant()))
        {
            list.Add(symbol.ToUpperInvariant());
            Preferences.Set("watchlist", System.Text.Json.JsonSerializer.Serialize(list));
        }
    }

    public async Task RemoveFromWatchlistAsync(string symbol)
    {
        var list = await GetWatchlistAsync();
        list.Remove(symbol.ToUpperInvariant());
        Preferences.Set("watchlist", System.Text.Json.JsonSerializer.Serialize(list));
    }

    public Task<bool> GetBiometricEnabledAsync() =>
        Task.FromResult(Preferences.Get("biometric", false));

    public Task SetBiometricEnabledAsync(bool enabled)
    {
        Preferences.Set("biometric", enabled);
        return Task.CompletedTask;
    }

    public Task<bool> GetPushNotificationsEnabledAsync() =>
        Task.FromResult(Preferences.Get("pushNotifications", true));

    public Task SetPushNotificationsEnabledAsync(bool enabled)
    {
        Preferences.Set("pushNotifications", enabled);
        return Task.CompletedTask;
    }
}

public class BrokerConnectionService : IBrokerConnectionService
{
    private bool _isConnected;
    private string _connectedBroker = string.Empty;

    public async Task<bool> ConnectAsync(string broker, string apiKey, string apiSecret, bool isPaperTrading)
    {
        // In production, this would connect to the actual broker API
        await Task.Delay(1000); // Simulate API call

        if (!string.IsNullOrEmpty(apiKey) && !string.IsNullOrEmpty(apiSecret))
        {
            _isConnected = true;
            _connectedBroker = broker;
            await SecureStorage.SetAsync("broker", broker);
            await SecureStorage.SetAsync("apiKey", apiKey);
            await SecureStorage.SetAsync("apiSecret", apiSecret);
            await SecureStorage.SetAsync("paperTrading", isPaperTrading.ToString());
            return true;
        }
        return false;
    }

    public async Task DisconnectAsync()
    {
        _isConnected = false;
        _connectedBroker = string.Empty;
        SecureStorage.Remove("broker");
        SecureStorage.Remove("apiKey");
        SecureStorage.Remove("apiSecret");
        await Task.CompletedTask;
    }

    public Task<bool> IsConnectedAsync() => Task.FromResult(_isConnected);
    public Task<string> GetConnectedBrokerAsync() => Task.FromResult(_connectedBroker);
}

public class PortfolioService : IPortfolioService
{
    public Task<AccountInfo> GetAccountAsync()
    {
        // Sample data - in production would call broker API
        return Task.FromResult(new AccountInfo
        {
            PortfolioValue = 125000m,
            Cash = 25000m,
            BuyingPower = 50000m,
            TodayPnL = 1250m,
            TotalPnL = 25000m
        });
    }

    public Task<List<Position>> GetPositionsAsync()
    {
        // Sample data
        return Task.FromResult(new List<Position>
        {
            new() { Symbol = "AAPL", CompanyName = "Apple Inc.", Quantity = 100, AveragePrice = 150m, CurrentPrice = 175m, MarketValue = 17500m, UnrealizedPnL = 2500m, DayPnL = 150m },
            new() { Symbol = "MSFT", CompanyName = "Microsoft Corp.", Quantity = 50, AveragePrice = 300m, CurrentPrice = 380m, MarketValue = 19000m, UnrealizedPnL = 4000m, DayPnL = 200m },
            new() { Symbol = "GOOGL", CompanyName = "Alphabet Inc.", Quantity = 25, AveragePrice = 140m, CurrentPrice = 155m, MarketValue = 3875m, UnrealizedPnL = 375m, DayPnL = 50m },
            new() { Symbol = "NVDA", CompanyName = "NVIDIA Corp.", Quantity = 30, AveragePrice = 500m, CurrentPrice = 850m, MarketValue = 25500m, UnrealizedPnL = 10500m, DayPnL = 450m },
            new() { Symbol = "TSLA", CompanyName = "Tesla Inc.", Quantity = 40, AveragePrice = 250m, CurrentPrice = 220m, MarketValue = 8800m, UnrealizedPnL = -1200m, DayPnL = -80m }
        });
    }
}

public class MarketDataService : IMarketDataService
{
    public Task<Quote?> GetQuoteAsync(string symbol)
    {
        // Sample data
        var quotes = new Dictionary<string, Quote>
        {
            ["AAPL"] = new() { Symbol = "AAPL", CompanyName = "Apple Inc.", LastPrice = 175m, Change = 2.50m, ChangePercent = 0.0145m },
            ["MSFT"] = new() { Symbol = "MSFT", CompanyName = "Microsoft Corp.", LastPrice = 380m, Change = 5.00m, ChangePercent = 0.0133m },
            ["GOOGL"] = new() { Symbol = "GOOGL", CompanyName = "Alphabet Inc.", LastPrice = 155m, Change = -1.50m, ChangePercent = -0.0096m },
            ["NVDA"] = new() { Symbol = "NVDA", CompanyName = "NVIDIA Corp.", LastPrice = 850m, Change = 15.00m, ChangePercent = 0.0180m },
            ["TSLA"] = new() { Symbol = "TSLA", CompanyName = "Tesla Inc.", LastPrice = 220m, Change = -5.00m, ChangePercent = -0.0222m }
        };

        quotes.TryGetValue(symbol.ToUpperInvariant(), out var quote);
        return Task.FromResult(quote);
    }

    public Task<MarketStatus> GetMarketStatusAsync()
    {
        var now = DateTime.Now;
        var isOpen = now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)
            && now.TimeOfDay >= new TimeSpan(9, 30, 0)
            && now.TimeOfDay <= new TimeSpan(16, 0, 0);

        var nextChange = isOpen
            ? now.Date.AddHours(16)
            : now.Date.AddDays(now.DayOfWeek == DayOfWeek.Friday ? 3 : 1).AddHours(9).AddMinutes(30);

        return Task.FromResult(new MarketStatus
        {
            IsOpen = isOpen,
            NextOpen = isOpen ? null : nextChange,
            NextClose = isOpen ? nextChange : null
        });
    }

    public Task<List<Bar>> GetHistoricalBarsAsync(string symbol, string timeframe, int count)
    {
        // Sample data
        var bars = new List<Bar>();
        var basePrice = 150m;
        var date = DateTime.Now.AddDays(-count);

        for (int i = 0; i < count; i++)
        {
            var change = (decimal)(new Random().NextDouble() - 0.5) * 5;
            bars.Add(new Bar
            {
                Timestamp = date.AddDays(i),
                Open = basePrice,
                High = basePrice + Math.Abs(change),
                Low = basePrice - Math.Abs(change),
                Close = basePrice + change,
                Volume = 1000000 + new Random().Next(500000)
            });
            basePrice += change;
        }

        return Task.FromResult(bars);
    }
}

public class OrderService : IOrderService
{
    private readonly List<Order> _orders = new();

    public Task<List<Order>> GetOpenOrdersAsync()
    {
        return Task.FromResult(_orders.Where(o => o.Status is OrderStatus.Pending or OrderStatus.PartiallyFilled).ToList());
    }

    public Task<List<Order>> GetRecentOrdersAsync(int count)
    {
        // Sample data
        return Task.FromResult(new List<Order>
        {
            new() { OrderId = "1", Symbol = "AAPL", Side = "buy", Quantity = 10, OrderType = "market", LimitPrice = 175m, AverageFillPrice = 175m, FilledQuantity = 10, Status = OrderStatus.Filled, SubmittedAt = DateTime.Now.AddHours(-1), FilledAt = DateTime.Now.AddHours(-1) },
            new() { OrderId = "2", Symbol = "MSFT", Side = "sell", Quantity = 5, OrderType = "market", LimitPrice = 380m, AverageFillPrice = 380m, FilledQuantity = 5, Status = OrderStatus.Filled, SubmittedAt = DateTime.Now.AddHours(-3), FilledAt = DateTime.Now.AddHours(-3) },
            new() { OrderId = "3", Symbol = "GOOGL", Side = "buy", Quantity = 10, OrderType = "limit", LimitPrice = 150m, Status = OrderStatus.Pending, SubmittedAt = DateTime.Now.AddMinutes(-30) }
        }.Take(count).ToList());
    }

    public Task<OrderResult> SubmitOrderAsync(OrderRequest request)
    {
        var orderId = Guid.NewGuid().ToString()[..8];
        _orders.Add(new Order
        {
            OrderId = orderId,
            Symbol = request.Symbol,
            Side = request.Side,
            Quantity = request.Quantity,
            OrderType = request.OrderType,
            LimitPrice = request.LimitPrice,
            StopPrice = request.StopPrice,
            Status = OrderStatus.Pending,
            SubmittedAt = DateTime.Now
        });

        return Task.FromResult(new OrderResult { Success = true, OrderId = orderId });
    }

    public Task<bool> CancelOrderAsync(string orderId)
    {
        var order = _orders.FirstOrDefault(o => o.OrderId == orderId);
        if (order is not null)
        {
            order.Status = OrderStatus.Cancelled;
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<bool> ClosePositionAsync(string symbol)
    {
        // Would submit market order to close position
        return Task.FromResult(true);
    }
}

public class AlertService : IAlertService
{
    private readonly List<Alert> _alerts = new()
    {
        new() { Id = "1", Symbol = "AAPL", Condition = "Price above", TargetPrice = 180m, IsActive = true, CreatedAt = DateTime.Now.AddDays(-5) },
        new() { Id = "2", Symbol = "TSLA", Condition = "Price below", TargetPrice = 200m, IsActive = true, CreatedAt = DateTime.Now.AddDays(-3) }
    };

    public Task<List<Alert>> GetActiveAlertsAsync() =>
        Task.FromResult(_alerts.Where(a => a.IsActive).ToList());

    public Task<List<Alert>> GetAllAlertsAsync() =>
        Task.FromResult(_alerts.ToList());

    public Task<string> CreateAlertAsync(string symbol, string condition, decimal targetPrice)
    {
        var id = Guid.NewGuid().ToString()[..8];
        _alerts.Add(new Alert { Id = id, Symbol = symbol, Condition = condition, TargetPrice = targetPrice, IsActive = true, CreatedAt = DateTime.Now });
        return Task.FromResult(id);
    }

    public Task UpdateAlertAsync(string alertId, bool isActive)
    {
        var alert = _alerts.FirstOrDefault(a => a.Id == alertId);
        if (alert is not null) alert.IsActive = isActive;
        return Task.CompletedTask;
    }

    public Task DeleteAlertAsync(string alertId)
    {
        _alerts.RemoveAll(a => a.Id == alertId);
        return Task.CompletedTask;
    }
}

public class NotificationService : INotificationService
{
    public Task RequestPermissionAsync()
    {
        // Platform-specific permission request
        return Task.CompletedTask;
    }

    public Task ShowLocalNotificationAsync(string title, string message)
    {
        // Would use platform-specific notification APIs
        return Task.CompletedTask;
    }

    public Task ScheduleNotificationAsync(string title, string message, DateTime scheduledTime)
    {
        // Would schedule notification for specified time
        return Task.CompletedTask;
    }
}

public class StrategyMonitorService : IStrategyMonitorService
{
    public Task<List<StrategyStatus>> GetActiveStrategiesAsync()
    {
        return Task.FromResult(new List<StrategyStatus>
        {
            new() { Id = "1", Name = "Moving Average Crossover", Status = "Running", PnL = 1250m, TradesCount = 15 },
            new() { Id = "2", Name = "RSI Momentum", Status = "Paused", PnL = 500m, TradesCount = 8 }
        });
    }

    public Task<bool> StartStrategyAsync(string strategyId) => Task.FromResult(true);
    public Task<bool> StopStrategyAsync(string strategyId) => Task.FromResult(true);
}

/// <summary>
/// High-performance indicator calculation service using the v2 Builder API.
/// Leverages the facade pattern for optimal computation and memory management.
/// </summary>
public sealed class IndicatorService : IIndicatorService
{
    /// <summary>
    /// Calculates all common indicators using the v2 Builder API.
    /// This is the high-performance entry point for bulk indicator computation.
    /// </summary>
    public IndicatorResults CalculateIndicators(List<Bar> bars, IndicatorSettings? settings = null)
    {
        if (bars.Count == 0)
        {
            return new IndicatorResults { DataCount = 0 };
        }

        settings ??= new IndicatorSettings();
        var results = new IndicatorResults { DataCount = bars.Count };

        try
        {
            // Convert MAUI bars to StockData for the library
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);

            // Using the v2 Builder API with facade pattern
            OoplesFinance.StockIndicators.Builder.SeriesHandle smaHandle = default;
            OoplesFinance.StockIndicators.Builder.SeriesHandle emaHandle = default;
            OoplesFinance.StockIndicators.Builder.SeriesHandle rsiHandle = default;
            OoplesFinance.StockIndicators.Builder.MacdResult macdHandles = default;
            OoplesFinance.StockIndicators.Builder.BollingerBandsResult bbHandles = default;
            OoplesFinance.StockIndicators.Builder.StochasticResult stochHandles = default;
            OoplesFinance.StockIndicators.Builder.SeriesHandle atrHandle = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators =>
                {
                    var price = indicators.Price();
                    smaHandle = indicators.Sma(settings.SmaLength, price);
                    emaHandle = indicators.Ema(settings.EmaLength, price);
                    rsiHandle = indicators.Rsi(settings.RsiLength, price);
                    macdHandles = indicators.Macd(settings.MacdFastLength, settings.MacdSlowLength, settings.MacdSignalLength, price);
                    bbHandles = indicators.BollingerBands(settings.BollingerLength, settings.BollingerStdDev, price);
                    stochHandles = indicators.Stochastic(settings.StochasticKLength, settings.StochasticDLength, price);
                    atrHandle = indicators.Atr(settings.AtrLength, price);
                });

            using var runtime = builder.Build();
            runtime.Subscribe(smaHandle, emaHandle, rsiHandle, macdHandles.Primary, macdHandles.Signal, macdHandles.Histogram,
                             bbHandles.Upper, bbHandles.Middle, bbHandles.Lower, stochHandles.K, stochHandles.D, atrHandle);
            runtime.Start();

            if (runtime.Latest is not null)
            {
                results.Sma = ExtractSeriesValues(runtime.Latest, smaHandle);
                results.Ema = ExtractSeriesValues(runtime.Latest, emaHandle);
                results.Rsi = new RsiResult { Values = ExtractSeriesValues(runtime.Latest, rsiHandle) };
                results.Macd = new MacdIndicatorResult
                {
                    MacdLine = ExtractSeriesValues(runtime.Latest, macdHandles.Primary),
                    SignalLine = ExtractSeriesValues(runtime.Latest, macdHandles.Signal),
                    Histogram = ExtractSeriesValues(runtime.Latest, macdHandles.Histogram)
                };
                results.BollingerBands = new BollingerBandsIndicatorResult
                {
                    Upper = ExtractSeriesValues(runtime.Latest, bbHandles.Upper),
                    Middle = ExtractSeriesValues(runtime.Latest, bbHandles.Middle),
                    Lower = ExtractSeriesValues(runtime.Latest, bbHandles.Lower)
                };
                results.Stochastic = new StochasticIndicatorResult
                {
                    K = ExtractSeriesValues(runtime.Latest, stochHandles.K),
                    D = ExtractSeriesValues(runtime.Latest, stochHandles.D)
                };
                results.Atr = ExtractSeriesValues(runtime.Latest, atrHandle);
            }
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateIndicators", ex);
        }

        return results;
    }

    public List<double> CalculateSma(List<Bar> bars, int length)
    {
        if (bars.Count == 0) return new List<double>();

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.SeriesHandle handle = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handle = indicators.Sma(length));

            using var runtime = builder.Build();
            runtime.Subscribe(handle);
            runtime.Start();

            return runtime.Latest is not null ? ExtractSeriesValues(runtime.Latest, handle) : new List<double>();
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateSma", ex);
            return new List<double>();
        }
    }

    public List<double> CalculateEma(List<Bar> bars, int length)
    {
        if (bars.Count == 0) return new List<double>();

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.SeriesHandle handle = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handle = indicators.Ema(length));

            using var runtime = builder.Build();
            runtime.Subscribe(handle);
            runtime.Start();

            return runtime.Latest is not null ? ExtractSeriesValues(runtime.Latest, handle) : new List<double>();
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateEma", ex);
            return new List<double>();
        }
    }

    public RsiResult CalculateRsi(List<Bar> bars, int length = 14)
    {
        var result = new RsiResult();
        if (bars.Count == 0) return result;

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.SeriesHandle handle = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handle = indicators.Rsi(length));

            using var runtime = builder.Build();
            runtime.Subscribe(handle);
            runtime.Start();

            if (runtime.Latest is not null)
            {
                result.Values = ExtractSeriesValues(runtime.Latest, handle);
            }
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateRsi", ex);
        }

        return result;
    }

    public MacdIndicatorResult CalculateMacd(List<Bar> bars, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        var result = new MacdIndicatorResult();
        if (bars.Count == 0) return result;

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.MacdResult handles = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handles = indicators.Macd(fastLength, slowLength, signalLength));

            using var runtime = builder.Build();
            runtime.Subscribe(handles.Primary, handles.Signal, handles.Histogram);
            runtime.Start();

            if (runtime.Latest is not null)
            {
                result.MacdLine = ExtractSeriesValues(runtime.Latest, handles.Primary);
                result.SignalLine = ExtractSeriesValues(runtime.Latest, handles.Signal);
                result.Histogram = ExtractSeriesValues(runtime.Latest, handles.Histogram);
            }
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateMacd", ex);
        }

        return result;
    }

    public BollingerBandsIndicatorResult CalculateBollingerBands(List<Bar> bars, int length = 20, double stdDevMult = 2)
    {
        var result = new BollingerBandsIndicatorResult();
        if (bars.Count == 0) return result;

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.BollingerBandsResult handles = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handles = indicators.BollingerBands(length, stdDevMult));

            using var runtime = builder.Build();
            runtime.Subscribe(handles.Upper, handles.Middle, handles.Lower);
            runtime.Start();

            if (runtime.Latest is not null)
            {
                result.Upper = ExtractSeriesValues(runtime.Latest, handles.Upper);
                result.Middle = ExtractSeriesValues(runtime.Latest, handles.Middle);
                result.Lower = ExtractSeriesValues(runtime.Latest, handles.Lower);
            }
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateBollingerBands", ex);
        }

        return result;
    }

    public StochasticIndicatorResult CalculateStochastic(List<Bar> bars, int kLength = 14, int dLength = 3)
    {
        var result = new StochasticIndicatorResult();
        if (bars.Count == 0) return result;

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.StochasticResult handles = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handles = indicators.Stochastic(kLength, dLength));

            using var runtime = builder.Build();
            runtime.Subscribe(handles.K, handles.D);
            runtime.Start();

            if (runtime.Latest is not null)
            {
                result.K = ExtractSeriesValues(runtime.Latest, handles.K);
                result.D = ExtractSeriesValues(runtime.Latest, handles.D);
            }
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateStochastic", ex);
        }

        return result;
    }

    public List<double> CalculateAtr(List<Bar> bars, int length = 14)
    {
        if (bars.Count == 0) return new List<double>();

        try
        {
            var stockData = ConvertToStockData(bars);
            var source = OoplesFinance.StockIndicators.Builder.IndicatorDataSource.FromBatch(stockData);
            OoplesFinance.StockIndicators.Builder.SeriesHandle handle = default;

            var builder = new OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder(source)
                .ConfigureIndicators(indicators => handle = indicators.Atr(length));

            using var runtime = builder.Build();
            runtime.Subscribe(handle);
            runtime.Start();

            return runtime.Latest is not null ? ExtractSeriesValues(runtime.Latest, handle) : new List<double>();
        }
        catch (Exception ex)
        {
            App.LogException("IndicatorService.CalculateAtr", ex);
            return new List<double>();
        }
    }

    /// <summary>
    /// Converts MAUI Bar list to OoplesFinance StockData.
    /// </summary>
    private static OoplesFinance.StockIndicators.Models.StockData ConvertToStockData(List<Bar> bars)
    {
        var opens = bars.Select(b => (double)b.Open).ToList();
        var highs = bars.Select(b => (double)b.High).ToList();
        var lows = bars.Select(b => (double)b.Low).ToList();
        var closes = bars.Select(b => (double)b.Close).ToList();
        var volumes = bars.Select(b => (double)b.Volume).ToList();
        var dates = bars.Select(b => b.Timestamp).ToList();

        return new OoplesFinance.StockIndicators.Models.StockData(opens, highs, lows, closes, volumes, dates);
    }

    /// <summary>
    /// Extracts values from an indicator snapshot for a given series handle.
    /// </summary>
    private static List<double> ExtractSeriesValues(
        OoplesFinance.StockIndicators.Builder.IndicatorSnapshot snapshot,
        OoplesFinance.StockIndicators.Builder.SeriesHandle handle)
    {
        if (snapshot.TryGetSeries(handle, out var values))
        {
            var result = new List<double>(values.Length);
            var span = values.Span;
            for (int i = 0; i < span.Length; i++)
            {
                result.Add(span[i]);
            }
            return result;
        }
        return new List<double>();
    }
}

/// <summary>
/// AI-powered analysis service implementation.
/// Integrates RegimeDetector, PriceAnomalyDetector, and indicator analysis
/// to generate trading signals and recommendations.
/// </summary>
public sealed class AIAnalysisService : IAIAnalysisService
{
    private readonly IIndicatorService _indicatorService;
    private readonly OoplesFinance.StockIndicators.Builder.ML.RegimeDetector _regimeDetector;
    private readonly OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection.PriceAnomalyDetector _anomalyDetector;

    public AIAnalysisService(IIndicatorService indicatorService)
    {
        _indicatorService = indicatorService;
        _regimeDetector = new OoplesFinance.StockIndicators.Builder.ML.RegimeDetector
        {
            LookbackPeriod = 60,  // ~3 months for shorter-term analysis
            ShortTermLookback = 10
        };
        _anomalyDetector = new OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection.PriceAnomalyDetector
        {
            LookbackPeriod = 30,
            ZScoreThreshold = 2.5,
            MinGapPercent = 1.5
        };
    }

    /// <summary>
    /// Generates AI trading signals based on technical indicators and market analysis.
    /// </summary>
    public async Task<AITradingSignal> GenerateSignalAsync(string symbol, List<Bar> bars)
    {
        return await Task.Run(() =>
        {
            var signal = new AITradingSignal { Symbol = symbol };

            if (bars.Count < 30)
            {
                signal.Signal = SignalType.Hold;
                signal.Confidence = 0;
                signal.Reasoning = "Insufficient data for analysis";
                return signal;
            }

            // Calculate indicators
            var indicators = _indicatorService.CalculateIndicators(bars);

            // Analyze market regime
            signal.RegimeAnalysis = AnalyzeMarketRegime(bars);

            // Generate indicator-based signals
            var indicatorSignals = AnalyzeIndicators(bars, indicators);
            signal.IndicatorSignals = indicatorSignals.Signals;

            // Calculate overall signal
            var (signalType, confidence, reasoning) = CalculateOverallSignal(
                indicatorSignals,
                signal.RegimeAnalysis,
                bars);

            signal.Signal = signalType;
            signal.Confidence = confidence;
            signal.Reasoning = reasoning;

            // Calculate entry, stop loss, and take profit
            var lastPrice = bars[^1].Close;
            var atr = indicators.Atr.Count > 0 ? (decimal)indicators.Atr[^1] : lastPrice * 0.02m;

            signal.SuggestedEntryPrice = lastPrice;
            if (signalType is SignalType.Buy or SignalType.StrongBuy)
            {
                signal.SuggestedStopLoss = lastPrice - (atr * 1.5m);
                signal.SuggestedTakeProfit = lastPrice + (atr * 2.5m);
            }
            else if (signalType is SignalType.Sell or SignalType.StrongSell)
            {
                signal.SuggestedStopLoss = lastPrice + (atr * 1.5m);
                signal.SuggestedTakeProfit = lastPrice - (atr * 2.5m);
            }

            return signal;
        });
    }

    /// <summary>
    /// Analyzes the current market regime.
    /// </summary>
    public MarketRegimeAnalysis AnalyzeMarketRegime(List<Bar> bars)
    {
        var result = new MarketRegimeAnalysis();

        if (bars.Count < 30)
        {
            return result;
        }

        try
        {
            var prices = bars.Select(b => b.Close).ToList();
            var analysis = _regimeDetector.AnalyzeRegime(prices);

            result.Regime = analysis.CurrentRegime.ToString();
            result.Trend = analysis.TrendDirection.ToString();
            result.Volatility = analysis.VolatilityRegime.ToString();
            result.Confidence = analysis.Confidence;
            result.Momentum = analysis.Momentum;
            result.TrendStrength = analysis.TrendStrength;

            foreach (var kvp in analysis.RegimeProbabilities)
            {
                result.RegimeProbabilities[kvp.Key.ToString()] = kvp.Value;
            }
        }
        catch (Exception ex)
        {
            App.LogException("AIAnalysisService.AnalyzeMarketRegime", ex);
        }

        return result;
    }

    /// <summary>
    /// Detects price anomalies in the data.
    /// </summary>
    public List<DetectedAnomaly> DetectAnomalies(List<Bar> bars)
    {
        var anomalies = new List<DetectedAnomaly>();

        if (bars.Count < 30)
        {
            return anomalies;
        }

        try
        {
            // Convert to PriceBar format used by the detector
            var priceBars = bars.Select(b => new OoplesFinance.StockIndicators.Builder.ML.AnomalyDetection.PriceBar
            {
                Timestamp = b.Timestamp,
                Open = b.Open,
                High = b.High,
                Low = b.Low,
                Close = b.Close,
                Volume = b.Volume
            }).ToList();

            var detected = _anomalyDetector.DetectAnomalies(priceBars);

            foreach (var a in detected)
            {
                anomalies.Add(new DetectedAnomaly
                {
                    Index = a.Index,
                    Timestamp = a.Timestamp,
                    Type = a.Type.ToString(),
                    Severity = a.Severity.ToString(),
                    Score = a.Score,
                    Description = a.Description,
                    Price = a.Price
                });
            }
        }
        catch (Exception ex)
        {
            App.LogException("AIAnalysisService.DetectAnomalies", ex);
        }

        return anomalies;
    }

    /// <summary>
    /// Gets AI-powered trading recommendations.
    /// </summary>
    public async Task<AIRecommendation> GetRecommendationAsync(string symbol, List<Bar> bars, IndicatorResults indicators)
    {
        return await Task.Run(() =>
        {
            var recommendation = new AIRecommendation { Symbol = symbol };

            if (bars.Count < 30)
            {
                recommendation.Summary = "Insufficient data for recommendation";
                return recommendation;
            }

            var regimeAnalysis = AnalyzeMarketRegime(bars);
            var indicatorAnalysis = AnalyzeIndicators(bars, indicators);
            var anomalies = DetectAnomalies(bars);

            // Collect bullish and bearish factors
            CollectFactors(recommendation, regimeAnalysis, indicatorAnalysis, anomalies, bars);

            // Calculate risk level
            recommendation.RiskLevel = CalculateRiskLevel(regimeAnalysis, anomalies);

            // Determine action
            var bullishScore = recommendation.BullishFactors.Count;
            var bearishScore = recommendation.BearishFactors.Count;
            var netScore = bullishScore - bearishScore;

            if (netScore >= 3)
            {
                recommendation.Action = "Strong Buy";
                recommendation.Confidence = Math.Min(0.9, 0.5 + netScore * 0.1);
            }
            else if (netScore >= 1)
            {
                recommendation.Action = "Buy";
                recommendation.Confidence = Math.Min(0.75, 0.4 + netScore * 0.1);
            }
            else if (netScore <= -3)
            {
                recommendation.Action = "Strong Sell";
                recommendation.Confidence = Math.Min(0.9, 0.5 + Math.Abs(netScore) * 0.1);
            }
            else if (netScore <= -1)
            {
                recommendation.Action = "Sell";
                recommendation.Confidence = Math.Min(0.75, 0.4 + Math.Abs(netScore) * 0.1);
            }
            else
            {
                recommendation.Action = "Hold";
                recommendation.Confidence = 0.5;
            }

            // Generate summary
            recommendation.Summary = GenerateSummary(recommendation, regimeAnalysis, bars);

            // Determine time horizon based on regime
            recommendation.TimeHorizon = regimeAnalysis.Volatility switch
            {
                "High" => "Short-term (days)",
                "Low" => "Medium-term (weeks)",
                _ => "Short to medium-term"
            };

            // Suggest position size based on confidence and risk
            recommendation.SuggestedPositionSize = Math.Max(0.01m,
                (decimal)recommendation.Confidence * (1m - recommendation.RiskLevel) * 0.1m);

            return recommendation;
        });
    }

    /// <summary>
    /// Calculates a confidence score for a potential trade.
    /// </summary>
    public double CalculateTradeConfidence(List<Bar> bars, IndicatorResults indicators, TradeSide side)
    {
        if (bars.Count < 20 || indicators.DataCount == 0)
        {
            return 0;
        }

        var confidence = 0.5; // Base confidence

        // RSI confirmation
        if (indicators.Rsi.Values.Count > 0)
        {
            var rsi = indicators.Rsi.Values[^1];
            if (side == TradeSide.Buy && rsi < 40) confidence += 0.1;
            else if (side == TradeSide.Buy && rsi > 70) confidence -= 0.1;
            else if (side == TradeSide.Sell && rsi > 60) confidence += 0.1;
            else if (side == TradeSide.Sell && rsi < 30) confidence -= 0.1;
        }

        // MACD confirmation
        if (indicators.Macd.MacdLine.Count > 0 && indicators.Macd.SignalLine.Count > 0)
        {
            var macd = indicators.Macd.MacdLine[^1];
            var signal = indicators.Macd.SignalLine[^1];
            var histogram = indicators.Macd.Histogram.Count > 0 ? indicators.Macd.Histogram[^1] : 0;

            if (side == TradeSide.Buy && macd > signal && histogram > 0) confidence += 0.15;
            else if (side == TradeSide.Sell && macd < signal && histogram < 0) confidence += 0.15;
        }

        // Moving average alignment
        if (indicators.Sma.Count > 0 && indicators.Ema.Count > 0)
        {
            var price = (double)bars[^1].Close;
            var sma = indicators.Sma[^1];
            var ema = indicators.Ema[^1];

            if (side == TradeSide.Buy && price > sma && price > ema) confidence += 0.1;
            else if (side == TradeSide.Sell && price < sma && price < ema) confidence += 0.1;
        }

        // Bollinger Bands position
        if (indicators.BollingerBands.Upper.Count > 0)
        {
            var price = (double)bars[^1].Close;
            var upper = indicators.BollingerBands.Upper[^1];
            var lower = indicators.BollingerBands.Lower[^1];
            var middle = indicators.BollingerBands.Middle[^1];

            if (side == TradeSide.Buy && price < lower) confidence += 0.1; // Oversold
            else if (side == TradeSide.Sell && price > upper) confidence += 0.1; // Overbought
        }

        // Market regime alignment
        var regime = AnalyzeMarketRegime(bars);
        if (side == TradeSide.Buy && regime.Trend == "Bullish") confidence += 0.1;
        else if (side == TradeSide.Sell && regime.Trend == "Bearish") confidence += 0.1;

        // High volatility reduces confidence
        if (regime.Volatility == "High") confidence *= 0.85;

        return Math.Clamp(confidence, 0, 1);
    }

    #region Private Helper Methods

    private IndicatorAnalysisResult AnalyzeIndicators(List<Bar> bars, IndicatorResults indicators)
    {
        var result = new IndicatorAnalysisResult();
        var bullishCount = 0;
        var bearishCount = 0;

        if (bars.Count == 0) return result;

        var lastPrice = (double)bars[^1].Close;

        // RSI Analysis
        if (indicators.Rsi.Values.Count > 0)
        {
            var rsi = indicators.Rsi.Values[^1];
            if (rsi < 30)
            {
                result.Signals.Add("RSI oversold (<30) - Bullish signal");
                bullishCount++;
            }
            else if (rsi > 70)
            {
                result.Signals.Add("RSI overbought (>70) - Bearish signal");
                bearishCount++;
            }
            else if (rsi < 45)
            {
                result.Signals.Add("RSI trending low - Mild bullish");
                bullishCount++;
            }
            else if (rsi > 55)
            {
                result.Signals.Add("RSI trending high - Mild bearish");
                bearishCount++;
            }
        }

        // MACD Analysis
        if (indicators.Macd.MacdLine.Count > 1 && indicators.Macd.SignalLine.Count > 1)
        {
            var macd = indicators.Macd.MacdLine[^1];
            var macdPrev = indicators.Macd.MacdLine[^2];
            var signal = indicators.Macd.SignalLine[^1];
            var signalPrev = indicators.Macd.SignalLine[^2];

            // Crossover detection
            if (macdPrev <= signalPrev && macd > signal)
            {
                result.Signals.Add("MACD bullish crossover - Strong buy signal");
                bullishCount += 2;
            }
            else if (macdPrev >= signalPrev && macd < signal)
            {
                result.Signals.Add("MACD bearish crossover - Strong sell signal");
                bearishCount += 2;
            }
            else if (macd > signal)
            {
                result.Signals.Add("MACD above signal - Bullish momentum");
                bullishCount++;
            }
            else
            {
                result.Signals.Add("MACD below signal - Bearish momentum");
                bearishCount++;
            }
        }

        // Moving Average Analysis
        if (indicators.Sma.Count > 0 && indicators.Ema.Count > 0)
        {
            var sma = indicators.Sma[^1];
            var ema = indicators.Ema[^1];

            if (lastPrice > sma && lastPrice > ema)
            {
                result.Signals.Add("Price above SMA & EMA - Bullish trend");
                bullishCount++;
            }
            else if (lastPrice < sma && lastPrice < ema)
            {
                result.Signals.Add("Price below SMA & EMA - Bearish trend");
                bearishCount++;
            }

            // Golden/Death cross approximation
            if (indicators.Sma.Count > 1 && indicators.Ema.Count > 1)
            {
                var smaPrev = indicators.Sma[^2];
                var emaPrev = indicators.Ema[^2];

                if (emaPrev <= smaPrev && ema > sma)
                {
                    result.Signals.Add("EMA crossed above SMA - Golden cross");
                    bullishCount += 2;
                }
                else if (emaPrev >= smaPrev && ema < sma)
                {
                    result.Signals.Add("EMA crossed below SMA - Death cross");
                    bearishCount += 2;
                }
            }
        }

        // Bollinger Bands Analysis
        if (indicators.BollingerBands.Upper.Count > 0)
        {
            var upper = indicators.BollingerBands.Upper[^1];
            var lower = indicators.BollingerBands.Lower[^1];
            var middle = indicators.BollingerBands.Middle[^1];

            if (lastPrice <= lower)
            {
                result.Signals.Add("Price at lower Bollinger Band - Potential reversal");
                bullishCount++;
            }
            else if (lastPrice >= upper)
            {
                result.Signals.Add("Price at upper Bollinger Band - Potential reversal");
                bearishCount++;
            }

            // Squeeze detection (bands narrowing)
            var bandWidth = (upper - lower) / middle;
            if (bandWidth < 0.04) // Less than 4% band width
            {
                result.Signals.Add("Bollinger Band squeeze - Breakout expected");
            }
        }

        // Stochastic Analysis
        if (indicators.Stochastic.K.Count > 0 && indicators.Stochastic.D.Count > 0)
        {
            var k = indicators.Stochastic.K[^1];
            var d = indicators.Stochastic.D[^1];

            if (k < 20 && d < 20)
            {
                result.Signals.Add("Stochastic oversold - Bullish signal");
                bullishCount++;
            }
            else if (k > 80 && d > 80)
            {
                result.Signals.Add("Stochastic overbought - Bearish signal");
                bearishCount++;
            }
        }

        result.BullishScore = bullishCount;
        result.BearishScore = bearishCount;
        result.NetScore = bullishCount - bearishCount;

        return result;
    }

    private (SignalType Signal, double Confidence, string Reasoning) CalculateOverallSignal(
        IndicatorAnalysisResult indicatorAnalysis,
        MarketRegimeAnalysis regimeAnalysis,
        List<Bar> bars)
    {
        var netScore = indicatorAnalysis.NetScore;

        // Adjust for regime
        if (regimeAnalysis.Trend == "Bullish")
        {
            netScore += 1;
        }
        else if (regimeAnalysis.Trend == "Bearish")
        {
            netScore -= 1;
        }

        // Calculate confidence based on signal strength and regime confidence
        var baseConfidence = Math.Min(0.9, 0.4 + Math.Abs(netScore) * 0.1);
        var confidence = baseConfidence * (0.5 + regimeAnalysis.Confidence * 0.5);

        // High volatility reduces confidence
        if (regimeAnalysis.Volatility == "High")
        {
            confidence *= 0.8;
        }

        SignalType signal;
        string reasoning;

        if (netScore >= 4)
        {
            signal = SignalType.StrongBuy;
            reasoning = $"Strong bullish consensus: {indicatorAnalysis.BullishScore} bullish signals vs {indicatorAnalysis.BearishScore} bearish. " +
                       $"Market regime: {regimeAnalysis.Regime}, Trend: {regimeAnalysis.Trend}.";
        }
        else if (netScore >= 2)
        {
            signal = SignalType.Buy;
            reasoning = $"Moderate bullish signal: {indicatorAnalysis.BullishScore} bullish vs {indicatorAnalysis.BearishScore} bearish indicators. " +
                       $"Current trend: {regimeAnalysis.Trend}.";
        }
        else if (netScore <= -4)
        {
            signal = SignalType.StrongSell;
            reasoning = $"Strong bearish consensus: {indicatorAnalysis.BearishScore} bearish signals vs {indicatorAnalysis.BullishScore} bullish. " +
                       $"Market regime: {regimeAnalysis.Regime}, Trend: {regimeAnalysis.Trend}.";
        }
        else if (netScore <= -2)
        {
            signal = SignalType.Sell;
            reasoning = $"Moderate bearish signal: {indicatorAnalysis.BearishScore} bearish vs {indicatorAnalysis.BullishScore} bullish indicators. " +
                       $"Current trend: {regimeAnalysis.Trend}.";
        }
        else
        {
            signal = SignalType.Hold;
            reasoning = $"Mixed signals: {indicatorAnalysis.BullishScore} bullish, {indicatorAnalysis.BearishScore} bearish. " +
                       $"Wait for clearer direction. Market regime: {regimeAnalysis.Regime}.";
            confidence = 0.5;
        }

        return (signal, confidence, reasoning);
    }

    private void CollectFactors(
        AIRecommendation recommendation,
        MarketRegimeAnalysis regimeAnalysis,
        IndicatorAnalysisResult indicatorAnalysis,
        List<DetectedAnomaly> anomalies,
        List<Bar> bars)
    {
        // Regime-based factors
        if (regimeAnalysis.Trend == "Bullish")
        {
            recommendation.BullishFactors.Add($"Bullish market trend (confidence: {regimeAnalysis.Confidence:P0})");
        }
        else if (regimeAnalysis.Trend == "Bearish")
        {
            recommendation.BearishFactors.Add($"Bearish market trend (confidence: {regimeAnalysis.Confidence:P0})");
        }

        if (regimeAnalysis.Momentum > 0.02)
        {
            recommendation.BullishFactors.Add($"Positive momentum: {regimeAnalysis.Momentum:P1}");
        }
        else if (regimeAnalysis.Momentum < -0.02)
        {
            recommendation.BearishFactors.Add($"Negative momentum: {regimeAnalysis.Momentum:P1}");
        }

        // Indicator-based factors
        foreach (var signal in indicatorAnalysis.Signals)
        {
            if (signal.Contains("Bullish") || signal.Contains("buy", StringComparison.OrdinalIgnoreCase) ||
                signal.Contains("Golden"))
            {
                recommendation.BullishFactors.Add(signal);
            }
            else if (signal.Contains("Bearish") || signal.Contains("sell", StringComparison.OrdinalIgnoreCase) ||
                     signal.Contains("Death"))
            {
                recommendation.BearishFactors.Add(signal);
            }
        }

        // Anomaly-based factors
        var recentAnomalies = anomalies.Where(a => a.Index >= bars.Count - 5).ToList();
        foreach (var anomaly in recentAnomalies)
        {
            if (anomaly.Type == "ExtremeReturn")
            {
                recommendation.BearishFactors.Add($"Recent price anomaly detected: {anomaly.Description}");
            }
            else if (anomaly.Type == "VolatilitySpike")
            {
                recommendation.BearishFactors.Add("Volatility spike - increased risk");
            }
        }

        // Price action factors
        if (bars.Count >= 5)
        {
            var recentBars = bars.TakeLast(5).ToList();
            var upDays = recentBars.Count(b => b.Close > b.Open);
            if (upDays >= 4)
            {
                recommendation.BullishFactors.Add("Strong recent price action (4+ up days in last 5)");
            }
            else if (upDays <= 1)
            {
                recommendation.BearishFactors.Add("Weak recent price action (4+ down days in last 5)");
            }
        }
    }

    private decimal CalculateRiskLevel(MarketRegimeAnalysis regimeAnalysis, List<DetectedAnomaly> anomalies)
    {
        var risk = 0.3m; // Base risk

        // Volatility factor
        if (regimeAnalysis.Volatility == "High")
        {
            risk += 0.3m;
        }
        else if (regimeAnalysis.Volatility == "Low")
        {
            risk -= 0.1m;
        }

        // Regime factor
        if (regimeAnalysis.Regime == "Crash")
        {
            risk += 0.4m;
        }
        else if (regimeAnalysis.Regime == "HighVolatility")
        {
            risk += 0.2m;
        }

        // Anomaly factor
        var criticalAnomalies = anomalies.Count(a => a.Severity == "Critical" || a.Severity == "High");
        risk += criticalAnomalies * 0.1m;

        return Math.Clamp(risk, 0m, 1m);
    }

    private string GenerateSummary(
        AIRecommendation recommendation,
        MarketRegimeAnalysis regimeAnalysis,
        List<Bar> bars)
    {
        var lastPrice = bars[^1].Close;
        var change = bars.Count > 1 ? (bars[^1].Close - bars[^2].Close) / bars[^2].Close * 100 : 0;

        return $"Based on analysis of {bars.Count} bars, the AI recommends to {recommendation.Action} " +
               $"with {recommendation.Confidence:P0} confidence. " +
               $"Current price: ${lastPrice:N2} ({change:+0.00;-0.00}%). " +
               $"Market is in a {regimeAnalysis.Regime} regime with {regimeAnalysis.Volatility} volatility. " +
               $"Found {recommendation.BullishFactors.Count} bullish and {recommendation.BearishFactors.Count} bearish factors.";
    }

    #endregion
}

/// <summary>
/// Internal result class for indicator analysis.
/// </summary>
internal sealed class IndicatorAnalysisResult
{
    public List<string> Signals { get; set; } = new();
    public int BullishScore { get; set; }
    public int BearishScore { get; set; }
    public int NetScore { get; set; }
}

#endregion
