using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>
/// V2-native batch computation using StatefulIndicators.
/// Processes all bars through a streaming indicator to produce batch results.
/// </summary>
internal static class BatchCompute
{
    private static readonly string DefaultSymbol = "DEFAULT";
    private static readonly BarTimeframe DefaultTimeframe = BarTimeframe.Days(1);

    /// <summary>
    /// Computes an indicator for all bars in the stock data.
    /// </summary>
    /// <param name="data">The stock data to compute on.</param>
    /// <param name="state">The stateful indicator to use.</param>
    /// <returns>Array of indicator values for each bar.</returns>
    /// <param name="outputKey">
    /// The named output to read, or null for the indicator's primary value. Passing null for an
    /// indicator with several outputs is what made every band of a multi-output result come back as the
    /// same series: the slot the caller asked for was simply not consulted.
    /// </param>
    public static double[] ComputeAll(StockData data, IStreamingIndicatorState state, string? outputKey = null)
    {
        var count = data.Count;
        var results = new double[count];
        state.Reset();

        for (var i = 0; i < count; i++)
        {
            var bar = CreateBar(data, i);
            var result = state.Update(bar, isFinal: true, includeOutputs: outputKey is not null);

            // Tested directly rather than through a flag, so the compiler narrows it: after this branch
            // outputKey is known non-null, and the lookup below needs neither a null-forgiving operator
            // nor a coalesce whose fallback could never run.
            if (outputKey is null)
            {
                results[i] = result.Value;
                continue;
            }

            if (result.Outputs is null || !result.Outputs.TryGetValue(outputKey, out var value))
            {
                var available = result.Outputs is null || result.Outputs.Count == 0
                    ? "none"
                    : string.Join(", ", result.Outputs.Keys);

                throw new CalculationException(
                    $"{state.Name} does not publish an output named '{outputKey}'. Available outputs: {available}.");
            }

            results[i] = value;
        }

        return results;
    }

    /// <summary>
    /// Computes an indicator for all bars with custom input values.
    /// </summary>
    /// <param name="data">The stock data (for OHLCV context).</param>
    /// <param name="customValues">Custom input values to use instead of close prices.</param>
    /// <param name="state">The stateful indicator to use.</param>
    /// <returns>Array of indicator values for each bar.</returns>
    public static double[] ComputeAllWithCustomInput(StockData data, double[] customValues, IStreamingIndicatorState state)
    {
        var count = Math.Min(data.Count, customValues.Length);
        var results = new double[count];
        state.Reset();

        for (var i = 0; i < count; i++)
        {
            // Create bar with custom close value for chained indicators
            var bar = CreateBarWithCustomClose(data, i, customValues[i]);
            var result = state.Update(bar, isFinal: true, includeOutputs: false);
            results[i] = result.Value;
        }

        return results;
    }

    /// <summary>
    /// Creates an OhlcvBar from stock data at the specified index.
    /// </summary>
    private static OhlcvBar CreateBar(StockData data, int index)
    {
        var date = data.Dates[index];
        return new OhlcvBar(
            DefaultSymbol,
            DefaultTimeframe,
            date,
            date,
            data.OpenPrices[index],
            data.HighPrices[index],
            data.LowPrices[index],
            data.ClosePrices[index],
            data.Volumes[index],
            isFinal: true);
    }

