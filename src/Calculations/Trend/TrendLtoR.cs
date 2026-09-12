
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Lowest Low over a rolling window.
    /// </summary>
    /// <remarks>
    /// The lowest low of the last <paramref name="length"/> bars. The window expands rather than warming up:
    /// before it is full, the lowest low of the bars so far is still the lowest low there is. When the caller
    /// supplies their own series, the low is the one the batch engine derives for that bar.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateLowestLow(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (_, _, lowList, _, _) = GetInputValuesList(stockData);
        var count = lowList.Count;
        List<double> lowestLowList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var start = Math.Max(0, i - length + 1);
            var lowest = lowList[start];
            for (var j = start + 1; j <= i; j++)
            {
                if (lowList[j] < lowest)
                {
                    lowest = lowList[j];
                }
            }

            lowestLowList.Add(lowest);

            var prevLowest1 = i >= 1 ? lowestLowList[i - 1] : 0;
            var prevLowest2 = i >= 2 ? lowestLowList[i - 2] : 0;
            var signal = GetCompareSignal(lowest - prevLowest1, prevLowest1 - prevLowest2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LowestLow", lowestLowList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(lowestLowList);
        stockData.IndicatorName = IndicatorName.LowestLow;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rolling Maximum of the input series.
    /// </summary>
    /// <remarks>
    /// The largest value of the last <paramref name="length"/> bars of the series being measured, which is the
    /// caller's own series when they supply one. The window expands rather than warming up.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRollingMax(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> rollingMaxList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var start = Math.Max(0, i - length + 1);
            var max = inputList[start];
            for (var j = start + 1; j <= i; j++)
            {
                if (inputList[j] > max)
                {
                    max = inputList[j];
                }
            }

            rollingMaxList.Add(max);

            var prevMax1 = i >= 1 ? rollingMaxList[i - 1] : 0;
            var prevMax2 = i >= 2 ? rollingMaxList[i - 2] : 0;
            var signal = GetCompareSignal(max - prevMax1, prevMax1 - prevMax2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "RollingMax", rollingMaxList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rollingMaxList);
        stockData.IndicatorName = IndicatorName.RollingMax;

        return stockData;
    }

    /// <summary>
    /// Calculates the Rolling Minimum of the input series.
    /// </summary>
    /// <remarks>
    /// The smallest value of the last <paramref name="length"/> bars of the series being measured, which is the
    /// caller's own series when they supply one. The window expands rather than warming up.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRollingMin(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> rollingMinList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var start = Math.Max(0, i - length + 1);
            var min = inputList[start];
            for (var j = start + 1; j <= i; j++)
            {
                if (inputList[j] < min)
                {
                    min = inputList[j];
                }
            }

            rollingMinList.Add(min);

            var prevMin1 = i >= 1 ? rollingMinList[i - 1] : 0;
            var prevMin2 = i >= 2 ? rollingMinList[i - 2] : 0;
            var signal = GetCompareSignal(min - prevMin1, prevMin1 - prevMin2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "RollingMin", rollingMinList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rollingMinList);
        stockData.IndicatorName = IndicatorName.RollingMin;

        return stockData;
    }

    /// <summary>
    /// Calculates the Range of each bar.
    /// </summary>
    /// <remarks>
    /// The bar's high less its low. It looks at no other bar, so it has no length and never warms up.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRange(this StockData stockData)
    {
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var count = highList.Count;
        List<double> rangeList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            var range = highList[i] - lowList[i];
            rangeList.Add(range);

            var prevRange1 = i >= 1 ? rangeList[i - 1] : 0;
            var prevRange2 = i >= 2 ? rangeList[i - 2] : 0;
            var signal = GetCompareSignal(range - prevRange1, prevRange1 - prevRange2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Range", rangeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rangeList);
        stockData.IndicatorName = IndicatorName.Range;

        return stockData;
    }

    /// <summary>
    /// Calculates the R Squared of the input series against time.
    /// </summary>
    /// <remarks>
    /// The square of the correlation between the window's values and the bar numbers they sit on: how much
    /// of the window's movement a straight line accounts for. It is a measure of how trending the window is
    /// and not of direction, so it runs from zero to one whichever way the line slopes. A window that does
    /// not move at all has no line to fit and publishes zero, as does one shorter than the length.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateRSquared(this StockData stockData, int length = 14)
    {
        length = Math.Max(length, 1);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var count = inputList.Count;
        List<double> rSquaredList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);

        for (var i = 0; i < count; i++)
        {
            double rSquared = 0;
            if (i >= length - 1)
            {
                double sumX = 0, sumY = 0, sumXy = 0, sumX2 = 0, sumY2 = 0;
                for (var j = 0; j < length; j++)
                {
                    double x = j;
                    var y = inputList[i - length + 1 + j];
                    sumX += x;
                    sumY += y;
                    sumXy += x * y;
                    sumX2 += x * x;
                    sumY2 += y * y;
                }

                var numerator = (length * sumXy) - (sumX * sumY);
                var denominator = Sqrt(((length * sumX2) - (sumX * sumX)) * ((length * sumY2) - (sumY * sumY)));
                var r = denominator != 0 ? numerator / denominator : 0;
                rSquared = r * r;
            }

            rSquaredList.Add(rSquared);

            var prevRSquared1 = i >= 1 ? rSquaredList[i - 1] : 0;
            var prevRSquared2 = i >= 2 ? rSquaredList[i - 2] : 0;
            var signal = GetCompareSignal(rSquared - prevRSquared1, prevRSquared1 - prevRSquared2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "RSquared", rSquaredList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rSquaredList);
        stockData.IndicatorName = IndicatorName.RSquared;

        return stockData;
    }

    /// <summary>
    /// Calculates the Optimized Trend Tracker
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="percent"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOptimizedTrendTracker(this StockData stockData, MovingAvgType maType = MovingAvgType.VariableIndexDynamicAverage,
        int length = 2, double percent = 1.4)
    {
        List<double> longStopList = new(stockData.Count);
        List<double> shortStopList = new(stockData.Count);
        List<double> ottList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ma = maList[i];
            var fark = ma * percent * 0.01;

            var prevLongStop = i >= 1 ? longStopList[i - 1] : 0;
            var longStop = ma - fark;
            longStop = ma > prevLongStop ? Math.Max(longStop, prevLongStop) : longStop;
            longStopList.Add(longStop);

            var prevShortStop = i >= 1 ? shortStopList[i - 1] : 0;
            var shortStop = ma + fark;
            shortStopList.Add(shortStop);

            var prevOtt = i >= 1 ? ottList[i - 1] : 0;
            var mt = ma > prevShortStop ? longStop : ma < prevLongStop ? shortStop : 0;
            var ott = ma > mt ? mt * (200 + percent) / 200 : mt * (200 - percent) / 200;
            ottList.Add(ott);

            var signal = GetCompareSignal(currentValue - ott, prevValue - prevOtt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ott", ottList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ottList);
        stockData.IndicatorName = IndicatorName.OptimizedTrendTracker;

        return stockData;
    }


    /// <summary>
    /// Calculates the Price Volume Trend
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePriceVolumeTrend(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> priceVolumeTrendList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevPvt = i >= 1 ? priceVolumeTrendList[i - 1] : 0;
            var pvt = prevValue != 0 ? prevPvt + (currentVolume * (MinPastValues(i, 1, currentValue - prevValue) / prevValue)) : prevPvt;
            priceVolumeTrendList.Add(pvt);
        }

        var pvtEmaList = GetMovingAverageList(stockData, maType, length, priceVolumeTrendList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pvt = priceVolumeTrendList[i];
            var pvtEma = pvtEmaList[i];
            var prevPvt = i >= 1 ? priceVolumeTrendList[i - 1] : 0;
            var prevPvtEma = i >= 1 ? pvtEmaList[i - 1] : 0;

            var signal = GetCompareSignal(pvt - pvtEma, prevPvt - prevPvtEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pvt", priceVolumeTrendList },
            { "Signal", pvtEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(priceVolumeTrendList);
        stockData.IndicatorName = IndicatorName.PriceVolumeTrend;

        return stockData;
    }


    /// <summary>
    /// Calculates the Percentage Trend
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pct"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculatePercentageTrend(this StockData stockData, int length = 20, double pct = 0.15)
    {
        List<double> trendList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];

            var period = 0;
            var prevTrend = i >= 1 ? trendList[i - 1] : 0;
            var trend = currentValue;
            for (var j = 1; j <= length; j++)
            {
                var prevC = i >= j - 1 ? inputList[i - (j - 1)] : 0;
                var currC = i >= j ? inputList[i - j] : 0;
                period = (prevC <= trend && currC > trend) || (prevC >= trend && currC < trend) ? 0 : period;

                double highest1 = currC, lowest1 = currC;
                for (var k = j - period; k <= j; k++)
                {
                    var c = i >= j - k ? inputList[i - (j - k)] : 0;
                    highest1 = Math.Max(highest1, c);
                    lowest1 = Math.Min(lowest1, c);
                }

                double highest2 = currC, lowest2 = currC;
                for (var k = i - length; k <= j; k++)
                {
                    var c = i >= j - k ? inputList[i - (j - k)] : 0;
                    highest2 = Math.Max(highest2, c);
                    lowest2 = Math.Min(lowest2, c);
                }

                if (period < length)
                {
                    period += 1;
                    trend = currC > trend ? highest1 * (1 - pct) : lowest1 * (1 + pct);
                }
                else
                {
                    trend = currC > trend ? highest2 * (1 - pct) : lowest2 * (1 + pct);
                }
            }
            trendList.Add(trend);

            var signal = GetCompareSignal(currentValue - trend, prevValue - prevTrend);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Pti", trendList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trendList);
        stockData.IndicatorName = IndicatorName.PercentageTrend;

        return stockData;
    }


    /// <summary>
    /// Calculates the Modified Price Volume Trend
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateModifiedPriceVolumeTrend(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 23)
    {
        List<double> mpvtList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];
            var rv = currentVolume / 50000;

            var prevMpvt = i >= 1 ? mpvtList[i - 1] : 0;
            var mpvt = prevValue != 0 ? prevMpvt + (rv * MinPastValues(i, 1, currentValue - prevValue) / prevValue) : 0;
            mpvtList.Add(mpvt);
        }

        var mpvtSignalList = GetMovingAverageList(stockData, maType, length, mpvtList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mpvt = mpvtList[i];
            var mpvtSignal = mpvtSignalList[i];
            var prevMpvt = i >= 1 ? mpvtList[i - 1] : 0;
            var prevMpvtSignal = i >= 1 ? mpvtSignalList[i - 1] : 0;

            var signal = GetCompareSignal(mpvt - mpvtSignal, prevMpvt - prevMpvtSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mpvt", mpvtList },
            { "Signal", mpvtSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(mpvtList);
        stockData.IndicatorName = IndicatorName.ModifiedPriceVolumeTrend;

        return stockData;
    }
}

