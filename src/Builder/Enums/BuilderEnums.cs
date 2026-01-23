namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Indicates the source kind for indicator data.
/// </summary>
public enum IndicatorSourceKind
{
    /// <summary>
    /// Batch processing mode - all data is available upfront.
    /// </summary>
    Batch,

    /// <summary>
    /// Streaming processing mode - data arrives in real-time.
    /// </summary>
    Streaming
}

/// <summary>
/// Determines when indicators are computed.
/// </summary>
public enum IndicatorComputePolicy
{
    /// <summary>
    /// Indicators are computed on-demand when accessed.
    /// </summary>
    Lazy,

    /// <summary>
    /// All configured indicators are computed immediately.
    /// </summary>
    Eager
}

/// <summary>
/// Preset indicator selections.
/// </summary>
public enum IndicatorPreset
{
    /// <summary>
    /// No default indicators - only compute what is explicitly configured.
    /// This is the recommended default for optimal performance.
    /// </summary>
    None,

    /// <summary>
    /// All available indicators.
    /// </summary>
    All,

    /// <summary>
    /// Core indicators only (SMA, RSI, MACD, Bollinger Bands).
    /// </summary>
    Core,

    /// <summary>
    /// Only specified indicators.
    /// </summary>
    Only
}

/// <summary>
/// Universe of symbols to use.
/// </summary>
public enum SymbolUniverse
{
    /// <summary>
    /// Use provider's default symbols.
    /// </summary>
    ProviderDefault,

    /// <summary>
    /// All US market symbols.
    /// </summary>
    AllUs,

    /// <summary>
    /// All available symbols across all markets.
    /// </summary>
    All
}

/// <summary>
/// Benchmark types for comparison.
/// </summary>
public enum BenchmarkKind
{
    /// <summary>
    /// SPY ETF benchmark.
    /// </summary>
    Spy,

    /// <summary>
    /// QQQ ETF benchmark.
    /// </summary>
    Qqq,

    /// <summary>
    /// Custom symbol benchmark.
    /// </summary>
    Custom
}

/// <summary>
/// Fee model types for backtesting.
/// </summary>
public enum FeeModel
{
    /// <summary>
    /// No fees.
    /// </summary>
    None,

    /// <summary>
    /// Fixed fee per trade.
    /// </summary>
    Fixed,

    /// <summary>
    /// Percentage-based fee.
    /// </summary>
    Percent
}

/// <summary>
/// Slippage model types for backtesting.
/// </summary>
public enum SlippageModel
{
    /// <summary>
    /// No slippage.
    /// </summary>
    None,

    /// <summary>
    /// Fixed tick-based slippage.
    /// </summary>
    FixedTicks,

    /// <summary>
    /// Percentage-based slippage.
    /// </summary>
    Percent
}

/// <summary>
/// Signal trigger types.
/// </summary>
public enum SignalTrigger
{
    /// <summary>
    /// Triggers when value is above threshold.
    /// </summary>
    Above,

    /// <summary>
    /// Triggers when value is below threshold.
    /// </summary>
    Below,

    /// <summary>
    /// Triggers when value crosses above threshold.
    /// </summary>
    CrossesAbove,

    /// <summary>
    /// Triggers when value crosses below threshold.
    /// </summary>
    CrossesBelow,

    /// <summary>
    /// Triggers when value is between low and high thresholds.
    /// </summary>
    Between,

    /// <summary>
    /// Triggers when value is outside low and high thresholds.
    /// </summary>
    Outside
}

/// <summary>
/// Signal group aggregation modes.
/// </summary>
public enum SignalGroupMode
{
    /// <summary>
    /// All conditions must be met.
    /// </summary>
    All,

    /// <summary>
    /// Any condition must be met.
    /// </summary>
    Any,

    /// <summary>
    /// At least N conditions must be met.
    /// </summary>
    AtLeast,

    /// <summary>
    /// A percentage of conditions must be met.
    /// </summary>
    Percent
}

/// <summary>
/// Indicator output types.
/// </summary>
public enum IndicatorOutput
{
    /// <summary>
    /// Primary output value.
    /// </summary>
    Primary,

    /// <summary>
    /// Signal line output (e.g., MACD signal).
    /// </summary>
    Signal,

    /// <summary>
    /// Histogram output (e.g., MACD histogram).
    /// </summary>
    Histogram,

    /// <summary>
    /// Upper band output (e.g., Bollinger upper).
    /// </summary>
    UpperBand,

    /// <summary>
    /// Middle band output (e.g., Bollinger middle).
    /// </summary>
    MiddleBand,

    /// <summary>
    /// Lower band output (e.g., Bollinger lower).
    /// </summary>
    LowerBand
}

/// <summary>
/// Formula operations for computed series.
/// </summary>
public enum FormulaOp
{
    /// <summary>
    /// Add two series.
    /// </summary>
    Add,

    /// <summary>
    /// Subtract two series.
    /// </summary>
    Subtract,

    /// <summary>
    /// Multiply two series.
    /// </summary>
    Multiply,

    /// <summary>
    /// Divide two series.
    /// </summary>
    Divide
}

/// <summary>
/// Types of series nodes in the computation graph.
/// </summary>
public enum SeriesNodeKind
{
    /// <summary>
    /// Base price series.
    /// </summary>
    Base,

    /// <summary>
    /// Indicator-computed series.
    /// </summary>
    Indicator,

    /// <summary>
    /// Formula-computed series.
    /// </summary>
    Formula
}

/// <summary>
/// Trade actions for auto-trading.
/// </summary>
public enum TradeAction
{
    /// <summary>
    /// Market buy order.
    /// </summary>
    MarketBuy,

    /// <summary>
    /// Market sell order.
    /// </summary>
    MarketSell,

    /// <summary>
    /// Close existing position.
    /// </summary>
    ClosePosition
}
