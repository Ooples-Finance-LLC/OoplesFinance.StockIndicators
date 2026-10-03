
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the parabolic sar.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="start">The start.</param>
    /// <param name="increment">The increment.</param>
    /// <param name="maximum">The maximum.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateParabolicSAR(this StockData stockData, double start = 0.02, double increment = 0.02, double maximum = 0.2)
    {
        List<double> sarList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var kernel = new Streaming.ParabolicSarKernel(start, increment, maximum);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sar = kernel.Next(highList[i], lowList[i], true);
            sarList.Add(sar);
            signalsList?.Add(kernel.Signal);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Sar", sarList } });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sarList);
        stockData.IndicatorName = IndicatorName.ParabolicSAR;
        return stockData;
    }


    /// <summary>
    /// Calculates the Linear Trailing Stop
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLinearTrailingStop(this StockData stockData, int length = 14, double mult = 28)
    {
        List<double> aList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<double> tsList = new(stockData.Count);
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var s = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevA2 = i >= 2 ? aList[i - 2] : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var x = currentValue + ((prevA - prevA2) * mult);

            var a = x > prevA + s ? prevA + s : x < prevA - s ? prevA - s : prevA;
            aList.Add(a);

            var up = a + (Math.Abs(a - prevA) * mult);
            var dn = a - (Math.Abs(a - prevA) * mult);

            var prevUpper = GetLastOrDefault(upperList);
            var upper = up == a ? prevUpper : up;
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = dn == a ? prevLower : dn;
            lowerList.Add(lower);

            var prevOs = GetLastOrDefault(osList);
            var os = currentValue > upper ? 1 : currentValue < lower ? 0 : prevOs;
            osList.Add(os);

            var prevTs = GetLastOrDefault(tsList);
            var ts = (os * lower) + ((1 - os) * upper);
            tsList.Add(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ts", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.LinearTrailingStop;

        return stockData;
    }


    /// <summary>
    /// Calculates the Nick Rypock Trailing Reverse
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateNickRypockTrailingReverse(this StockData stockData, int length = 2)
    {
        length = Math.Max(1, length);
        List<double> nrtrList = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> lpList = new(stockData.Count);
        List<double> trendList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var pct = (double)length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevTrend = GetLastOrDefault(trendList);
            var prevHp = GetLastOrDefault(hpList);
            var prevLp = GetLastOrDefault(lpList);
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevNrtr = GetLastOrDefault(nrtrList);
            double nrtr, hp = 0, lp = 0, trend = prevTrend;
            if (prevTrend >= 0)
            {
                hp = currentValue > prevHp ? currentValue : prevHp;
                nrtr = RoundedPercentageBand.Percent(hp, pct, -1);

                if (currentValue <= nrtr)
                {
                    trend = -1;
                    lp = currentValue;
                    nrtr = RoundedPercentageBand.Percent(lp, pct, 1);
                }
            }
            else
            {
                lp = currentValue < prevLp ? currentValue : prevLp;
                nrtr = RoundedPercentageBand.Percent(lp, pct, 1);

                if (currentValue > nrtr)
                {
                    trend = 1;
                    hp = currentValue;
                    nrtr = RoundedPercentageBand.Percent(hp, pct, -1);
                }
            }
            trendList.Add(trend);
            hpList.Add(hp);
            lpList.Add(lp);
            nrtrList.Add(nrtr);

            var signal = GetCompareSignal(currentValue - nrtr, prevValue - prevNrtr);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nrtr", nrtrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nrtrList);
        stockData.IndicatorName = IndicatorName.NickRypockTrailingReverse;

        return stockData;
    }


    /// <summary>
    /// Calculates the Percentage Trailing Stops
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pct"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePercentageTrailingStops(this StockData stockData, int length = 100, double pct = 10)
    {
        List<double> stopSList = new(stockData.Count);
        List<double> stopLList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHH = i >= 1 ? highestList[i - 1] : currentClose;
            var prevLL = i >= 1 ? lowestList[i - 1] : currentClose;
            var pSS = i >= 1 ? GetLastOrDefault(stopSList) : currentClose;
            var pSL = i >= 1 ? GetLastOrDefault(stopLList) : currentClose;

            var stopL = currentHigh > prevHH ? RoundedPercentageBand.Percent(currentHigh, pct, -1) : pSL;
            stopLList.Add(stopL);

            var stopS = currentLow < prevLL ? RoundedPercentageBand.Percent(currentLow, pct, 1) : pSS;
            stopSList.Add(stopS);

            var signal = GetConditionSignal(prevHigh < stopS && currentHigh > stopS, prevLow > stopL && currentLow < stopL);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LongStop", stopLList },
            { "ShortStop", stopSList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.PercentageTrailingStops;

        return stockData;
    }


    /// <summary>
    /// Calculates the Motion To Attraction Trailing Stop
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMotionToAttractionTrailingStop(this StockData stockData, int length = 14)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var window = new MotionAttractionWindow(length); List<double> values = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var value = window.Next(input[i], true).Stop; signals?.Add(GetCompareSignal(input[i] - value, i > 0 ? input[i - 1] - values[i - 1] : 0)); values.Add(value); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ts", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.MotionToAttractionTrailingStop; return stockData;
    }


    /// <summary>
    /// Calculates the UT Bot Alerts indicator, the ATR trailing stop published as the
    /// "UT Bot Alerts" TradingView script.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The stop trails price by <paramref name="keyValue"/> multiples of ATR. While price stays on the
    /// same side of the stop the level only ever moves in the favourable direction - it ratchets up in an
    /// uptrend and down in a downtrend, and never gives ground - which is what makes it a stop rather than
    /// a band. When price closes through it, the stop flips to the other side and the position marker
    /// reverses.
    /// </para>
    /// <para>
    /// Buy and Sell mark the bars where that flip happens; Position holds 1 while long and -1 while short,
    /// so it can be read directly as a regime filter between flips.
    /// </para>
    /// </remarks>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Moving average used to smooth the true range. Wilder's is the ATR standard.</param>
    /// <param name="length">ATR period.</param>
    /// <param name="keyValue">ATR multiple the stop trails by.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateUtBotAlerts(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 10, double keyValue = 1)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var window = new UtBotWindow(maType, length, keyValue, external); List<double>? atr = null;
        if (external) { var caller = stockData.CaptureInputSeries(); var ranges = GetTrueRangeList(stockData); atr = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ranges), Math.Max(1, length))?.ToList() ?? GetMovingAverageList(stockData, maType, length, ranges); stockData.RestoreInputSeries(caller); }
        var stops = new List<double>(input.Count); var positions = new List<double>(input.Count); var buys = new List<double>(input.Count); var sells = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, external ? atr![i] : null); stops.Add(point.Stop); positions.Add(point.Position); buys.Add(point.Buy); sells.Add(point.Sell); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "TrailingStop", stops }, { "Position", positions }, { "Buy", buys }, { "Sell", sells } }); stockData.SetSignals(signals); stockData.SetCustomValues(stops); stockData.IndicatorName = IndicatorName.UtBotAlerts; return stockData;
    }

}

