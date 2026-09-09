//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>
/// Derived series - moving averages, dispersion, true range - computed from an explicit
/// <see cref="IndicatorSource"/> instead of from ambient state on a <see cref="StockData"/>.
/// </summary>
/// <remarks>
/// <para>
/// The existing entry points take their input series from the <see cref="StockData"/> they are handed:
/// <c>CalculateStandardDeviationVolatility(stockData, ...)</c> and
/// <c>CalculateAverageTrueRange(stockData, ...)</c> both begin with <c>GetInputValuesList(stockData)</c>,
/// which returns <see cref="StockData.CustomValuesList"/> when it is non-empty. A moving average
/// computed earlier in the same method writes that property, so the dispersion that follows is measured
/// against the average rather than against price. Issue #145 lists twenty-three indicators where that
/// happens and twenty-one more that carry a hand-written guard against it.
/// </para>
/// <para>
/// The methods here take the series as a parameter, in the same shape
/// <see cref="MovingAverageCore"/> and <c>GetMovingAverageList</c> already use. There is nothing for a
/// caller to reset, and reordering two calls cannot change what either one reads.
/// </para>
/// <para>
/// Where a moving average type has no span-based core, the work is delegated through a
/// <see cref="StockData.WithValues"/> view rather than the caller's own object, so the caller's
/// <see cref="StockData.CustomValuesList"/> is never written.
/// </para>
/// </remarks>
internal static class IndicatorMath
{
    /// <summary>
    /// Computes a moving average over <paramref name="values"/> without touching
    /// <paramref name="context"/>'s custom values.
    /// </summary>
    /// <param name="context">Supplies the bars for moving average types that need them.</param>
    /// <param name="values">The series to average.</param>
    /// <param name="maType">The moving average type.</param>
    /// <param name="length">The averaging length.</param>
    internal static List<double> MovingAverage(StockData context, IReadOnlyList<double> values,
        MovingAvgType maType, int length)
    {
        var list = AsList(values);

        // GetMovingAverageList writes CustomValuesList on whatever StockData it is given, because the
        // slow path resolves its input from that property. Give it a view, so the write lands on a
        // throwaway object and the caller's series is left alone.
        return GetMovingAverageList(context.WithValues(list), maType, length, list);
    }

    /// <inheritdoc cref="MovingAverage(StockData, IReadOnlyList{double}, MovingAvgType, int)"/>
    internal static List<double> MovingAverage(StockData context, in IndicatorSource source,
        MovingAvgType maType, int length) =>
        MovingAverage(context, source.Values, maType, length);

    /// <summary>
    /// Computes the true range of each bar in <paramref name="source"/>.
    /// </summary>
    /// <remarks>
    /// The first bar has no predecessor, so its previous close is taken from the bar itself, making its
    /// true range <c>high - low</c>. This matches <c>BuildDerivedSeriesList</c>, and is the rule the
    /// streaming states were corrected to in issue #146.
    /// </remarks>
    internal static List<double> TrueRange(in IndicatorSource source)
    {
        var count = source.Count;

        // BuildDerivedSeriesList reads stockData.HighPrices/LowPrices directly, never the substituted
        // highs and lows that GetInputValuesList returns. Use the bar series here so this stays a
        // faithful port; see IndicatorSource.BarHigh for why the two differ.
        var highs = source.BarHigh;
        var lows = source.BarLow;
        var closes = source.Values;
        var list = new List<double>(count);

        for (var i = 0; i < count; i++)
        {
            var prevClose = i >= 1 ? closes[i - 1] : closes[i];
            list.Add(CalculateTrueRange(highs[i], lows[i], prevClose));
        }

        return list;
    }

    /// <summary>
    /// Computes the average true range of <paramref name="source"/>, smoothed by
    /// <paramref name="maType"/>.
    /// </summary>
    internal static List<double> AverageTrueRange(StockData context, in IndicatorSource source,
        MovingAvgType maType, int length)
    {
        var trueRangeList = TrueRange(source);

        if (maType == MovingAvgType.WildersSmoothingMethod)
        {
            var trSpan = SpanCompat.AsReadOnlySpan(trueRangeList);
            var buffer = SpanCompat.CreateOutputBuffer(trueRangeList.Count);
            MovingAverageCore.WellesWilderMovingAverage(trSpan, buffer.Span, length);

            return buffer.ToList();
        }

        return MovingAverage(context, trueRangeList, maType, length);
    }