    /// <summary>
    /// Computes a chain of indicators in a single pass, without materialising the series between them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A chained indicator is evaluated by resolving its input to a complete array and then walking that array
    /// again: <c>SeriesEvaluator.ResolveIndicator</c> calls <c>Resolve</c> on the input node, which computes and
    /// caches the whole upstream series, and <see cref="ComputeAllWithCustomInput"/> then loops over it. An
    /// SMA feeding an EMA is therefore two traversals and one array that exists only to be read once.
    /// </para>
    /// <para>
    /// This is the same computation with the array removed. Each bar goes through the chain in order - the
    /// first state sees the real bar, and each state after it sees a bar whose close is the value the state
    /// before produced, which is exactly what <see cref="ComputeAllWithCustomInput"/> constructs one bar at a
    /// time. The states are the same objects, driven in the same order with the same <c>isFinal</c>, so the
    /// values are identical rather than merely close; the tests hold them to that and not to a tolerance.
    /// </para>
    /// <para>
    /// Only the last state's value is published, so this is only correct when nothing else needs the
    /// intermediates. The caller decides that, not this method: see the fusion conditions in
    /// <c>SeriesEvaluator</c>, which fuses only where an intermediate has a single consumer and is not itself
    /// asked for. See issue #107.
    /// </para>
    /// </remarks>
    /// <param name="data">The stock data to compute on.</param>
    /// <param name="chain">The states in order, the first reading the bars and each next reading the previous.</param>
    public static double[] ComputeAllChained(StockData data, IReadOnlyList<IStreamingIndicatorState> chain)
    {
        if (chain is null || chain.Count == 0)
        {
            throw new ArgumentException("A fused chain needs at least one state.", nameof(chain));
        }

        var count = data.Count;
        var results = new double[count];
        for (var s = 0; s < chain.Count; s++)
        {
            chain[s].Reset();
        }

        for (var i = 0; i < count; i++)
        {
            // The head reads the bar itself; every later state reads a bar carrying the previous value as its
            // close, which is the chaining convention ComputeAllWithCustomInput already uses.
            var value = chain[0].Update(CreateBar(data, i), isFinal: true, includeOutputs: false).Value;
            for (var s = 1; s < chain.Count; s++)
            {
                value = chain[s].Update(CreateBarWithCustomClose(data, i, value), isFinal: true, includeOutputs: false).Value;
            }

            results[i] = value;
        }

        return results;
    }

    /// <summary>
    /// Creates an OhlcvBar with a custom close value (for chained indicators).
    /// </summary>
    private static OhlcvBar CreateBarWithCustomClose(StockData data, int index, double customClose)
    {
        var date = data.Dates[index];
        return new OhlcvBar(
            DefaultSymbol,
            DefaultTimeframe,
            date,
            date,
            data.OpenPrices[index],
            data.HighPrices[index],
            data.LowPrices[index],
            customClose,
            data.Volumes[index],
            isFinal: true);
    }

    /// <summary>
    /// Computes a multi-series indicator for all bars in the stock and market data.
    /// Uses the existing IMultiSeriesIndicatorState interface designed for streaming.
    /// </summary>
    /// <param name="stockData">The stock data to compute on.</param>
    /// <param name="marketData">The market/benchmark data to compare against.</param>
    /// <param name="state">The multi-series indicator state to use.</param>
    /// <param name="primaryKey">The series key for the primary (stock) data.</param>
    /// <param name="marketKey">The series key for the market data.</param>
    /// <returns>Array of indicator values for each bar.</returns>
    public static double[] ComputeAllMultiSeries(
        StockData stockData,
        StockData marketData,
        IMultiSeriesIndicatorState state,
        Streaming.SeriesKey primaryKey,
        Streaming.SeriesKey marketKey)
    {
        var count = Math.Min(stockData.Count, marketData.Count);
        var results = new double[count];
        state.Reset();

        // Create a SeriesStore and MultiSeriesContext for the indicator
        var store = new SeriesStore();
        var context = new MultiSeriesContext(store);

        for (var i = 0; i < count; i++)
        {
            // First update the market bar so it's available when we update the primary
            var marketBar = CreateBarForSeries(marketData, i, marketKey.Symbol, DefaultTimeframe);
            store.Update(marketKey, marketBar);

            // Now update the primary bar and get the result
            var stockBar = CreateBarForSeries(stockData, i, primaryKey.Symbol, DefaultTimeframe);
            store.Update(primaryKey, stockBar);

            var result = state.Update(context, primaryKey, stockBar, isFinal: true, includeOutputs: false);
            results[i] = result.HasValue ? result.Value : 0;
        }

        return results;
    }

    /// <summary>
    /// Creates an OhlcvBar from stock data with a specific symbol.
    /// </summary>
    private static OhlcvBar CreateBarForSeries(StockData data, int index, string symbol, BarTimeframe timeframe)
    {
        var date = data.Dates[index];
        return new OhlcvBar(
            symbol,
            timeframe,
            date,
            date,
            data.OpenPrices[index],
            data.HighPrices[index],
            data.LowPrices[index],
            data.ClosePrices[index],
            data.Volumes[index],
            isFinal: true);
    }
}
