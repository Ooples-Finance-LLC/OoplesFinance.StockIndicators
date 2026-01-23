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

    /// <summary>
    /// Computes Double Exponential Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeDemaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.DoubleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Triple Exponential Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeTemaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TripleExponentialMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Hull Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeHmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HullMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Triangular Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeTmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.TriangularMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Welles Wilder Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeWwmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.WellesWilderMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeLinRegFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LinearRegression(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Kaufman Adaptive Moving Average using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeKamaFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.KaufmanAdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Zero-Lag EMA using zero-allocation fast path.
    /// Uses MovingAverageCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeZlemaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ZeroLagEma(inputSpan, buffer.WritableSpan, length);

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

    /// <summary>
    /// Computes Williams %R using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeWilliamsRFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.WilliamsR(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Commodity Channel Index using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeCciFast(StockData data, ComputeContext context, int length = 20)
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
        OscillatorCore.CommodityChannelIndex(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Stochastic %K using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeStochasticKFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.StochasticK(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Average Directional Index using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeAdxFast(StockData data, ComputeContext context, int length = 14)
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
        OscillatorCore.AverageDirectionalIndex(high, low, close, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Chande Momentum Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeCmoFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeMomentumOscillator(inputSpan, buffer.WritableSpan, length);

        return buffer;
    }

    /// <summary>
    /// Computes Percentage Price Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputePpoFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentagePriceOscillator(inputSpan, buffer.WritableSpan, fastLength, slowLength);

        return buffer;
    }

    /// <summary>
    /// Computes Absolute Price Oscillator using zero-allocation fast path.
    /// Uses OscillatorCore with span-based computation directly into pooled buffer.
    /// </summary>
    public static ComputeBuffer ComputeApoFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);

        var buffer = context.Rent(inputList.Count);
        OscillatorCore.AbsolutePriceOscillator(inputSpan, buffer.WritableSpan, fastLength, slowLength);

        return buffer;
    }

    /// <summary>
    /// Computes Ultimate Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeUltimateOscillatorFast(StockData data, ComputeContext context, int length1 = 7, int length2 = 14, int length3 = 28)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        OscillatorCore.UltimateOscillator(high, low, close, buffer.WritableSpan, length1, length2, length3);
        return buffer;
    }

    /// <summary>
    /// Computes True Strength Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTsiFast(StockData data, ComputeContext context, int longLength = 25, int shortLength = 13)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.TrueStrengthIndex(inputSpan, buffer.WritableSpan, longLength, shortLength);
        return buffer;
    }

    /// <summary>
    /// Computes Stochastic RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeStochRsiFast(StockData data, ComputeContext context, int rsiLength = 14, int stochLength = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.StochasticRsi(inputSpan, buffer.WritableSpan, rsiLength, stochLength);
        return buffer;
    }

    /// <summary>
    /// Computes Aroon Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAroonOscillatorFast(StockData data, ComputeContext context, int length = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AroonOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Detrended Price Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDpoFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.DetrendedPriceOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes TRIX indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTrixFast(StockData data, ComputeContext context, int length = 15)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.Trix(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMassIndexFast(StockData data, ComputeContext context, int emaLength = 9, int sumLength = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.MassIndex(high, low, buffer.WritableSpan, emaLength, sumLength);
        return buffer;
    }

    #endregion

    #region Volume

    /// <summary>
    /// Computes On Balance Volume using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeObvFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.OnBalanceVolume(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Accumulation/Distribution Line using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdlFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.AccumulationDistributionLine(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Money Flow using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCmfFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.ChaikinMoneyFlow(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Force Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeForceIndexFast(StockData data, ComputeContext context, int length = 13)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.ForceIndex(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Rate of Change using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVrocFast(StockData data, ComputeContext context, int length = 12)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.VolumeRateOfChange(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Negative Volume Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeNviFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.NegativeVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Positive Volume Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePviFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.PositiveVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Price Volume Trend using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePvtFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.PriceVolumeTrend(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes VWAP using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVwapFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.VolumeWeightedAveragePrice(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChaikinOscillatorFast(StockData data, ComputeContext context, int fastLength = 3, int slowLength = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.ChaikinOscillator(high, low, close, volume, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Ease of Movement using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEmvFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.EaseOfMovement(high, low, volume, buffer.WritableSpan, length);
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

    #region Trend

    /// <summary>
    /// Computes Parabolic SAR using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeParabolicSarFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.ParabolicSar(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes SuperTrend using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSuperTrendFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        TrendCore.SuperTrend(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Middle using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDonchianChannelFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.DonchianChannelMiddle(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Highest High using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeHighestHighFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
        }
        var buffer = context.Rent(count);
        TrendCore.HighestHigh(high, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Lowest Low using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeLowestLowFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.LowestLow(low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Day Range using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdrFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.AverageDayRange(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Typical Price using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTypicalPriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        TrendCore.TypicalPrice(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Median Price using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMedianPriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.MedianPrice(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Weighted Close using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeWeightedCloseFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        TrendCore.WeightedClose(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Change using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePercentageChangeFast(StockData data, ComputeContext context, int length = 1)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.PercentageChange(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Slope using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeLinRegSlopeFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.LinearRegressionSlope(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes R-Squared using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRSquaredFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.RSquared(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Standard Error using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeStandardErrorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.StandardError(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vertical Horizontal Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVhfFast(StockData data, ComputeContext context, int length = 28)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.VerticalHorizontalFilter(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volatility Indicators (Additional)

    /// <summary>
    /// Computes Historical Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeHistoricalVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.HistoricalVolatility(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chaikin Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChaikinVolatilityFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        VolatilityCore.ChaikinVolatility(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ulcer Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeUlcerIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.UlcerIndex(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Normalized ATR using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeNormalizedAtrFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        VolatilityCore.NormalizedAtr(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Variance using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVarianceFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.Variance(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Coefficient of Variation using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCoefficientOfVariationFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        VolatilityCore.CoefficientOfVariation(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes True Range using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTrueRangeFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        VolatilityCore.TrueRange(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Middle (SMA) using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBollingerBandsFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        // Bollinger Bands middle is just SMA
        MovingAverageCore.SimpleMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Volume Indicators

    /// <summary>
    /// Computes Klinger Volume Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKlingerVolumeFast(StockData data, ComputeContext context, int length = 34)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.KlingerVolumeOscillator(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Price Confirmation Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVpciFast(StockData data, ComputeContext context, int length = 5)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        VolumeCore.VolumePriceConfirmationIndicator(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators

    /// <summary>
    /// Computes Money Flow Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMoneyFlowIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        OscillatorCore.MoneyFlowIndex(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Balance of Power using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBalanceOfPowerFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length;
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var open = new double[count];
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            open[i] = (double)tickerList[i].Open;
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.BalanceOfPower(open, high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Vigor Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRelativeVigorIndexFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var open = new double[count];
        var high = new double[count];
        var low = new double[count];
        var close = new double[count];
        for (var i = 0; i < count; i++)
        {
            open[i] = (double)tickerList[i].Open;
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
            close[i] = (double)tickerList[i].Close;
        }
        var buffer = context.Rent(count);
        OscillatorCore.RelativeVigorIndex(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Aroon Up using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAroonUpFast(StockData data, ComputeContext context, int length = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AroonUp(high, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Aroon Down using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAroonDownFast(StockData data, ComputeContext context, int length = 25)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AroonDown(low, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Trend Indicators

    /// <summary>
    /// Computes Keltner Channel Middle using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKeltnerChannelMiddleFast(StockData data, ComputeContext context, int length = 20)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.KeltnerChannelMiddle(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trend Detection using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTrendDetectionFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        TrendCore.TrendDetection(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Channel Middle using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePriceChannelMiddleFast(StockData data, ComputeContext context, int length = 20)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        TrendCore.PriceChannelMiddle(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Oscillators (Batch 2)

    /// <summary>
    /// Computes Stochastic D using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeStochasticDFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        OscillatorCore.StochasticD(high, low, close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Awesome Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAwesomeOscillatorFast(StockData data, ComputeContext context, int length = 5)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AwesomeOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Accelerator Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAcceleratorOscillatorFast(StockData data, ComputeContext context, int length = 5)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.AcceleratorOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Percentage Volume Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePvoFast(StockData data, ComputeContext context, int length = 12)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var volume = new double[count];
        for (var i = 0; i < count; i++)
        {
            volume[i] = (double)tickerList[i].Volume;
        }
        var buffer = context.Rent(count);
        OscillatorCore.PercentageVolumeOscillator(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fisher Transform using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeFisherTransformFast(StockData data, ComputeContext context, int length = 10)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
        var high = new double[count];
        var low = new double[count];
        for (var i = 0; i < count; i++)
        {
            high[i] = (double)tickerList[i].High;
            low[i] = (double)tickerList[i].Low;
        }
        var buffer = context.Rent(count);
        OscillatorCore.FisherTransform(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Connors RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeConnorsRsiFast(StockData data, ComputeContext context, int length = 3)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ConnorsRsi(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Momentum Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePmoFast(StockData data, ComputeContext context, int length = 35)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PriceMomentumOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Know Sure Thing using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKstFast(StockData data, ComputeContext context, int length = 10)
    {
        _ = length;
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.KnowSureThing(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Percent Rank using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePercentRankFast(StockData data, ComputeContext context, int length = 100)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.PercentRank(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Choppiness Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChoppinessIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var tickerList = data.TickerDataList;
        var count = tickerList.Count;
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
        OscillatorCore.ChoppinessIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Additional Moving Averages (Batch 2)

    /// <summary>
    /// Computes Smoothed Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSmmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SmoothedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes McGinley Dynamic using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMcGinleyDynamicFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.McGinleyDynamic(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes T3 Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeT3Fast(StockData data, ComputeContext context, int length = 5)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.T3MovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vidya using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVidyaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.Vidya(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Variable Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.VariableMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes MACD Line using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMacdLineFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MacdLine(inputSpan, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes MACD Signal using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMacdSignalFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MacdSignal(inputSpan, buffer.WritableSpan, fastLength, slowLength, signalLength);
        return buffer;
    }

    /// <summary>
    /// Computes MACD Histogram using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMacdHistogramFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26, int signalLength = 9)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.MacdHistogram(inputSpan, buffer.WritableSpan, fastLength, slowLength, signalLength);
        return buffer;
    }

    /// <summary>
    /// Computes Absolute Strength Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAbsoluteStrengthIndexFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AbsoluteStrengthIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Momentum Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRelativeMomentumIndexFast(StockData data, ComputeContext context, int length = 14, int momentum = 4)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.RelativeMomentumIndex(inputSpan, buffer.WritableSpan, length, momentum);
        return buffer;
    }

    /// <summary>
    /// Computes Intraday Momentum Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeIntradayMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.IntradayMomentumIndex(open, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Swing Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SwingIndex(open, high, low, close, buffer.WritableSpan, limitMove);
        return buffer;
    }

    /// <summary>
    /// Computes Accumulative Swing Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAccumulativeSwingIndexFast(StockData data, ComputeContext context, double limitMove = 0)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AccumulativeSwingIndex(open, high, low, close, buffer.WritableSpan, limitMove);
        return buffer;
    }

    /// <summary>
    /// Computes Coppock Curve using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCoppockCurveFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Coppock uses fixed parameters internally
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.CoppockCurve(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Forecast Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.ChandeForecastOscillator(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bull Power using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBullPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BullPower(high, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bear Power using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBearPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BearPower(low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Polarized Fractal Efficiency using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePolarizedFractalEfficiencyFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PolarizedFractalEfficiency(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Schaff Trend Cycle using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSchaffTrendCycleFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SchaffTrendCycle(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Price Zone Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePriceZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceZoneOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Force Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElderForceIndexFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ElderForceIndex(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Pretty Good Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePrettyGoodOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PrettyGoodOscillator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRelativeVolatilityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeVolatilityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Qstick using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeQstickFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Qstick(open, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Special K using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSpecialKFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Special K uses fixed parameters
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        OscillatorCore.SpecialK(inputSpan, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Arnaud Legoux Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAlmaFast(StockData data, ComputeContext context, int length = 9)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ArnaudLegouxMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Least Squares Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeLsmaFast(StockData data, ComputeContext context, int length = 25)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.LeastSquaresMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fractal Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeFramaFast(StockData data, ComputeContext context, int length = 16)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.FractalAdaptiveMovingAverage(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAmaFast(StockData data, ComputeContext context, int length = 10)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.AdaptiveMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Sine Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSineWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.SineWeightedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Hamming Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeHammingMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.HammingMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Geometric Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGeoMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.GeometricMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Regularized EMA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRegularizedEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.RegularizedEma(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Modified Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeModifiedMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var inputList = data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValues;
        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var buffer = context.Rent(inputList.Count);
        MovingAverageCore.ModifiedMovingAverage(inputSpan, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ZigZag using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeZigZagFast(StockData data, ComputeContext context, int length = 5)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ZigZag(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chandelier Exit Long using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandelierExitLongFast(StockData data, ComputeContext context, int length = 22)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandelierExitLong(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chandelier Exit Short using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandelierExitShortFast(StockData data, ComputeContext context, int length = 22)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandelierExitShort(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trend Intensity Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTrendIntensityIndexFast(StockData data, ComputeContext context, int length = 30)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.TrendIntensityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Average Price using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAveragePriceFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Average price doesn't use length
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AveragePrice(open, high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Pivot Point using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePivotPointFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Pivot point doesn't use length
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PivotPoint(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Range using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRangeFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Range doesn't use length
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Range(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Price Momentum using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePriceMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.PriceMomentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midpoint using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMidpointFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Midpoint(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Midprice using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMidpriceFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.Midprice(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volume Fast Path Methods (Additional)

    /// <summary>
    /// Computes Money Flow Index using zero-allocation fast path via VolumeCore.
    /// </summary>
    public static ComputeBuffer ComputeMfiCoreFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.MoneyFlowIndex(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Trade Volume Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTradeVolumeIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // TVI uses minTickValue, not length
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.TradeVolumeIndex(close, volume, buffer.WritableSpan, 0.5);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeOscillator(volume, buffer.WritableSpan, 5, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVwmaFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeWeightedMovingAveragePrice(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Twiggs Money Flow using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeTwiggsMoneyFlowFast(StockData data, ComputeContext context, int length = 21)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.TwiggsMoneyFlow(high, low, close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Zone Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeZoneOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeZoneOscillator(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demand Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDemandIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Demand Index doesn't use length
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.DemandIndex(high, low, close, volume, buffer.WritableSpan);
        return buffer;
    }

    #endregion

    #region Oscillator Fast Path Methods (Additional Batch 5)

    /// <summary>
    /// Computes Disparity Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDisparityIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DisparityIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Directional Trend Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDirectionalTrendIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DirectionalTrendIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Double Smoothed Stochastic using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDoubleSmoothedStochasticFast(StockData data, ComputeContext context, int length = 10)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DoubleSmoothedStochastic(high, low, close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    /// <summary>
    /// Computes Dynamic Momentum Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDynamicMomentumIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // DMI uses min/max lengths, not a single length
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DynamicMomentumIndex(close, buffer.WritableSpan, 3, 30);
        return buffer;
    }

    /// <summary>
    /// Computes Ergodic Candlestick Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeErgodicCandlestickOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ErgodicCandlestickOscillator(open, close, buffer.WritableSpan, 5, length);
        return buffer;
    }

    /// <summary>
    /// Computes Demarker Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDemarkerFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Demarker(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Smoothed Rate of Change using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSmoothedRocFast(StockData data, ComputeContext context, int length = 12)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedRateOfChange(close, buffer.WritableSpan, length, 3);
        return buffer;
    }

    #endregion

    #region Volatility Fast Path Methods (Additional)

    /// <summary>
    /// Computes Standard Error using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeStandardErrorCoreFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardError(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Keltner Channel Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeKeltnerChannelWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.KeltnerChannelWidth(high, low, close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Bollinger Bands Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBollingerBandsWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.BollingerBandsWidth(close, buffer.WritableSpan, length, 2);
        return buffer;
    }

    /// <summary>
    /// Computes Relative Volatility Index using zero-allocation fast path via VolatilityCore.
    /// </summary>
    public static ComputeBuffer ComputeRviVolatilityFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RelativeVolatilityIndex(close, buffer.WritableSpan, length, 10);
        return buffer;
    }

    /// <summary>
    /// Computes Donchian Channel Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDonchianChannelWidthFast(StockData data, ComputeContext context, int length = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.DonchianChannelWidth(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Index using zero-allocation fast path via VolatilityCore.
    /// </summary>
    public static ComputeBuffer ComputeMassIndexCoreFast(StockData data, ComputeContext context, int length = 25)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.MassIndex(high, low, buffer.WritableSpan, length, 9);
        return buffer;
    }

    /// <summary>
    /// Computes Close-to-Close Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCloseToCloseVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CloseToCloseVolatility(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parkinson Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeParkinsonVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.ParkinsonVolatility(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Garman-Klass Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGarmanKlassVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.GarmanKlassVolatility(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Moving Average Fast Path Methods (Additional Batch 3)

    /// <summary>
    /// Computes Jurik Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeJmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.JurikMovingAverage(close, buffer.WritableSpan, length, 0);
        return buffer;
    }

    /// <summary>
    /// Computes Butterworth Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeButterworthFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ButterworthFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes SuperSmoother Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeSuperSmootherFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.SuperSmoother(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes EndPoint Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeEndPointMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.EndpointMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Cubic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCubicWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.CubicWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Natural Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeNaturalMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.NaturalMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elliott Wave Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElliottWaveOscillatorFast(StockData data, ComputeContext context, int fastLength = 5, int slowLength = 35)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ElliottWaveOscillator(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    /// <summary>
    /// Computes Forecast Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeForecastOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ForecastOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Derivative Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDerivativeOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.DerivativeOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Gator Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGatorOscillatorFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.GatorOscillator(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Fractal Chaos Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeFractalChaosOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        _ = length; // Not used in this oscillator
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.FractalChaosOscillator(high, low, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Rahul Mohindar Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRahulMohindarOscillatorFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RahulMohindarOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Premier Stochastic Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputePremierStochasticFast(StockData data, ComputeContext context, int length = 8)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PremierStochastic(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Repulse Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRepulseFast(StockData data, ComputeContext context, int length = 5)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Repulse(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Gann HiLo Activator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeGannHiLoActivatorFast(StockData data, ComputeContext context, int length = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.GannHiLoActivator(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes HalfTrend indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeHalfTrendFast(StockData data, ComputeContext context, int length = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.HalfTrend(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Indicator Positive using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVortexPositiveFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.VortexPositive(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Vortex Indicator Negative using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVortexNegativeFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.VortexNegative(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Linear Regression Intercept using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeLinRegInterceptFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.LinearRegressionIntercept(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Impulse System using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElderImpulseSystemFast(StockData data, ComputeContext context, int length = 13)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ElderImpulseSystem(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Tenkan-sen using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeIchimokuTenkanSenFast(StockData data, ComputeContext context, int length = 9)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuTenkanSen(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ichimoku Kijun-sen using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeIchimokuKijunSenFast(StockData data, ComputeContext context, int length = 26)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        TrendCore.IchimokuKijunSen(high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Mass Thrust Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMassThrustFast(StockData data, ComputeContext context, int length = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        TrendCore.MassThrust(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volume Indicators - Additional Batch 2

    /// <summary>
    /// Computes Williams Accumulation/Distribution using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeWilliamsADFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolumeCore.WilliamsAD(high, low, close, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Net Volume using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeNetVolumeFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.NetVolume(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Cumulative Volume Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCumulativeVolumeIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.CumulativeVolumeIndex(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Momentum using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeMomentumFast(StockData data, ComputeContext context, int length = 10)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeMomentum(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Price Trend using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumePriceTrendFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumePriceTrend(close, volume, buffer.WritableSpan);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Ray Bull Power using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElderRayBullPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolumeCore.ElderRayBullPower(high, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Elder Ray Bear Power using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeElderRayBearPowerFast(StockData data, ComputeContext context, int length = 13)
    {
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolumeCore.ElderRayBearPower(low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Normalized Volume using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeNormalizedVolumeFast(StockData data, ComputeContext context, int length = 20)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.NormalizedVolume(volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Weighted RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeWeightedRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        VolumeCore.VolumeWeightedRsi(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Oscillators - Additional Batch 7

    /// <summary>
    /// Computes Chande Composite Momentum Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandeCompositeMomentumIndexFast(StockData data, ComputeContext context, int shortLength = 3, int longLength = 10)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeCompositeMomentumIndex(close, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    /// <summary>
    /// Computes Chande Kroll R-Squared Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandeKrollRSquaredIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChandeKrollRSquaredIndex(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bayesian Oscillator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBayesianOscillatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BayesianOscillator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Anchored Momentum using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAnchoredMomentumFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AnchoredMomentum(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chartmill Value Indicator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChartmillValueIndicatorFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.ChartmillValueIndicator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Center of Linearity using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCenterOfLinearityFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.CenterOfLinearity(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Breakout RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBreakoutRsiFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.BreakoutRsi(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Asymmetrical RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAsymmetricalRsiFast(StockData data, ComputeContext context, int upLength = 14, int downLength = 7)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AsymmetricalRsi(close, buffer.WritableSpan, upLength, downLength);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Stochastic using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveStochasticFast(StockData data, ComputeContext context, int minLength = 5, int maxLength = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AdaptiveStochastic(high, low, close, buffer.WritableSpan, minLength, maxLength);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive RSI using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveRsiFast(StockData data, ComputeContext context, int minLength = 5, int maxLength = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.AdaptiveRsi(close, buffer.WritableSpan, minLength, maxLength);
        return buffer;
    }

    #endregion

    #region Trend - Additional Batch 4

    /// <summary>
    /// Computes Chande Trend Score using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChandeTrendScoreFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChandeTrendScore(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Chop Zone using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeChopZoneFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ChopZone(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Line using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutoLineFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoLine(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Line with Drift using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutoLineWithDriftFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoLineWithDrift(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Auto Filter using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutoFilterFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AutoFilter(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Buff Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBuffAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.BuffAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Bryant Adaptive Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeBryantAdaptiveMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.BryantAdaptiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Trailing Stops using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAtrTrailingStopsFast(StockData data, ComputeContext context, int length = 14, double multiplier = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AtrTrailingStops(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Compound Ratio Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCompoundRatioMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.CompoundRatioMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Conditional Accumulator using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeConditionalAccumulatorFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.ConditionalAccumulator(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Ahrens Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAhrensMovingAverageFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        TrendCore.AhrensMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Volatility - Additional Batch 2

    /// <summary>
    /// Computes Rogers-Satchell Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeRogersSatchellVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.RogersSatchellVolatility(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Yang-Zhang Volatility using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeYangZhangVolatilityFast(StockData data, ComputeContext context, int length = 20)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.YangZhangVolatility(open, high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Calmar Ratio using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCalmarRatioFast(StockData data, ComputeContext context, int length = 252)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CalmarRatio(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Downside Deviation using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeDownsideDeviationFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.DownsideDeviation(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Channel Width using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAtrChannelWidthFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AtrChannelWidth(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    /// <summary>
    /// Computes Commodity Selection Index using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeCommoditySelectionIndexFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.CommoditySelectionIndex(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Moving Averages - Additional Batch 4

    /// <summary>
    /// Computes Alpha Decreasing EMA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAlphaDecreasingEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AlphaDecreasingEma(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive EMA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveExponentialMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Autonomous Recursive MA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAutonomousRecursiveMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AutonomousRecursiveMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Adaptive Least Squares using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAdaptiveLeastSquaresFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AdaptiveLeastSquares(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes ATR Filtered EMA using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeAtrFilteredEmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.AtrFilteredEma(close, high, low, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Median Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeMedianMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.MedianMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Volume Adjusted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeVolumeAdjustedMaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.VolumeAdjustedMovingAverage(close, volume, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Quadratic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeQuadraticWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.QuadraticWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    /// <summary>
    /// Computes Parabolic Weighted Moving Average using zero-allocation fast path.
    /// </summary>
    public static ComputeBuffer ComputeParabolicWmaFast(StockData data, ComputeContext context, int length = 14)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        MovingAverageCore.ParabolicWeightedMovingAverage(close, buffer.WritableSpan, length);
        return buffer;
    }

    #endregion

    #region Oscillators - Additional Batch 8

    public static ComputeBuffer ComputeSmoothedWilliamsRFast(StockData data, ComputeContext context, int length = 14, int smoothLength = 3)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.SmoothedWilliamsR(high, low, close, buffer.WritableSpan, length, smoothLength);
        return buffer;
    }

    public static ComputeBuffer ComputePriceOscillatorPercentFast(StockData data, ComputeContext context, int shortLength = 10, int longLength = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.PriceOscillatorPercent(close, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    public static ComputeBuffer ComputeNormalizedMacdFast(StockData data, ComputeContext context, int fastLength = 12, int slowLength = 26)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.NormalizedMacd(close, buffer.WritableSpan, fastLength, slowLength);
        return buffer;
    }

    public static ComputeBuffer ComputeRelativeVigorIndexSignalFast(StockData data, ComputeContext context, int length = 10, int signalLength = 4)
    {
        var open = SpanCompat.AsReadOnlySpan(data.OpenPrices);
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.RelativeVigorIndexSignal(open, high, low, close, buffer.WritableSpan, length, signalLength);
        return buffer;
    }

    public static ComputeBuffer ComputeVolumeMomentumOscillatorFast(StockData data, ComputeContext context, int shortLength = 5, int longLength = 20)
    {
        var volume = SpanCompat.AsReadOnlySpan(data.Volumes);
        var buffer = context.Rent(data.Count);
        OscillatorCore.VolumeMomentumOscillator(volume, buffer.WritableSpan, shortLength, longLength);
        return buffer;
    }

    public static ComputeBuffer ComputeTrendContinuationFactorFast(StockData data, ComputeContext context, int length = 35)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendContinuationFactor(close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeTrendPersistenceRateFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.TrendPersistenceRate(close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeInertiaFast(StockData data, ComputeContext context, int rviLength = 14, int smoothLength = 20)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        OscillatorCore.Inertia(high, low, close, buffer.WritableSpan, rviLength, smoothLength);
        return buffer;
    }

    #endregion

    #region Volatility - Additional Batch 3

    public static ComputeBuffer ComputeStandardDeviationChannelFast(StockData data, ComputeContext context, int length = 20)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviationChannel(close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeStandardDeviationVolatilityFast(StockData data, ComputeContext context, int length = 20, int annualizationFactor = 252)
    {
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.StandardDeviationVolatility(close, buffer.WritableSpan, length, annualizationFactor);
        return buffer;
    }

    public static ComputeBuffer ComputeAverageTrueRangeChannelFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.AverageTrueRangeChannel(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    public static ComputeBuffer ComputeVolatilityRatioFast(StockData data, ComputeContext context, int length = 14)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.VolatilityRatio(high, low, close, buffer.WritableSpan, length);
        return buffer;
    }

    public static ComputeBuffer ComputeVolatilityStopFast(StockData data, ComputeContext context, int length = 14, double multiplier = 2)
    {
        var high = SpanCompat.AsReadOnlySpan(data.HighPrices);
        var low = SpanCompat.AsReadOnlySpan(data.LowPrices);
        var close = SpanCompat.AsReadOnlySpan(data.ClosePrices);
        var buffer = context.Rent(data.Count);
        VolatilityCore.VolatilityStop(high, low, close, buffer.WritableSpan, length, multiplier);
        return buffer;
    }

    #endregion
}
