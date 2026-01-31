namespace OoplesFinance.TradingApp.Maui.Models;

/// <summary>
/// Account information from the broker.
/// </summary>
public class AccountInfo
{
    public decimal PortfolioValue { get; set; }
    public decimal Cash { get; set; }
    public decimal BuyingPower { get; set; }
    public decimal TodayPnL { get; set; }
    public decimal TotalPnL { get; set; }
    public decimal CostBasis { get; set; }
}

/// <summary>
/// Position held in the portfolio.
/// </summary>
public class Position
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal MarketValue { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal DayPnL { get; set; }
}

/// <summary>
/// Real-time quote for a symbol.
/// </summary>
public class Quote
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal BidPrice { get; set; }
    public decimal AskPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }
    public long Volume { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// OHLCV bar for charting.
/// </summary>
public class Bar
{
    public DateTime Timestamp { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

/// <summary>
/// Market status (open/closed).
/// </summary>
public class MarketStatus
{
    public string Status { get; set; } = string.Empty;
    public bool IsOpen { get; set; }
    public DateTime? NextChange { get; set; }
}

/// <summary>
/// Order placed with the broker.
/// </summary>
public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty; // buy or sell
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal FilledPrice { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string TimeInForce { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? FilledAt { get; set; }
}

/// <summary>
/// Request to submit a new order.
/// </summary>
public class OrderRequest
{
    public string Symbol { get; set; } = string.Empty;
    public string Side { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string OrderType { get; set; } = "market";
    public string TimeInForce { get; set; } = "day";
    public decimal? LimitPrice { get; set; }
    public decimal? StopPrice { get; set; }
}

/// <summary>
/// Result of order submission.
/// </summary>
public class OrderResult
{
    public bool Success { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
}

/// <summary>
/// Price alert configuration.
/// </summary>
public class Alert
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty; // "Price above", "Price below", etc.
    public decimal TargetPrice { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }
}

/// <summary>
/// Status of a running strategy.
/// </summary>
public class StrategyStatus
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "Running", "Paused", "Stopped"
    public decimal PnL { get; set; }
    public int TradesCount { get; set; }
    public DateTime StartedAt { get; set; }
}
