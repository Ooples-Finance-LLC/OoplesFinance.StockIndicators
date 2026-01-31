namespace OoplesFinance.TradingApp.Maui.Models;

/// <summary>
/// Account information from broker.
/// </summary>
public sealed class AccountInfo
{
    public string AccountId { get; set; } = string.Empty;
    public decimal PortfolioValue { get; set; }
    public decimal Cash { get; set; }
    public decimal BuyingPower { get; set; }
    public decimal TodayPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal TodayPnLPercent => PortfolioValue != 0 ? TodayPnL / PortfolioValue : 0;
    public decimal TotalPnLPercent => (PortfolioValue - TotalPnL) != 0 ? TotalPnL / (PortfolioValue - TotalPnL) : 0;
    public bool IsPaper { get; set; }
}

/// <summary>
/// A position in the portfolio.
/// </summary>
public sealed class Position
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal UnrealizedPnLPercent => AveragePrice != 0 ? (CurrentPrice - AveragePrice) / AveragePrice : 0;
    public decimal DayPnL { get; set; }
    public decimal DayPnLPercent { get; set; }
    public decimal CostBasis => Quantity * AveragePrice;
    public Microsoft.Maui.Graphics.Color PnLColor => UnrealizedPnL >= 0
        ? Microsoft.Maui.Graphics.Color.FromArgb("#4EC9B0")  // Green
        : Microsoft.Maui.Graphics.Color.FromArgb("#F14C4C"); // Red
}

/// <summary>
/// An order request to be submitted.
/// </summary>
public sealed class OrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = "buy"; // "buy" or "sell"
    public decimal Quantity { get; set; }
    public string OrderType { get; set; } = "market"; // "market", "limit", "stop", "stop_limit"
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public string TimeInForce { get; set; } = "day"; // "day", "gtc", "ioc", "fok"
}

/// <summary>
/// An order from the broker.
/// </summary>
public sealed class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal FilledQuantity { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
    public decimal? AverageFillPrice { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime? FilledAt { get; set; }
    public string? ErrorMessage { get; set; }

    public string StatusDisplay => Status switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.PartiallyFilled => $"Partial ({FilledQuantity}/{Quantity})",
        OrderStatus.Filled => "Filled",
        OrderStatus.Cancelled => "Cancelled",
        OrderStatus.Rejected => "Rejected",
        OrderStatus.Expired => "Expired",
        _ => "Unknown"
    };

    public Microsoft.Maui.Graphics.Color StatusColor => Status switch
    {
        OrderStatus.Filled => Microsoft.Maui.Graphics.Color.FromArgb("#10B981"),
        OrderStatus.PartiallyFilled => Microsoft.Maui.Graphics.Color.FromArgb("#F59E0B"),
        OrderStatus.Cancelled or OrderStatus.Rejected or OrderStatus.Expired =>
            Microsoft.Maui.Graphics.Color.FromArgb("#EF4444"),
        _ => Microsoft.Maui.Graphics.Color.FromArgb("#6B7280")
    };
}

/// <summary>
/// Order status enumeration.
/// </summary>
public enum OrderStatus
{
    Pending,
    PartiallyFilled,
    Filled,
    Cancelled,
    Rejected,
    Expired
}

/// <summary>
/// A price quote.
/// </summary>
public sealed class Quote
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public decimal BidPrice { get; set; }
    public decimal AskPrice { get; set; }
    public long Volume { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Open { get; set; }
    public decimal PreviousClose { get; set; }
    public DateTime Timestamp { get; set; }

    public Microsoft.Maui.Graphics.Color ChangeColor => Change >= 0
        ? Microsoft.Maui.Graphics.Color.FromArgb("#10B981")
        : Microsoft.Maui.Graphics.Color.FromArgb("#EF4444");
}

/// <summary>
/// A price bar for charts.
/// </summary>
public sealed class Bar
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

/// <summary>
/// Bar interval for historical data.
/// </summary>
public enum BarInterval
{
    Minute1,
    Minute5,
    Minute15,
    Minute30,
    Hour1,
    Hour4,
    Day,
    Week,
    Month
}

/// <summary>
/// Market status information.
/// </summary>
public sealed class MarketStatus
{
    public bool IsOpen { get; set; }
    public DateTime? NextOpen { get; set; }
    public DateTime? NextClose { get; set; }
    public string StatusText => IsOpen ? "Market Open" : "Market Closed";
    public Microsoft.Maui.Graphics.Color StatusColor => IsOpen
        ? Microsoft.Maui.Graphics.Color.FromArgb("#10B981")
        : Microsoft.Maui.Graphics.Color.FromArgb("#6B7280");
}

/// <summary>
/// A price alert.
/// </summary>
public sealed class Alert
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty; // "Price above", "Price below", etc.
    public decimal TargetPrice { get; set; }
    public bool IsActive { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }

    public string ConditionDisplay => $"{Condition} ${TargetPrice:F2}";
}

/// <summary>
/// A trading strategy.
/// </summary>
public sealed class Strategy
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string StrategyJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Backtest result for a strategy.
/// </summary>
public sealed class BacktestResult
{
    public string Id { get; set; } = string.Empty;
    public string StrategyId { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public decimal FinalValue { get; set; }
    public decimal TotalReturn { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal MaxDrawdown { get; set; }
    public decimal SharpeRatio { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal WinRate => TotalTrades > 0 ? (decimal)WinningTrades / TotalTrades : 0;
    public DateTime RunAt { get; set; }
}
