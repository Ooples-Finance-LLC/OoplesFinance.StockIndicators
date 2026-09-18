
using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Elder Impulse System.
    /// </summary>
    /// <remarks>
    /// Elder's traffic light: a one when both the exponential average and the convergence and divergence
    /// histogram are rising, so trend and momentum agree upwards, a minus one when both are falling, and a
    /// zero when they disagree and the system says to stand aside. The reading is a verdict rather than a
    /// measurement, so it takes only those three values.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="macdFastLength"></param>
    /// <param name="macdSlowLength"></param>
    /// <param name="macdSignalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElderImpulseSystem(this StockData stockData, int length = 13, int macdFastLength = 12,
        int macdSlowLength = 26, int macdSignalLength = 9)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> impulseList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var emaBuffer = SpanCompat.CreateOutputBuffer(count);
        var histogramBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.ExponentialMovingAverage(inputSpan, emaBuffer.Span, length);
        OscillatorCore.MacdHistogram(inputSpan, histogramBuffer.Span, macdFastLength, macdSlowLength, macdSignalLength);

        for (var i = 0; i < count; i++)
        {
            double impulse = 0;
            if (i >= 1)
            {
                var emaRising = emaBuffer.Span[i] > emaBuffer.Span[i - 1];
                var histogramRising = histogramBuffer.Span[i] > histogramBuffer.Span[i - 1];
                impulse = emaRising && histogramRising ? 1 : !emaRising && !histogramRising ? -1 : 0;
            }

            impulseList.Add(impulse);

            var prevImpulse = i >= 1 ? impulseList[i - 1] : 0;
            var signal = GetCompareSignal(impulse, prevImpulse);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eis", impulseList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(impulseList);
        stockData.IndicatorName = IndicatorName.ElderImpulseSystem;

        return stockData;
    }

    /// <summary>
    /// Calculates the Cumulative Sum of the input series.
    /// </summary>
    /// <remarks>
    /// The running total: each bar adds its own value to the total of every bar before it. It has no length,
    /// so it never warms up - the first bar's total is its own value.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCumulativeSum(this StockData stockData)
    {
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> cumulativeSumList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        double sum = 0;
        for (var i = 0; i < count; i++)
        {
            sum += inputList[i];
            cumulativeSumList.Add(sum);

            var prevSum1 = i >= 1 ? cumulativeSumList[i - 1] : 0;
            var prevSum2 = i >= 2 ? cumulativeSumList[i - 2] : 0;
            var signal = GetCompareSignal(sum - prevSum1, prevSum1 - prevSum2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "CumulativeSum", cumulativeSumList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cumulativeSumList);
        stockData.IndicatorName = IndicatorName.CumulativeSum;

        return stockData;
    }

    /// <summary>
    /// Calculates the Coral Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="cd"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateCoralTrendIndicator(this StockData stockData, int length = 21, double cd = 0.4)
    {
        List<double> i1List = new(stockData.Count);
        List<double> i2List = new(stockData.Count);
        List<double> i3List = new(stockData.Count);
        List<double> i4List = new(stockData.Count);
        List<double> i5List = new(stockData.Count);
        List<double> i6List = new(stockData.Count);
        List<double> bfrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var di = ((double)(length - 1) / 2) + 1;
        var c1 = 2 / (di + 1);
        var c2 = 1 - c1;
        var c3 = 3 * ((cd * cd) + (cd * cd * cd));
        var c4 = -3 * ((2 * cd * cd) + cd + (cd * cd * cd));
        var c5 = (3 * cd) + 1 + (cd * cd * cd) + (3 * cd * cd);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevI1 = i >= 1 ? i1List[i - 1] : 0;
            var i1 = (c1 * currentValue) + (c2 * prevI1);
            i1List.Add(i1);

            var prevI2 = i >= 1 ? i2List[i - 1] : 0;
            var i2 = (c1 * i1) + (c2 * prevI2);
            i2List.Add(i2);

            var prevI3 = i >= 1 ? i3List[i - 1] : 0;
            var i3 = (c1 * i2) + (c2 * prevI3);
            i3List.Add(i3);

            var prevI4 = i >= 1 ? i4List[i - 1] : 0;
            var i4 = (c1 * i3) + (c2 * prevI4);
            i4List.Add(i4);

            var prevI5 = i >= 1 ? i5List[i - 1] : 0;
            var i5 = (c1 * i4) + (c2 * prevI5);
            i5List.Add(i5);

            var prevI6 = i >= 1 ? i6List[i - 1] : 0;
            var i6 = (c1 * i5) + (c2 * prevI6);
            i6List.Add(i6);

            var prevBfr = i >= 1 ? bfrList[i - 1] : 0;
            var bfr = (-1 * cd * cd * cd * i6) + (c3 * i5) + (c4 * i4) + (c5 * i3);
            bfrList.Add(bfr);

            var signal = GetCompareSignal(currentValue - bfr, prevValue - prevBfr);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cti", bfrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bfrList);
        stockData.IndicatorName = IndicatorName.CoralTrendIndicator;

        return stockData;
    }

}

