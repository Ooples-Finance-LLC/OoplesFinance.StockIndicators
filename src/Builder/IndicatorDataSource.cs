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
/// Provides a common abstraction for both historical (batch) and real-time (streaming) data.
/// </summary>
public sealed class IndicatorDataSource
{
    private IndicatorDataSource(
        IndicatorSourceKind kind,
        StockData? batchData,
        IStreamSource? streamSource,
        IDataProviderDefaults? providerDefaults,
        SymbolId? symbol,
        BarTimeframe? timeframe)
    {
        Kind = kind;
        BatchData = batchData;
        StreamSource = streamSource;
        ProviderDefaults = providerDefaults;
        Symbol = symbol;
        Timeframe = timeframe;
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
    /// Gets the symbol for this data source (when configured).
    /// </summary>
    public SymbolId? Symbol { get; }

    /// <summary>
    /// Gets the timeframe for this data source (when configured).
    /// </summary>
    public BarTimeframe? Timeframe { get; }

    /// <summary>
    /// Creates a batch data source from a single StockData instance.
    /// </summary>
    /// <param name="data">The stock data to use.</param>
    /// <param name="defaults">Optional provider defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public static IndicatorDataSource FromBatch(StockData data, IDataProviderDefaults? defaults = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        return new IndicatorDataSource(IndicatorSourceKind.Batch, data, null, defaults, null, null);
    }

    /// <summary>
    /// Creates a batch data source from a collection of stock data.
    /// The data is merged into a single StockData instance.
    /// </summary>
    /// <param name="data">The collection of stock data to use.</param>
    /// <param name="defaults">Optional provider defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when data is null.</exception>
    public static IndicatorDataSource FromBatch(IEnumerable<StockData> data, IDataProviderDefaults? defaults = null)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        var list = data.ToList();
        if (list.Count == 0)
        {
            throw new ArgumentException("Data collection cannot be empty.", nameof(data));
        }

        if (list.Count == 1)
        {
            return FromBatch(list[0], defaults);
        }

        // Merge multiple StockData instances by concatenating their data
        var merged = MergeStockData(list);
        return new IndicatorDataSource(IndicatorSourceKind.Batch, merged, null, defaults, null, null);
    }

    /// <summary>
    /// Creates a streaming data source from an IStreamSource.
    /// </summary>
    /// <param name="source">The stream source to use.</param>
    /// <param name="defaults">Optional provider defaults.</param>
    /// <exception cref="ArgumentNullException">Thrown when source is null.</exception>
    public static IndicatorDataSource FromStreaming(IStreamSource source, IDataProviderDefaults? defaults = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        return new IndicatorDataSource(IndicatorSourceKind.Streaming, null, source, defaults, null, null);
    }

    /// <summary>
    /// Creates a new data source configured for a specific symbol and timeframe.
    /// Enables multi-symbol support by creating symbol-specific data sources.
    /// </summary>
    /// <param name="symbol">The symbol to configure.</param>
    /// <param name="timeframe">The timeframe to use.</param>
    /// <returns>A new data source configured for the specified symbol and timeframe.</returns>
    public IndicatorDataSource For(string symbol, BarTimeframe? timeframe = null)
    {
        return For(new SymbolId(symbol), timeframe);
    }

    /// <summary>
    /// Creates a new data source configured for a specific symbol and timeframe.
    /// Enables multi-symbol support by creating symbol-specific data sources.
    /// </summary>
    /// <param name="symbol">The symbol to configure.</param>
    /// <param name="timeframe">The timeframe to use.</param>
    /// <returns>A new data source configured for the specified symbol and timeframe.</returns>
    public IndicatorDataSource For(SymbolId symbol, BarTimeframe? timeframe = null)
    {
        return new IndicatorDataSource(
            Kind,
            BatchData,
            StreamSource,
            ProviderDefaults,
            symbol,
            timeframe ?? Timeframe);
    }

    /// <summary>
    /// Merges multiple StockData instances into one, ensuring chronological order.
    /// </summary>
    private static StockData MergeStockData(List<StockData> dataList)
    {
        // Calculate total capacity
        int totalCapacity = 0;
        foreach (var data in dataList)
        {
            totalCapacity += data.Count;
        }

        // Collect all ticks into a list for sorting
        var allTicks = new List<TickerData>(totalCapacity);
        foreach (var data in dataList)
        {
            allTicks.AddRange(data.TickerDataList);
        }

        // Sort by date to ensure chronological order (oldest to newest)
        allTicks.Sort((a, b) => a.Date.CompareTo(b.Date));

        // Build output lists from sorted data
        var dates = new List<DateTime>(totalCapacity);
        var opens = new List<double>(totalCapacity);
        var highs = new List<double>(totalCapacity);
        var lows = new List<double>(totalCapacity);
        var closes = new List<double>(totalCapacity);
        var volumes = new List<double>(totalCapacity);

        foreach (var tick in allTicks)
        {
            dates.Add(tick.Date);
            opens.Add((double)tick.Open);
            highs.Add((double)tick.High);
            lows.Add((double)tick.Low);
            closes.Add((double)tick.Close);
            volumes.Add((double)tick.Volume);
        }

        // StockData constructor signature: opens, highs, lows, closes, volumes, dates
        return new StockData(opens, highs, lows, closes, volumes, dates);
    }
}
