using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Options for data configuration.
/// </summary>
public sealed class DataOptions
{
    /// <summary>
    /// Gets or sets the timeframe for bar aggregation.
    /// </summary>
    public BarTimeframe? Timeframe { get; set; }

    /// <summary>
    /// Gets or sets streaming-specific options.
    /// </summary>
    public StreamingOptions? StreamingOptions { get; set; }
}
