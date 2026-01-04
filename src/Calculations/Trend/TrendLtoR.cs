
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Optimized Trend Tracker
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="percent"></param>
    /// <returns></returns>
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

