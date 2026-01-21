using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// Interface for data provider defaults.
/// </summary>
public interface IDataProviderDefaults
{
    /// <summary>
    /// Tries to get the default symbols for this provider.
    /// </summary>
    bool TryGetDefaultSymbols(out IReadOnlyList<SymbolId> symbols);

    /// <summary>
    /// Tries to get the default timeframe for this provider.
    /// </summary>
    bool TryGetDefaultTimeframe(out BarTimeframe timeframe);

    /// <summary>
    /// Creates streaming options for this provider.
    /// </summary>
    StreamingOptions? CreateStreamingOptions(IReadOnlyList<SymbolId> symbols, BarTimeframe timeframe);
}

/// <summary>
/// Unified data source for batch and streaming indicator computation.
/// </summary>
public sealed class IndicatorDataSource
{
    private IndicatorDataSource(
        IndicatorSourceKind kind,
        StockData? batchData,
        IStreamSource? streamSource,
        IDataProviderDefaults? providerDefaults)
    {
        Kind = kind;
        BatchData = batchData;
        StreamSource = streamSource;
        ProviderDefaults = providerDefaults;
    }

    /// <summary>
    /// Gets the source kind.
    /// </summary>
    public IndicatorSourceKind Kind { get; }

    /// <summary>
    /// Gets the batch data (when Kind is Batch).
    /// </summary>
    public StockData? BatchData { get; }

    /// <summary>
    /// Gets the stream source (when Kind is Streaming).
    /// </summary>
    public IStreamSource? StreamSource { get; }

    /// <summary>
    /// Gets the provider defaults.
    /// </summary>
    public IDataProviderDefaults? ProviderDefaults { get; }

    /// <summary>
    /// Creates a batch data source.
    /// </summary>
    /// <param name="data">The stock data to use.</param>
    /// <param name="defaults">Optional provider defaults.</param>
    public static IndicatorDataSource FromBatch(StockData data, IDataProviderDefaults? defaults = null)
    {
        return new IndicatorDataSource(IndicatorSourceKind.Batch, data, null, defaults);
    }

    /// <summary>
    /// Creates a streaming data source.
    /// </summary>
    /// <param name="source">The stream source to use.</param>
    /// <param name="defaults">Optional provider defaults.</param>
    public static IndicatorDataSource FromStreaming(IStreamSource source, IDataProviderDefaults? defaults = null)
    {
        return new IndicatorDataSource(IndicatorSourceKind.Streaming, null, source, defaults);
    }
}
