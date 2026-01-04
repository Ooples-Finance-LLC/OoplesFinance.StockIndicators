
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the adaptive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveTrailingStop(this StockData stockData, int length = 100, double factor = 3)
    {
        List<double> upList = new(stockData.Count);
        List<double> dnList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<double> tsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var perList = CalculatePoweredKaufmanAdaptiveMovingAverage(stockData, length, factor).OutputValues["Per"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var per = perList[i];

            var prevA = i >= 1 ? GetLastOrDefault(aList) : currentValue;
            var a = Math.Max(currentValue, prevA) - (Math.Abs(currentValue - prevA) * per);
            aList.Add(a);

            var prevB = i >= 1 ? GetLastOrDefault(bList) : currentValue;
            var b = Math.Min(currentValue, prevB) + (Math.Abs(currentValue - prevB) * per);
            bList.Add(b);

            var prevUp = GetLastOrDefault(upList);
            var up = a > prevA ? a : a < prevA && b < prevB ? a : prevUp;
            upList.Add(up);

            var prevDn = GetLastOrDefault(dnList);
            var dn = b < prevB ? b : b > prevB && a > prevA ? b : prevDn;
            dnList.Add(dn);

            var prevOs = GetLastOrDefault(osList);
            var os = up > currentValue ? 1 : dn > currentValue ? 0 : prevOs;
            osList.Add(os);

            var prevTs = GetLastOrDefault(tsList);
            var ts = (os * dn) + ((1 - os) * up);
            tsList.Add(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ts", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.AdaptiveTrailingStop;

        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive autonomous recursive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveAutonomousRecursiveTrailingStop(this StockData stockData, int length = 14, double gamma = 3)
    {
        List<double> tsList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var aamaList = CalculateAdaptiveAutonomousRecursiveMovingAverage(stockData, length, gamma);
        var ma2List = aamaList.CustomValuesList;
        var dList = aamaList.OutputValues["D"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ma2 = ma2List[i];
            var d = dList[i];

            var prevUpper = GetLastOrDefault(upperList);
            var upper = ma2 + d;
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = ma2 - d;
            lowerList.Add(lower);

            var prevOs = GetLastOrDefault(osList);
            var os = currentValue > prevUpper ? 1 : currentValue < prevLower ? 0 : prevOs;
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
        stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chandelier Exit
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    public static StockData CalculateChandelierExit(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22, 
        double mult = 3)
    {
        List<double> chandelierExitLongList = new(stockData.Count);
        List<double> chandelierExitShortList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAvgTrueRange = atrList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevExitLong = GetLastOrDefault(chandelierExitLongList);
            var chandelierExitLong = highestHigh - (currentAvgTrueRange * mult);
            chandelierExitLongList.Add(chandelierExitLong);

            var prevExitShort = GetLastOrDefault(chandelierExitShortList);
            var chandelierExitShort = lowestLow + (currentAvgTrueRange * mult);
            chandelierExitShortList.Add(chandelierExitShort);

            var signal = GetBullishBearishSignal(currentValue - chandelierExitLong, prevValue - prevExitLong, currentValue - chandelierExitShort, prevValue - prevExitShort);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "ExitLong", chandelierExitLongList },
            { "ExitShort", chandelierExitShortList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ChandelierExit;

        return stockData;
    }


    /// <summary>
    /// Calculates the Average True Range Trailing Stops
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length1">The length1.</param>
    /// <param name="length2">The length2.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    public static StockData CalculateAverageTrueRangeTrailingStops(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 63, int length2 = 21, double factor = 3)
    {
        List<double> atrtsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;
        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = emaList[i];
            var currentAtr = atrList[i];
            var prevAtrts = i >= 1 ? GetLastOrDefault(atrtsList) : currentValue;
            var upTrend = currentValue > currentEma;
            var dnTrend = currentValue <= currentEma;

            var atrts = upTrend ? Math.Max(currentValue - (factor * currentAtr), prevAtrts) : dnTrend ?
                Math.Min(currentValue + (factor * currentAtr), prevAtrts) : prevAtrts;
            atrtsList.Add(atrts);

            var signal = GetCompareSignal(currentValue - atrts, prevValue - prevAtrts);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Atrts", atrtsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(atrtsList);
        stockData.IndicatorName = IndicatorName.AverageTrueRangeTrailingStops;

        return stockData;
    }

}

