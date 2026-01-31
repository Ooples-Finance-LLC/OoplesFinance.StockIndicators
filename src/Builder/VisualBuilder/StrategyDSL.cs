namespace OoplesFinance.StockIndicators.Builder.VisualBuilder;

using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Domain-Specific Language for defining trading strategies visually.
/// Supports serialization/deserialization for strategy persistence.
/// </summary>
public sealed class StrategyDefinition
{
    /// <summary>Gets or sets the unique strategy identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the strategy name.</summary>
    public string Name { get; set; } = "Untitled Strategy";

    /// <summary>Gets or sets the strategy description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the strategy version.</summary>
    public string Version { get; set; } = "1.0.0";

    /// <summary>Gets or sets the author name.</summary>
    public string Author { get; set; } = string.Empty;

    /// <summary>Gets or sets creation timestamp.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets last modified timestamp.</summary>
    public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Gets or sets the node graph representing the strategy logic.</summary>
    public NodeGraph Graph { get; set; } = new();

    /// <summary>Gets or sets strategy parameters that can be optimized.</summary>
    public List<StrategyParameter> Parameters { get; set; } = new();

    /// <summary>Gets or sets the target symbols for this strategy.</summary>
    public List<string> Symbols { get; set; } = new();

    /// <summary>Gets or sets the timeframe for the strategy.</summary>
    public StrategyTimeframe Timeframe { get; set; } = StrategyTimeframe.Daily;

    /// <summary>Gets or sets risk management settings.</summary>
    public RiskSettings RiskSettings { get; set; } = new();

    /// <summary>
    /// Serializes the strategy to JSON.
    /// </summary>
    public string ToJson()
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Serialize(this, options);
    }

    /// <summary>
    /// Deserializes a strategy from JSON.
    /// </summary>
    public static StrategyDefinition FromJson(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() }
        };
        return JsonSerializer.Deserialize<StrategyDefinition>(json, options)
            ?? throw new InvalidOperationException("Failed to deserialize strategy");
    }

    /// <summary>
    /// Creates a deep copy of the strategy.
    /// </summary>
    public StrategyDefinition Clone()
    {
        return FromJson(ToJson());
    }
}

/// <summary>
/// A parameter that can be adjusted or optimized in the strategy.
/// </summary>
public sealed class StrategyParameter
{
    /// <summary>Gets or sets the parameter name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the display name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Gets or sets the parameter type.</summary>
    public ParameterType Type { get; set; } = ParameterType.Integer;

    /// <summary>Gets or sets the default value.</summary>
    public object? DefaultValue { get; set; }

    /// <summary>Gets or sets the minimum value (for optimization).</summary>
    public object? MinValue { get; set; }

    /// <summary>Gets or sets the maximum value (for optimization).</summary>
    public object? MaxValue { get; set; }

    /// <summary>Gets or sets the step size for optimization.</summary>
    public object? Step { get; set; }

    /// <summary>Gets or sets whether this parameter can be optimized.</summary>
    public bool IsOptimizable { get; set; } = true;

    /// <summary>Gets or sets the parameter description.</summary>
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Parameter data types.
/// </summary>
public enum ParameterType
{
    Integer,
    Decimal,
    Boolean,
    String,
    Enum,
    DateRange
}

/// <summary>
/// Strategy timeframe options.
/// </summary>
public enum StrategyTimeframe
{
    Tick,
    Second1,
    Second5,
    Second15,
    Second30,
    Minute1,
    Minute5,
    Minute15,
    Minute30,
    Hour1,
    Hour4,
    Daily,
    Weekly,
    Monthly
}

/// <summary>
/// Risk management settings for the strategy.
/// </summary>
public sealed class RiskSettings
{
    /// <summary>Gets or sets maximum position size as percent of equity.</summary>
    public decimal MaxPositionSizePercent { get; set; } = 0.10m;

    /// <summary>Gets or sets maximum number of concurrent positions.</summary>
    public int MaxConcurrentPositions { get; set; } = 10;

    /// <summary>Gets or sets maximum daily loss percent.</summary>
    public decimal MaxDailyLossPercent { get; set; } = 0.02m;

    /// <summary>Gets or sets maximum drawdown percent before stopping.</summary>
    public decimal MaxDrawdownPercent { get; set; } = 0.10m;

