
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

        var atrList = CalculateAverageTrueRange(stockData, maType, atrLength).CustomValuesList;
        var highMaList = GetMovingAverageList(stockData, maType, length, highList);
        var lowMaList = GetMovingAverageList(stockData, maType, length, lowList);

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
            var maxLow = i >= 1 ? prevLow : low;
            var minHigh = i >= 1 ? prevHigh : high;
            var prevNextTrend = GetLastOrDefault(nextTrendList);
            var prevTrend = GetLastOrDefault(trendList);
            var prevUp = GetLastOrDefault(upList);
            var prevDown = GetLastOrDefault(downList);
            var atr = currentAvgTrueRange / 2;
            var dev = length * atr;

            double trend = 0, nextTrend = 0;
            if (prevNextTrend == 1)
            {
                maxLow = Math.Max(low, maxLow);

                if (highMa < maxLow && currentValue < (prevLow != 0 ? prevLow : low))
                {
                    trend = 1;
                    nextTrend = 0;
                    minHigh = high;
                }
                else
                {
                    minHigh = Math.Min(high, minHigh);

                    if (lowMa > minHigh && currentValue > (prevHigh != 0 ? prevHigh : high))
                    {
                        trend = 0;
                        nextTrend = 1;
                        maxLow = low;
                    }
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
    /// <param name="inputName"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDev1"></param>
    /// <param name="stdDev2"></param>
    /// <param name="stdDev3"></param>
    /// <param name="stdDev4"></param>
    /// <returns></returns>
    public static StockData CalculateKaseDevStopV1(this StockData stockData, InputName inputName = InputName.TypicalPrice,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 21, int length = 20, double stdDev1 = 0,
        double stdDev2 = 1, double stdDev3 = 2.2, double stdDev4 = 3.6)
    {
        List<double> warningLineList = new(stockData.Count);
        List<double> dev1List = new(stockData.Count);
        List<double> dev2List = new(stockData.Count);
        List<double> dev3List = new(stockData.Count);
        List<double> dtrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);

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
        var smaSlowList = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var smaFastList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        stockData.SetCustomValues(dtrList);
        var dtrStdList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
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
    public static StockData CalculateKaseDevStopV2(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 10, int slowLength = 21, int length = 20, double stdDev1 = 0, double stdDev2 = 1, double stdDev3 = 2.2,
        double stdDev4 = 3.6)
    {
        List<double> valList = new(stockData.Count);
        List<double> val1List = new(stockData.Count);
        List<double> val2List = new(stockData.Count);
        List<double> val3List = new(stockData.Count);
        List<double> rrangeList = new(stockData.Count);
        List<double> priceList = new(stockData.Count);
        List<double> trendList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaFastList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var smaSlowList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var maFast = smaFastList[i];
            var maSlow = smaSlowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevClose = i >= 2 ? inputList[i - 2] : 0;

            double trend = maFast > maSlow ? 1 : -1;
            trendList.Add(trend);

            var price = trend == 1 ? currentHigh : currentLow;
            price = trend > 0 ? Math.Max(price, currentHigh) : Math.Min(price, currentLow);
            priceList.Add(price);

            var mmax = Math.Max(Math.Max(currentHigh, prevHigh), prevClose);
            var mmin = Math.Min(Math.Min(currentLow, prevLow), prevClose);
            var rrange = mmax - mmin;
            rrangeList.Add(rrange);
        }

        var rangeAvgList = GetMovingAverageList(stockData, maType, length, rrangeList);
        stockData.SetCustomValues(rrangeList);
        var rangeStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var price = priceList[i];
            var trend = trendList[i];
            var avg = rangeAvgList[i];
            var dev = rangeStdDevList[i];
            var prevPrice = i >= 1 ? priceList[i - 1] : 0;

            var val = (price + ((-1) * trend)) * (avg + (stdDev1 * dev));
            valList.Add(val);

            var val1 = (price + ((-1) * trend)) * (avg + (stdDev2 * dev));
            val1List.Add(val1);

            var val2 = (price + ((-1) * trend)) * (avg + (stdDev3 * dev));
            val2List.Add(val2);

            var prevVal3 = GetLastOrDefault(val3List);
            var val3 = (price + ((-1) * trend)) * (avg + (stdDev4 * dev));
            val3List.Add(val3);

            var signal = GetCompareSignal(price - val3, prevPrice - prevVal3);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dev1", valList },
            { "Dev2", val1List },
            { "Dev3", val2List },
            { "Dev4", val3List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.KaseDevStopV2;

        return stockData;
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
    public static StockData CalculateElderSafeZoneStops(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 63, int length2 = 22, int length3 = 3, double factor = 2.5)
    {
        List<double> safeZPlusList = new(stockData.Count);
        List<double> safeZMinusList = new(stockData.Count);
        List<double> dmPlusCountList = new(stockData.Count);
        List<double> dmMinusCountList = new(stockData.Count);
        List<double> dmMinusList = new(stockData.Count);
        List<double> dmPlusList = new(stockData.Count);
        List<double> stopList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum dmMinusCountSum = new();
        RollingSum dmMinusSum = new();
        RollingSum dmPlusCountSum = new();
        RollingSum dmPlusSum = new();
        RollingMinMax safeZMinusWindow = new(length3);
        RollingMinMax safeZPlusWindow = new(length3);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentLow = lowList[i];
            var currentHigh = highList[i];
            var currentEma = emaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;

            var dmMinus = prevLow > currentLow ? prevLow - currentLow : 0;
            dmMinusList.Add(dmMinus);
            dmMinusSum.Add(dmMinus);

            double dmMinusCount = prevLow > currentLow ? 1 : 0;
            dmMinusCountList.Add(dmMinusCount);
            dmMinusCountSum.Add(dmMinusCount);

            var dmPlus = currentHigh > prevHigh ? currentHigh - prevHigh : 0;
            dmPlusList.Add(dmPlus);
            dmPlusSum.Add(dmPlus);

            double dmPlusCount = currentHigh > prevHigh ? 1 : 0;
            dmPlusCountList.Add(dmPlusCount);
            dmPlusCountSum.Add(dmPlusCount);

            var countM = dmMinusCountSum.Sum(length2);
            var dmMinusSumValue = dmMinusSum.Sum(length2);
            var dmAvgMinus = countM != 0 ? dmMinusSumValue / countM : 0;
            var countP = dmPlusCountSum.Sum(length2);
            var dmPlusSumValue = dmPlusSum.Sum(length2);
            var dmAvgPlus = countP != 0 ? dmPlusSumValue / countP : 0;

            var safeZMinus = prevLow - (factor * dmAvgMinus);
            safeZMinusList.Add(safeZMinus);
            safeZMinusWindow.Add(safeZMinus);

            var safeZPlus = prevHigh + (factor * dmAvgPlus);
            safeZPlusList.Add(safeZPlus);
            safeZPlusWindow.Add(safeZPlus);

            var highest = safeZMinusWindow.Max;
            var lowest = safeZPlusWindow.Min;

            var prevStop = GetLastOrDefault(stopList);
            var stop = currentValue >= currentEma ? highest : lowest;
            stopList.Add(stop);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(currentEma, stop), prevValue - Math.Max(prevEma, prevStop),
                currentValue - Math.Min(currentEma, stop), prevValue - Math.Min(prevEma, prevStop));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eszs", stopList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stopList);
        stockData.IndicatorName = IndicatorName.ElderSafeZoneStops;

        return stockData;
    }
}

