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
    public static double[] ComputeAll(StockData data, IStreamingIndicatorState state)
    {
        var count = data.Count;
        var results = new double[count];
        state.Reset();

        for (var i = 0; i < count; i++)
        {
            var bar = CreateBar(data, i);
            var result = state.Update(bar, isFinal: true, includeOutputs: false);
            results[i] = result.Value;
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