    /// <summary>Gets or sets default stop loss percent.</summary>
    public decimal DefaultStopLossPercent { get; set; } = 0.02m;

    /// <summary>Gets or sets default take profit percent.</summary>
    public decimal DefaultTakeProfitPercent { get; set; } = 0.04m;

    /// <summary>Gets or sets whether to use trailing stops.</summary>
    public bool UseTrailingStop { get; set; } = false;

    /// <summary>Gets or sets trailing stop activation percent.</summary>
    public decimal TrailingStopActivationPercent { get; set; } = 0.02m;

    /// <summary>Gets or sets trailing stop distance percent.</summary>
    public decimal TrailingStopDistancePercent { get; set; } = 0.01m;
}

/// <summary>
/// Represents a complete trading signal generated by the strategy.
/// </summary>
public sealed class StrategySignal
{
    /// <summary>Gets or sets the signal timestamp.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>Gets or sets the symbol.</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>Gets or sets the signal type.</summary>
    public SignalType Type { get; set; }

    /// <summary>Gets or sets the signal strength (0-1).</summary>
    public decimal Strength { get; set; }

    /// <summary>Gets or sets the suggested entry price.</summary>
    public decimal? EntryPrice { get; set; }

    /// <summary>Gets or sets the suggested stop loss price.</summary>
    public decimal? StopLoss { get; set; }

    /// <summary>Gets or sets the suggested take profit price.</summary>
    public decimal? TakeProfit { get; set; }

    /// <summary>Gets or sets the suggested position size.</summary>
    public decimal? PositionSize { get; set; }

    /// <summary>Gets or sets the reason for the signal.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets contributing indicator values.</summary>
    public Dictionary<string, decimal> IndicatorValues { get; set; } = new();
}

/// <summary>
/// Signal types.
/// </summary>
public enum SignalType
{
    /// <summary>No signal.</summary>
    None,

    /// <summary>Strong buy signal.</summary>
    StrongBuy,

    /// <summary>Buy signal.</summary>
    Buy,

    /// <summary>Weak buy signal.</summary>
    WeakBuy,

    /// <summary>Hold/neutral signal.</summary>
    Hold,

    /// <summary>Weak sell signal.</summary>
    WeakSell,

    /// <summary>Sell signal.</summary>
    Sell,

    /// <summary>Strong sell signal.</summary>
    StrongSell,

    /// <summary>Exit long position.</summary>
    ExitLong,

    /// <summary>Exit short position.</summary>
    ExitShort,

    /// <summary>Scale in to position.</summary>
    ScaleIn,

    /// <summary>Scale out of position.</summary>
    ScaleOut
}

/// <summary>
/// Strategy execution context with market data and state.
/// </summary>
public sealed class StrategyContext
{
    /// <summary>Gets or sets the current bar/candle data.</summary>
    public BarData CurrentBar { get; set; } = new();

    /// <summary>Gets or sets historical bars.</summary>
    public IReadOnlyList<BarData> History { get; set; } = Array.Empty<BarData>();

    /// <summary>Gets or sets current positions.</summary>
    public IReadOnlyList<PositionInfo> Positions { get; set; } = Array.Empty<PositionInfo>();

    /// <summary>Gets or sets account equity.</summary>
    public decimal AccountEquity { get; set; }

    /// <summary>Gets or sets available buying power.</summary>
    public decimal BuyingPower { get; set; }

    /// <summary>Gets or sets today's realized P&L.</summary>
    public decimal DailyPnL { get; set; }

    /// <summary>Gets or sets computed indicator values.</summary>
    public Dictionary<string, decimal> IndicatorValues { get; set; } = new();

    /// <summary>Gets or sets custom state for the strategy.</summary>
    public Dictionary<string, object> State { get; set; } = new();
}

/// <summary>
/// Bar/candle data.
/// </summary>
public sealed class BarData
{
    public DateTime Timestamp { get; set; }
    public string Symbol { get; set; } = string.Empty;
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
}

/// <summary>
/// Position information.
/// </summary>
public sealed class PositionInfo
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal AveragePrice { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal UnrealizedPnL { get; set; }
    public decimal UnrealizedPnLPercent { get; set; }
    public DateTime OpenedAt { get; set; }
}