    /// <summary>
    /// Computes the rolling standard deviation of <paramref name="values"/> over a window of
    /// <paramref name="length"/> bars - the population standard deviation about each window's own mean.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the statistic Bollinger Bands, Keltner width and every other dispersion band are defined
    /// against, and the one TA-Lib's <c>TA_STDDEV</c> computes:
    /// </para>
    /// <code>
    /// var[i] = (1/n) * sum over k in the window of (x[k] - mean[i])^2
    /// </code>
    /// <para>
    /// It is <b>not</b> what <c>CalculateStandardDeviationVolatility</c> produces. That method squares
    /// each bar's deviation from <i>its own</i> contemporaneous moving average and then averages those:
    /// </para>
    /// <code>
    /// var[i] = (1/n) * sum over k in the window of (x[k] - sma[k])^2
    /// </code>
    /// <para>
    /// which is the mean squared residual from the moving-average line, a larger number whenever price
    /// is trending. On the 200-bar AAPL fixture the two disagree by up to 11.0 on the upper band, and
    /// only 0.6% of bars fall outside the resulting two-sigma envelope where a correct one leaves about
    /// 10% outside. Warm-up follows <see cref="MovingAverageCore.SimpleMovingAverage"/> and emits zero
    /// until the window is full.
    /// </para>
    /// </remarks>
    internal static List<double> RollingStandardDeviation(IReadOnlyList<double> values, int length)
    {
        var count = values.Count;
        var result = new List<double>(count);

        if (length < 1)
        {
            length = 1;
        }

        // The window mean comes from a rolling sum, which is well conditioned. The variance does not:
        // computing it as sumOfSquares/n - mean^2 subtracts two nearly equal large numbers, and on a
        // price series that is catastrophic cancellation. Prices near 150 with a spread near 6 give
        // both terms around 23,000 and a difference around 36, so roughly three significant digits are
        // lost immediately - and the running sum of squares keeps drifting as values are added and
        // removed across a long series. Measured against a two-pass reference on the full AAPL fixture
        // the one-pass form was already off by 1.2e-7 by bar 121.
        //
        // So the squared deviations are summed directly over the window. That is O(n * length) rather
        // than O(n), which for the lengths indicators actually use is not a cost worth trading accuracy
        // for. TA-Lib's TA_STDDEV takes the one-pass route and carries the same drift.
        double windowSum = 0;

        for (var i = 0; i < count; i++)
        {
            windowSum += values[i];

            if (i >= length)
            {
                windowSum -= values[i - length];
            }

            if (i < length - 1)
            {
                result.Add(0);
                continue;
            }

            var mean = windowSum / length;
            double sumOfSquaredDeviations = 0;
            for (var k = i - length + 1; k <= i; k++)
            {
                var deviation = values[k] - mean;
                sumOfSquaredDeviations += deviation * deviation;
            }

            var variance = sumOfSquaredDeviations / length;
            result.Add(variance > 0 ? Sqrt(variance) : 0);
        }

        return result;
    }

    /// <summary>
    /// Computes the rolling standard deviation of <paramref name="values"/>.
    /// </summary>
    /// <remarks>
    /// This is the dispersion of the series it is given. Whether that series should be price or a
    /// moving average of price is the caller's decision, made visibly at the call site - which is the
    /// whole point of taking it as a parameter.
    /// </remarks>
    internal static List<double> StandardDeviation(StockData context, IReadOnlyList<double> values,
        MovingAvgType maType, int length)
    {
        var count = values.Count;

        if (maType == MovingAvgType.SimpleMovingAverage)
        {
            var inputSpan = SpanCompat.AsReadOnlySpan(AsList(values));
            var smaBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.SimpleMovingAverage(inputSpan, smaBuffer.Span, length);

            var deviationSquared = new double[count];
            for (var i = 0; i < count; i++)
            {
                var deviation = values[i] - smaBuffer.Span[i];
                deviationSquared[i] = Pow(deviation, 2);
            }

            var varianceBuffer = SpanCompat.CreateOutputBuffer(count);
            MovingAverageCore.SimpleMovingAverage(deviationSquared, varianceBuffer.Span, length);

            var stdDevList = new List<double>(count);
            for (var i = 0; i < count; i++)
            {
                stdDevList.Add(Sqrt(varianceBuffer.Span[i]));
            }

            return stdDevList;
        }

        var smaList = MovingAverage(context, values, maType, length);
        var deviationSquaredList = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            deviationSquaredList.Add(Pow(values[i] - smaList[i], 2));
        }

        var varianceList = MovingAverage(context, deviationSquaredList, maType, length);
        var result = new List<double>(count);
        for (var i = 0; i < count; i++)
        {
            result.Add(Sqrt(varianceList[i]));
        }

        return result;
    }

    /// <inheritdoc cref="StandardDeviation(StockData, IReadOnlyList{double}, MovingAvgType, int)"/>
    internal static List<double> StandardDeviation(StockData context, in IndicatorSource source,
        MovingAvgType maType, int length) =>
        StandardDeviation(context, source.Values, maType, length);

    private static List<double> AsList(IReadOnlyList<double> values) =>
        values as List<double> ?? new List<double>(values);
}
