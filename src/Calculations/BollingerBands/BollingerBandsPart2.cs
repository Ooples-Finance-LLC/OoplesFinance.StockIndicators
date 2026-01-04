
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Bollinger Bands using Atr Pct
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bbLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsWithAtrPct(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, int bbLength = 20, double stdDevMult = 2)
    {
        List<double> aptrList = new(stockData.Count);
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var ratio = (double)2 / (length + 1);

        var smaList = GetMovingAverageList(stockData, maType, bbLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var basis = smaList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var lh = currentHigh - currentLow;
            var hc = Math.Abs(currentHigh - prevValue);
            var lc = Math.Abs(currentLow - prevValue);
            var mm = Math.Max(Math.Max(lh, hc), lc);
            var prevBasis = i >= 1 ? smaList[i - 1] : 0;
            var atrs = mm == hc ? hc / (prevValue + (hc / 2)) : mm == lc ? lc / (currentLow + (lc / 2)) : mm == lh ? lh /
                (currentLow + (lh / 2)) : 0;

            var prevAptr = GetLastOrDefault(aptrList);
            var aptr = (100 * atrs * ratio) + (prevAptr * (1 - ratio));
            aptrList.Add(aptr);

            var dev = stdDevMult * aptr;
            var prevUpper = GetLastOrDefault(upperList);
            var upper = basis + (basis * dev / 100);
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = basis - (basis * dev / 100);
            lowerList.Add(lower);

            var signal = GetBollingerBandsSignal(currentValue - basis, prevValue - prevBasis, currentValue, prevValue, upper, prevUpper, lower, prevLower);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BollingerBandsWithAtrPct;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bollinger Bands %B
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsPercentB(this StockData stockData, double stdDevMult = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> pctBList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var bbList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBandList = bbList.OutputValues["UpperBand"];
        var lowerBandList = bbList.OutputValues["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];
            var prevPctB1 = i >= 1 ? pctBList[i - 1] : 0;
            var prevPctB2 = i >= 2 ? pctBList[i - 2] : 0;

            var pctB = upperBand - lowerBand != 0 ? (currentValue - lowerBand) / (upperBand - lowerBand) * 100 : 0;
            pctBList.Add(pctB);

            var signal = GetRsiSignal(pctB - prevPctB1, prevPctB1 - prevPctB2, pctB, prevPctB1, 100, 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "PctB", pctBList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(pctBList);
        stockData.IndicatorName = IndicatorName.BollingerBandsPercentB;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bollinger Bands Width
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsWidth(this StockData stockData, double stdDevMult = 2,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> bbWidthList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var bbList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBandList = bbList.OutputValues["UpperBand"];
        var lowerBandList = bbList.OutputValues["LowerBand"];
        var middleBandList = bbList.OutputValues["MiddleBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];
            var middleBand = middleBandList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? middleBandList[i - 1] : 0;

            var prevBbWidth = GetLastOrDefault(bbWidthList);
            var bbWidth = middleBand != 0 ? (upperBand - lowerBand) / middleBand : 0;
            bbWidthList.Add(bbWidth);

            var signal = GetVolatilitySignal(currentValue - middleBand, prevValue - prevMiddleBand, bbWidth, prevBbWidth);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "BbWidth", bbWidthList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bbWidthList);
        stockData.IndicatorName = IndicatorName.BollingerBandsWidth;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vervoort Modified Bollinger Band Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateVervoortModifiedBollingerBandIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.TripleExponentialMovingAverage, InputName inputName = InputName.FullTypicalPrice, int length1 = 18,
        int length2 = 200, int smoothLength = 8, double stdDevMult = 1.6)
    {
        List<double> haOpenList = new(stockData.Count);
        List<double> hacList = new(stockData.Count);
        List<double> zlhaList = new(stockData.Count);
        List<double> percbList = new(stockData.Count);
        List<double> ubList = new(stockData.Count);
        List<double> lbList = new(stockData.Count);
        List<double> percbSignalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevOhlc = i >= 1 ? inputList[i - 1] : 0;

            var prevHaOpen = GetLastOrDefault(haOpenList);
            var haOpen = (prevOhlc + prevHaOpen) / 2;
            haOpenList.Add(haOpen);

            var haC = (currentValue + haOpen + Math.Max(currentHigh, haOpen) + Math.Min(currentLow, haOpen)) / 4;
            hacList.Add(haC);
        }

        var tma1List = GetMovingAverageList(stockData, maType, smoothLength, hacList);
        var tma2List = GetMovingAverageList(stockData, maType, smoothLength, tma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tma1 = tma1List[i];
            var tma2 = tma2List[i];
            var diff = tma1 - tma2;

            var zlha = tma1 + diff;
            zlhaList.Add(zlha);
        }

        var zlhaTemaList = GetMovingAverageList(stockData, maType, smoothLength, zlhaList);
        stockData.SetCustomValues(zlhaTemaList);
        var zlhaTemaStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;
        var wmaZlhaTemaList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, length1, zlhaTemaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var zihaTema = zlhaTemaList[i];
            var zihaTemaStdDev = zlhaTemaStdDevList[i];
            var wmaZihaTema = wmaZlhaTemaList[i];

            var percb = zihaTemaStdDev != 0 ? (zihaTema + (2 * zihaTemaStdDev) - wmaZihaTema) / (4 * zihaTemaStdDev) * 100 : 0;
            percbList.Add(percb);
        }

        stockData.SetCustomValues(percbList);
        var percbStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = percbList[i];
            var percbStdDev = percbStdDevList[i];
            var prevValue = i >= 1 ? percbList[i - 1] : 0;

            var prevUb = GetLastOrDefault(ubList);
            var ub = 50 + (stdDevMult * percbStdDev);
            ubList.Add(ub);

            var prevLb = GetLastOrDefault(lbList);
            var lb = 50 - (stdDevMult * percbStdDev);
            lbList.Add(lb);

            var prevPercbSignal = GetLastOrDefault(percbSignalList);
            var percbSignal = (ub + lb) / 2;
            percbSignalList.Add(percbSignal);

            var signal = GetBollingerBandsSignal(currentValue - percbSignal, prevValue - prevPercbSignal, currentValue,
                    prevValue, ub, prevUb, lb, prevLb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", ubList },
            { "MiddleBand", percbList },
            { "LowerBand", lbList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VervoortModifiedBollingerBandIndicator;

        return stockData;
    }
}

