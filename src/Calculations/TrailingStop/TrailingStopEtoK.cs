
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Half Trend
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="atrLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateHalfTrend(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 2,
        int atrLength = 100)
    {
        List<double> trendList = new(stockData.Count);
        List<double> nextTrendList = new(stockData.Count);
        List<double> upList = new(stockData.Count);
        List<double> downList = new(stockData.Count);
        List<double> htList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var atrList = CalculateAverageTrueRange(stockData, maType, atrLength).ChainedValues;
        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

        double maxLow = 0, minHigh = 0;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAvgTrueRange = atrList[i];
            var high = highestList[i];
            var low = lowestList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var highMa = highMaList[i];
            var lowMa = lowMaList[i];
            if (i == 0) { maxLow = low; minHigh = high; }
            var prevNextTrend = GetLastOrDefault(nextTrendList);
            var prevTrend = GetLastOrDefault(trendList);
            var prevUp = i == 0 ? low : upList[i - 1];
            var prevDown = i == 0 ? high : downList[i - 1];
            var atr = currentAvgTrueRange / 2;
            var dev = length * atr;

            var trend = prevTrend;
            var nextTrend = prevNextTrend;
            if (prevNextTrend == 1)
            {
                maxLow = Math.Max(low, maxLow);
                if (highMa < maxLow && currentValue < (i > 0 ? prevLow : low))
                {
                    trend = 1;
                    nextTrend = 0;
                    minHigh = high;
                }
            }
            else
            {
                minHigh = Math.Min(high, minHigh);
                if (lowMa > minHigh && currentValue > (i > 0 ? prevHigh : high))
                {
                    trend = 0;
                    nextTrend = 1;
                    maxLow = low;
                }
            }
            trendList.Add(trend);
            nextTrendList.Add(nextTrend);

            double up = 0, down = 0, arrowUp = 0, arrowDown = 0;
            if (trend == 0)
            {
                if (prevTrend != 0)
                {
                    up = prevDown;
                    arrowUp = up - atr;
                }
                else
                {
                    up = Math.Max(maxLow, prevUp);
                }
            }
            else
            {
                if (prevTrend != 1)
                {
                    down = prevUp;
                    arrowDown = down + atr;
                }
                else
                {
                    down = Math.Min(minHigh, prevDown);
                }
            }
            upList.Add(up);
            downList.Add(down);

            var ht = trend == 0 ? up : down;
            htList.Add(ht);

            var signal = GetConditionSignal(arrowUp != 0 && trend == 0 && prevTrend == 1, arrowDown != 0 && trend == 1 && prevTrend == 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ht", htList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(htList);
        stockData.IndicatorName = IndicatorName.HalfTrend;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Dev Stop V1
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDev1"></param>
    /// <param name="stdDev2"></param>
    /// <param name="stdDev3"></param>
    /// <param name="stdDev4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaseDevStopV1(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 21, int length = 20, double stdDev1 = 0,
        double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6)
    {
        List<double> warningLineList = new(stockData.Count);
        List<double> dev1List = new(stockData.Count);
        List<double> dev2List = new(stockData.Count);
        List<double> dev3List = new(stockData.Count);
        List<double> dtrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(InputName.TypicalPrice, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 2 ? closeList[i - 2] : 0;
            var prevLow = i >= 2 ? lowList[i - 2] : 0;

            var dtr = Math.Max(Math.Max(currentHigh - prevLow, Math.Abs(currentHigh - prevClose)), Math.Abs(currentLow - prevClose));
            dtrList.Add(dtr);
        }

        var dtrAvgList = GetMovingAverageList(stockData, maType, length, dtrList);
        // Direction is discontinuous at equal means. Preserve sums through division.
        var smaSlowList = Streaming.SpreadAverage.Calculate(inputList, maType, slowLength);
        var smaFastList = Streaming.SpreadAverage.Calculate(inputList, maType, fastLength);
        // The deviation of the true-range window about its own mean; the band below is avg + k * dev, so
        // this is the quantity a band at k sigma is defined against. See #190.
        var dtrStdList = GetStandardDeviationList(dtrList, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var maFast = smaFastList[i];
            var maSlow = smaSlowList[i];
            var dtrAvg = dtrAvgList[i];
            var dtrStd = dtrStdList[i];
            var currentTypicalPrice = inputList[i];
            var prevMaFast = i >= 1 ? smaFastList[i - 1] : 0;
            var prevMaSlow = i >= 1 ? smaSlowList[i - 1] : 0;

            var warningLine = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev1 * dtrStd) :
                currentTypicalPrice - dtrAvg - (stdDev1 * dtrStd);
            warningLineList.Add(warningLine);

            var dev1 = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev2 * dtrStd) : currentTypicalPrice - dtrAvg - (stdDev2 * dtrStd);
            dev1List.Add(dev1);

            var dev2 = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev3 * dtrStd) : currentTypicalPrice - dtrAvg - (stdDev3 * dtrStd);
            dev2List.Add(dev2);

            var dev3 = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev4 * dtrStd) : currentTypicalPrice - dtrAvg - (stdDev4 * dtrStd);
            dev3List.Add(dev3);

            var signal = GetCompareSignal(maFast - maSlow, prevMaFast - prevMaSlow);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dev1", dev1List },
            { "Dev2", dev2List },
            { "Dev3", dev3List },
            { "WarningLine", warningLineList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaseDevStopV1;

        return stockData;
    }


    /// <summary>
    /// Calculates the Kase Dev Stop V2
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDev1"></param>
    /// <param name="stdDev2"></param>
    /// <param name="stdDev3"></param>
    /// <param name="stdDev4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateKaseDevStopV2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 10, int slowLength = 21, int length = 20, double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2,
        double stdDev4 = 3.6)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new KaseStopV2Window(maType, fastLength, slowLength, length, stdDev1, stdDev2, stdDev3, stdDev4, external);
        List<double>? fast = null, slow = null, mean = null;
        if (external) { var caller = stockData.CaptureInputSeries(); List<double> Average(List<double> values, int period) => Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(values), Math.Max(1, period))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, period), values); fast = Average(input, fastLength); slow = Average(input, slowLength); mean = Average(KaseStopV2Window.PublishedRanges(high, low, input).ToList(), length); stockData.RestoreInputSeries(caller); }
        var first = new List<double>(input.Count); var second = new List<double>(input.Count); var third = new List<double>(input.Count); var fourth = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, external ? fast![i] : null, external ? slow![i] : null, external ? mean![i] : null); first.Add(point.Dev1); second.Add(point.Dev2); third.Add(point.Dev3); fourth.Add(point.Dev4); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dev1", first }, { "Dev2", second }, { "Dev3", third }, { "Dev4", fourth } }); stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>()); stockData.IndicatorName = IndicatorName.KaseDevStopV2; return stockData;
    }


    /// <summary>
    /// Calculates the Elder Safe Zone Stops
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateElderSafeZoneStops(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 63, int length2 = 22, int length3 = 3, double factor = 2.5)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType); using var window = new ElderSafeZoneWindow(maType, length1, length2, length3, factor, external); List<double>? trend = null;
        if (external) { var caller = stockData.CaptureInputSeries(); trend = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(input), Math.Max(1, length1))?.ToList() ?? GetMovingAverageList(stockData, maType, Math.Max(1, length1), input); stockData.RestoreInputSeries(caller); }
        var values = new List<double>(input.Count); var signals = CreateSignalsList(stockData); for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, external ? trend![i] : null); values.Add(point.Value); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Eszs", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.ElderSafeZoneStops; return stockData;
    }
}

