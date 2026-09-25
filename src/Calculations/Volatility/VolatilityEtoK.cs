using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Keltner Channel Width.
    /// </summary>
    /// <remarks>
    /// The distance between a Keltner channel's bands as a percentage of the exponential average they are
    /// drawn about, which is how the room the channel gives is compared between instruments of different
    /// price. The average is always exponential here, whatever average a caller may have in mind, and the
    /// range is the one <see cref="CalculateAverageTrueRange"/> takes.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="multiplier"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKeltnerChannelWidth(this StockData stockData, int length = 20, double multiplier = 2)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> widthList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        var inputSpan = SpanCompat.AsReadOnlySpan(inputList);
        var emaBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.ExponentialMovingAverage(inputSpan, emaBuffer.Span, length);

        var trList = GetTrueRangeList(stockData);
        var trSpan = SpanCompat.AsReadOnlySpan(trList);
        var atrBuffer = SpanCompat.CreateOutputBuffer(count);
        MovingAverageCore.WellesWilderMovingAverage(trSpan, atrBuffer.Span, length);

        for (var i = 0; i < count; i++)
        {
            var ema = emaBuffer.Span[i];
            var atr = atrBuffer.Span[i];
            var upper = ema + (multiplier * atr);
            var lower = ema - (multiplier * atr);
            var width = ema != 0 ? (upper - lower) / ema * 100 : 0;
            widthList.Add(width);

            var prevWidth1 = i >= 1 ? widthList[i - 1] : 0;
            var prevWidth2 = i >= 2 ? widthList[i - 2] : 0;
            var signal = GetCompareSignal(width - prevWidth1, prevWidth1 - prevWidth2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Kcw", widthList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(widthList);
        stockData.IndicatorName = IndicatorName.KeltnerChannelWidth;

        return stockData;
    }

    /// <summary>
    /// Calculates the historical volatility.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHistoricalVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> hvList = new(stockData.Count);
        List<double> tempLogList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var annualSqrt = Sqrt(365);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var tempLog = StableLogRatio.OfSameSign(currentValue, prevValue);
            tempLogList.Add(tempLog);
        }

        // The deviation of the log-return window about its own mean, not the mean squared residual from a
        // moving average of that series. Historical volatility is defined as the standard deviation of
        // returns, so this is the quantity it is defined against; see #190.
        var stdDevLogList = GetStandardDeviationList(tempLogList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            // The first bar has no prior close, so its log return is a fabricated zero. A window still
            // holding it is one genuine return short, and a deviation taken over it is diluted by an
            // observation that never happened. Nothing is published until index length, where the window is
            // returns 1..length and every one of them is real.
            var stdDevLog = i >= length ? stdDevLogList[i] : 0;
            var currentEma = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevHv = GetLastOrDefault(hvList);
            var hv = 100 * stdDevLog * annualSqrt;
            hvList.Add(hv);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, hv, prevHv);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hv", hvList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hvList);
        stockData.IndicatorName = IndicatorName.HistoricalVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Garman Klass Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGarmanKlassVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 14, int signalLength = 7)
    {
        List<double> gcvList = new(stockData.Count);
        List<double> logList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        length = Math.Max(1, length);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var logHl = currentLow != 0 ? StableLogRatio.OfSameSign(currentHigh, currentLow) : 0;
            var logCo = currentOpen != 0 ? StableLogRatio.OfSameSign(currentClose, currentOpen) : 0;

            var log = (0.5 * Pow(logHl, 2)) - (((2 * Math.Log(2)) - 1) * Pow(logCo, 2));
            logList.Add(log);
            double logSum = 0;
            if (i >= length - 1)
                for (var j = i - length + 1; j <= i; j++) logSum += logList[j];
            var gcv = Sqrt(logSum / length) * Sqrt(252);
            gcvList.Add(gcv);
        }

        var gcvWmaList = GetMovingAverageList(stockData, maType, signalLength, gcvList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var wma = wmaList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;
            var gcv = gcvList[i];
            var gcvWma = i >= 1 ? gcvWmaList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentClose - wma, prevClose - prevWma, gcv, gcvWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gcv", gcvList },
            { "Signal", gcvWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gcvList);
        stockData.IndicatorName = IndicatorName.GarmanKlassVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Gopalakrishnan Range Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateGopalakrishnanRangeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 5)
    {
        // A logarithm base of one is undefined; use the smallest valid period.
        length = Math.Max(2, length);
        List<double> gapoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var wmaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var range = highestHigh - lowestLow;
            var rangeLog = range > 0 ? Math.Log(range) : 0;

            var gapo = rangeLog / Math.Log(length);
            gapoList.Add(gapo);
        }

        var gapoWmaList = GetMovingAverageList(stockData, maType, length, gapoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var gapoWma = gapoWmaList[i];
            var prevGapoWma = i >= 1 ? gapoWmaList[i - 1] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentWma = wmaList[i];
            var prevWma = i >= 1 ? wmaList[i - 1] : 0;

            var signal = GetVolatilitySignal(currentValue - currentWma, prevValue - prevWma, gapoWma, prevGapoWma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Gapo", gapoList },
            { "Signal", gapoWmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(gapoList);
        stockData.IndicatorName = IndicatorName.GopalakrishnanRangeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Historical Volatility Percentile
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="annualLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHistoricalVolatilityPercentile(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 21, int annualLength = 252)
    {
        List<double> devLogSqList = new(stockData.Count);
        List<double> devLogSqAvgList = new(stockData.Count);
        List<double> hvList = new(stockData.Count);
        List<double> hvpList = new(stockData.Count);
        List<double> tempLogList = new(stockData.Count);
        List<double> stdDevLogList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum tempLogSumWindow = new();
        RollingSum devLogSqSumWindow = new();
        using var hvOrder = new RollingOrderStatistic(annualLength);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma = emaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var tempLog = currentValue != 0 && prevValue != 0 && Math.Sign(currentValue) == Math.Sign(prevValue)
                ? StableLogRatio.Of(Math.Abs(currentValue), Math.Abs(prevValue)) : 0;
            tempLogList.Add(tempLog);
            tempLogSumWindow.Add(tempLog);

            var avgLog = tempLogSumWindow.Average(length);
            var devLogSq = Pow(tempLog - avgLog, 2);
            devLogSqList.Add(devLogSq);
            devLogSqSumWindow.Add(devLogSq);

            // A single observation has no estimable sample variance; define its volatility as zero.
            var devLogSqAvg = length > 1 ? devLogSqSumWindow.Sum(length) / (length - 1) : 0;
            var stdDevLog = devLogSqAvg >= 0 ? Sqrt(devLogSqAvg) : 0;

            var hv = stdDevLog * Sqrt(annualLength);
            hvList.Add(hv);
            hvOrder.Add(hv);

            double count = hvOrder.CountLessThan(VolatilityRank.StrictBoundary(hv));
            var hvp = count / annualLength * 100;
            hvpList.Add(hvp);
        }

        var hvpEmaList = GetMovingAverageList(stockData, maType, length, hvpList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var hvp = hvpList[i];
            var hvpEma = hvpEmaList[i];

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, hvp, hvpEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Hvp", hvpList },
            { "Signal", hvpEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(hvpList);
        stockData.IndicatorName = IndicatorName.HistoricalVolatilityPercentile;

        return stockData;
    }


    /// <summary>
    /// Calculates the Fast Z Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFastZScore(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        length = Math.Max(1, length);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var exact = StrengthWindow.Supports(maType);
        var means = exact ? StrengthWindow.Smooth(inputList, maType, length) : GetMovingAverageList(stockData, maType, length, inputList);
        using var state = new StandardizedScoreWindow(length, true);
        var values = new List<double>(stockData.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < stockData.Count; i++)
        {
            var score = state.Next(inputList[i], means[i], exact && maType == MovingAvgType.SimpleMovingAverage, true);
            var previous = i > 0 ? values[i - 1] : 0;
            var older = i > 1 ? values[i - 2] : 0;
            signals?.Add(GetVolatilitySignal(inputList[i] - means[i], i > 0 ? inputList[i - 1] - means[i - 1] : 0, score, 0)); values.Add(score);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fzs", values } });
        stockData.SetSignals(signals); stockData.SetCustomValues(values);
        stockData.IndicatorName = IndicatorName.FastZScore; return stockData;
    }

}

