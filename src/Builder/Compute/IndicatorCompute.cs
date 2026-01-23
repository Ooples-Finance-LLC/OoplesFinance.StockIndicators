using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Builder.Compute;

/// <summary>
/// Internal fast path for indicator computation.
/// Bypasses graph machinery for maximum performance on simple single-output indicators.
/// </summary>
/// <remarks>
/// <para>This class provides a zero-allocation computation path for common indicators.</para>
/// <para>Results are stored in pooled buffers that are automatically returned on dispose.</para>
/// <para>For complex multi-output or chained indicators, use the standard SeriesEvaluator path.</para>
/// </remarks>
internal static partial class IndicatorCompute
{
    /// <summary>
    /// Computes an indicator using the fast path if available, falling back to standard computation.
    /// </summary>
    /// <param name="data">The stock data to compute on.</param>
    /// <param name="spec">The indicator specification.</param>
    /// <param name="context">The compute context for buffer management.</param>
    /// <returns>A ComputeBuffer containing the indicator result, or null if fast path unavailable.</returns>
    public static ComputeBuffer? TryComputeFast(StockData data, IndicatorSpec spec, ComputeContext context)
    {
        // Only use fast path for single-output indicators with typed options
        if (spec.Output != IndicatorOutput.Primary)
        {
            return null;
        }

        return spec.Options switch
        {
            SmaSpecOptions sma => ComputeSmaFast(data, context, sma.Length),
            EmaSpecOptions ema => ComputeEmaFast(data, context, ema.Length),
            RsiSpecOptions rsi => ComputeRsiFast(data, context, rsi.Length),
            AtrSpecOptions atr => ComputeAtrFast(data, context, atr.Length),
            _ => null
        };
    }

    #region Moving Averages

    /// <summary>
    /// Computes Simple Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeSmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SimpleMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Exponential Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Weighted Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WeightedMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    #endregion

    #region Oscillators

    /// <summary>
    /// Computes Relative Strength Index using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RelativeStrengthIndex(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Rate of Change using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeRocFast(StockData data, ComputeContext context, int length = 12)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RateOfChange(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Momentum using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.Momentum(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    #endregion

    #region Volatility

    /// <summary>
    /// Computes Average True Range using zero-allocation fast path.
    /// Uses VolatilityCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeAtrFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;

        // Extract OHLC data into spans
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];

        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }

        var buffer = context.Rent(count);
        VolatilityCore.AverageTrueRange(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Standard Deviation using zero-allocation fast path.
    /// Uses VolatilityCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeStdDevFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        VolatilityCore.StandardDeviation(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    #endregion
}
