#pragma warning disable CS0618 // Suppress obsolete warnings for internal Calculate* method calls
using OoplesFinance.StockIndicators.Builder.Specs;
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
            SmaSpecOptions sma => ComputeSma(data, context, sma.Length),
            EmaSpecOptions ema => ComputeEma(data, context, ema.Length),
            RsiSpecOptions rsi => ComputeRsi(data, context, rsi.Length),
            AtrSpecOptions atr => ComputeAtr(data, context, atr.Length),
            AdxSpecOptions adx => ComputeAdx(data, context, adx.Length),
            _ => null
        };
    }

    /// <summary>
    /// Computes Simple Moving Average.
    /// </summary>
    public static ComputeBuffer ComputeSma(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateSimpleMovingAverage(length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Exponential Moving Average.
    /// </summary>
    public static ComputeBuffer ComputeEma(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateExponentialMovingAverage(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Relative Strength Index.
    /// </summary>
    public static ComputeBuffer ComputeRsi(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateRelativeStrengthIndex(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Average True Range.
    /// </summary>
    public static ComputeBuffer ComputeAtr(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateAverageTrueRange(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Average Directional Index.
    /// </summary>
    public static ComputeBuffer ComputeAdx(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateAverageDirectionalIndex(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Weighted Moving Average.
    /// </summary>
    public static ComputeBuffer ComputeWma(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateWeightedMovingAverage(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Hull Moving Average.
    /// </summary>
    public static ComputeBuffer ComputeHma(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateHullMovingAverage(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Triple Exponential Moving Average (TEMA).
    /// </summary>
    public static ComputeBuffer ComputeTema(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateTripleExponentialMovingAverage(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Double Exponential Moving Average (DEMA).
    /// </summary>
    public static ComputeBuffer ComputeDema(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateDoubleExponentialMovingAverage(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Commodity Channel Index.
    /// </summary>
    public static ComputeBuffer ComputeCci(StockData data, ComputeContext context, int length = 20)
    {
        var result = data.CalculateCommodityChannelIndex(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Williams %R.
    /// </summary>
    public static ComputeBuffer ComputeWilliamsR(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateWilliamsR(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Rate of Change.
    /// </summary>
    public static ComputeBuffer ComputeRoc(StockData data, ComputeContext context, int length = 12)
    {
        var result = data.CalculateRateOfChange(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Momentum Oscillator.
    /// </summary>
    public static ComputeBuffer ComputeMomentum(StockData data, ComputeContext context, int length = 10)
    {
        var result = data.CalculateMomentumOscillator(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Standard Deviation Volatility.
    /// </summary>
    public static ComputeBuffer ComputeStdDev(StockData data, ComputeContext context, int length = 20)
    {
        var result = data.CalculateStandardDeviationVolatility(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes On Balance Volume.
    /// </summary>
    public static ComputeBuffer ComputeObv(StockData data, ComputeContext context)
    {
        var result = data.CalculateOnBalanceVolume();
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes Money Flow Index.
    /// </summary>
    public static ComputeBuffer ComputeMfi(StockData data, ComputeContext context, int length = 14)
    {
        var result = data.CalculateMoneyFlowIndex(length: length);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes True Strength Index.
    /// </summary>
    public static ComputeBuffer ComputeTsi(StockData data, ComputeContext context, int length1 = 25, int length2 = 13)
    {
        var result = data.CalculateTrueStrengthIndex(length1: length1, length2: length2);
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Computes VWAP (Volume Weighted Average Price).
    /// </summary>
    public static ComputeBuffer ComputeVwap(StockData data, ComputeContext context)
    {
        var result = data.CalculateVolumeWeightedAveragePrice();
        return ExtractToBuffer(result, context);
    }

    /// <summary>
    /// Extracts indicator output to a pooled buffer.
    /// </summary>
    /// <param name="result">The StockData containing computation results.</param>
    /// <param name="context">The compute context for buffer management.</param>
    /// <returns>A ComputeBuffer containing the indicator values.</returns>
    private static ComputeBuffer ExtractToBuffer(StockData result, ComputeContext context)
    {
        var values = result.CustomValuesList;
        if (values.Count == 0)
        {
            return context.Rent(0);
        }

        var buffer = context.Rent(values.Count);
        var span = buffer.WritableSpan;

        // Use span-based copy for performance
        for (int i = 0; i < values.Count; i++)
        {
            span[i] = values[i];
        }

        return buffer;
    }

    /// <summary>
    /// Extracts a specific output key from indicator results to a pooled buffer.
    /// </summary>
    /// <param name="result">The StockData containing computation results.</param>
    /// <param name="context">The compute context for buffer management.</param>
    /// <param name="outputKey">The output key to extract (e.g., "Signal", "UpperBand").</param>
    /// <returns>A ComputeBuffer containing the indicator values, or an empty buffer if key not found.</returns>
    private static ComputeBuffer ExtractToBuffer(StockData result, ComputeContext context, string outputKey)
    {
        if (result.OutputValues.TryGetValue(outputKey, out var values) && values.Count > 0)
        {
            var buffer = context.Rent(values.Count);
            var span = buffer.WritableSpan;

            for (int i = 0; i < values.Count; i++)
            {
                span[i] = values[i];
            }

            return buffer;
        }

        // Fall back to CustomValuesList
        return ExtractToBuffer(result, context);
    }
}
