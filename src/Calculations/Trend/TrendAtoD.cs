
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
                var emaFalling = emaBuffer.Span[i] < emaBuffer.Span[i - 1];
                var histogramFalling = histogramBuffer.Span[i] < histogramBuffer.Span[i - 1];
                impulse = emaRising && histogramRising ? 1 : emaFalling && histogramFalling ? -1 : 0;
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

        var total = new ExactMeanAccumulator();
        for (var i = 0; i < count; i++)
        {
            total.Add(inputList[i]);
            var sum = total.Mean(1);
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
        var window = new CoralTrendWindow(length, cd); var (input, _, _, _, _) = GetInputValuesList(stockData); var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        foreach (var price in input) { var point = window.Next(price, true); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Cti", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.CoralTrendIndicator; return stockData;

    }

}

