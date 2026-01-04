
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the standard deviation channel.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="stdDevMult">The standard dev mult.</param>
    /// <returns></returns>
    public static StockData CalculateStandardDeviationChannel(this StockData stockData, int length = 40, double stdDevMult = 2)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDeviationList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;
        var regressionList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = regressionList[i];
            var currentValue = inputList[i];
            var currentStdDev = stdDeviationList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? regressionList[i - 1] : 0;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + (currentStdDev * stdDevMult);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - (currentStdDev * stdDevMult);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", regressionList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.StandardDeviationChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the stoller average range channels.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrMult">The atr mult.</param>
    /// <returns></returns>
    public static StockData CalculateStollerAverageRangeChannels(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 14, double atrMult = 2)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentAtr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = middleBand + (currentAtr * atrMult);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = middleBand - (currentAtr * atrMult);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.StollerAverageRangeChannels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Ultimate Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="minLength"></param>
    /// <param name="maxLength"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateUltimateMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int minLength = 5, int maxLength = 50, double stdDevMult = 2)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var umaList = CalculateUltimateMovingAverage(stockData, maType, minLength, maxLength, 1).CustomValuesList;
        var stdevList = CalculateStandardDeviationVolatility(stockData, maType, minLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevVal = i >= 1 ? inputList[i - 1] : 0;
            var uma = umaList[i];
            var prevUma = i >= 1 ? umaList[i - 1] : 0;
            var stdev = stdevList[i];

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = uma + (stdDevMult * stdev);
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = uma - (stdDevMult * stdev);
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - uma, prevVal - prevUma, currentValue, prevVal, upperBand, prevUpperBand,
                lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", umaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.UltimateMovingAverageBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Uni Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="ubFac"></param>
    /// <param name="lbFac"></param>
    /// <param name="type1"></param>
    /// <returns></returns>
    public static StockData CalculateUniChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 10, double ubFac = 0.02, double lbFac = 0.02, bool type1 = false)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var prevUb = GetLastOrDefault(upperBandList);
            var ub = type1 ? currentSma + ubFac : currentSma + (currentSma * ubFac);
            upperBandList.Add(ub);

            var prevLb = GetLastOrDefault(lowerBandList);
            var lb = type1 ? currentSma - lbFac : currentSma - (currentSma * lbFac);
            lowerBandList.Add(lb);

            var signal = GetBollingerBandsSignal(currentValue - currentSma, prevValue - prevSma, currentValue, prevValue, ub, prevUb, lb, prevLb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.UniChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Wilson Relative Price Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="overbought"></param>
    /// <param name="oversold"></param>
    /// <param name="upperNeutralZone"></param>
    /// <param name="lowerNeutralZone"></param>
    /// <returns></returns>
    public static StockData CalculateWilsonRelativePriceChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 34, int smoothLength = 1, double overbought = 70, double oversold = 30, double upperNeutralZone = 55,
        double lowerNeutralZone = 45)
    {
        List<double> rsiOverboughtList = new(stockData.Count);
        List<double> rsiOversoldList = new(stockData.Count);
        List<double> rsiUpperNeutralZoneList = new(stockData.Count);
        List<double> rsiLowerNeutralZoneList = new(stockData.Count);
        List<double> s1List = new(stockData.Count);
        List<double> s2List = new(stockData.Count);
        List<double> u1List = new(stockData.Count);
        List<double> u2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length, smoothLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];

            var rsiOverbought = rsi - overbought;
            rsiOverboughtList.Add(rsiOverbought);

            var rsiOversold = rsi - oversold;
            rsiOversoldList.Add(rsiOversold);

            var rsiUpperNeutralZone = rsi - upperNeutralZone;
            rsiUpperNeutralZoneList.Add(rsiUpperNeutralZone);

            var rsiLowerNeutralZone = rsi - lowerNeutralZone;
            rsiLowerNeutralZoneList.Add(rsiLowerNeutralZone);
        }

        var obList = GetMovingAverageList(stockData, maType, smoothLength, rsiOverboughtList);
        var osList = GetMovingAverageList(stockData, maType, smoothLength, rsiOversoldList);
        var nzuList = GetMovingAverageList(stockData, maType, smoothLength, rsiUpperNeutralZoneList);
        var nzlList = GetMovingAverageList(stockData, maType, smoothLength, rsiLowerNeutralZoneList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ob = obList[i];
            var os = osList[i];
            var nzu = nzuList[i];
            var nzl = nzlList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevS1 = GetLastOrDefault(s1List);
            var s1 = currentValue - (currentValue * os / 100);
            s1List.Add(s1);

            var prevU1 = GetLastOrDefault(u1List);
            var u1 = currentValue - (currentValue * ob / 100);
            u1List.Add(u1);

            var prevU2 = GetLastOrDefault(u2List);
            var u2 = currentValue - (currentValue * nzu / 100);
            u2List.Add(u2);

            var prevS2 = GetLastOrDefault(s2List);
            var s2 = currentValue - (currentValue * nzl / 100);
            s2List.Add(s2);

            var signal = GetBullishBearishSignal(currentValue - Math.Min(u1, u2), prevValue - Math.Min(prevU1, prevU2),
                currentValue - Math.Max(s1, s2), prevValue - Math.Max(prevS1, prevS2));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "S1", s1List },
            { "S2", s2List },
            { "U1", u1List },
            { "U2", u2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.WilsonRelativePriceChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vortex Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVortexBands(this StockData stockData, MovingAvgType maType = MovingAvgType.McNichollMovingAverage,
        int length = 20)
    {
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var basisList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var basis = basisList[i];
            var currentValue = inputList[i];

            var diff = currentValue - basis;
            diffList.Add(diff);
        }

        var diffMaList = GetMovingAverageList(stockData, maType, length, diffList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffMa = diffMaList[i];
            var basis = basisList[i];
            var dev = 2 * diffMa;

            var upper = basis + dev;
            upperList.Add(upper);

            var lower = basis - dev;
            lowerList.Add(lower);

            var signal = GetConditionSignal(upper > lower && upper > basis, lower > upper && lower > basis);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperList },
            { "MiddleBand", basisList },
            { "LowerBand", lowerList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VortexBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volume Adaptive Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAdaptiveBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 100)
    {
        List<double> upList = new(stockData.Count);
        List<double> dnList = new(stockData.Count);
        List<double> middleBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var aList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var a = Math.Max(aList[i], 1);
            var b = a * -1;

            var prevUp = i >= 1 ? upList[i - 1] : currentValue;
            var up = a != 0 ? (prevUp + (currentValue * a)) / a : 0;
            upList.Add(up);

            var prevDn = i >= 1 ? dnList[i - 1] : currentValue;
            var dn = b != 0 ? (prevDn + (currentValue * b)) / b : 0;
            dnList.Add(dn);
        }

        var upSmaList = GetMovingAverageList(stockData, maType, length, upList);
        var dnSmaList = GetMovingAverageList(stockData, maType, length, dnList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBand = upSmaList[i];
            var lowerBand = dnSmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMiddleBand = GetLastOrDefault(middleBandList);
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.Add(middleBand);

            var signal = GetCompareSignal(currentValue - middleBand, prevValue - prevMiddleBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upSmaList },
            { "MiddleBand", middleBandList },
            { "LowerBand", dnSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VolumeAdaptiveBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Variable Moving Average Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateVariableMovingAverageBands(this StockData stockData, MovingAvgType maType = MovingAvgType.VariableMovingAverage,
        int length = 6, double mult = 1.5)
    {
        List<double> ubandList = new(stockData.Count);
        List<double> lbandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAtr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var vma = maList[i];
            var prevVma = i >= 1 ? maList[i - 1] : 0;
            var o = mult * currentAtr;

            var prevUband = GetLastOrDefault(ubandList);
            var uband = vma + o;
            ubandList.Add(uband);

            var prevLband = GetLastOrDefault(lbandList);
            var lband = vma - o;
            lbandList.Add(lband);

            var signal = GetBollingerBandsSignal(currentValue - vma, prevValue - prevVma, currentValue, prevValue, uband, prevUband, lband, prevLband);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", ubandList },
            { "MiddleBand", maList },
            { "LowerBand", lbandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VariableMovingAverageBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Vervoort Volatility Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="devMult"></param>
    /// <param name="lowBandMult"></param>
    /// <returns></returns>
    public static StockData CalculateVervoortVolatilityBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 8, int length2 = 13, double devMult = 3.55, double lowBandMult = 0.9)
    {
        List<double> typicalList = new(stockData.Count);
        List<double> deviationList = new(stockData.Count);
        List<double> ubList = new(stockData.Count);
        List<double> lbList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> medianAvgSmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum typicalSumWindow = new();
        RollingSum medianAvgSumWindow = new();
        var (inputList, _, lowList, _, _) = GetInputValuesList(stockData);

        var medianAvgList = GetMovingAverageList(stockData, maType, length1, inputList);
        var medianAvgEmaList = GetMovingAverageList(stockData, maType, length1, medianAvgList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var medianAvg = medianAvgList[i];
            tempList.Add(medianAvg);
            medianAvgSumWindow.Add(medianAvg);

            var currentValue = inputList[i];
            var currentLow = lowList[i];
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var typical = currentValue >= prevValue ? currentValue - prevLow : prevValue - currentLow;
            typicalList.Add(typical);
            typicalSumWindow.Add(typical);

            var typicalSma = typicalSumWindow.Average(length2);
            var deviation = devMult * typicalSma;
            deviationList.Add(deviation);

            var medianAvgSma = medianAvgSumWindow.Average(length1);
            medianAvgSmaList.Add(medianAvgSma);
        }

        var devHighList = GetMovingAverageList(stockData, maType, length1, deviationList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var devHigh = devHighList[i];
            var midline = medianAvgSmaList[i];
            var medianAvgEma = medianAvgEmaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMidline = i >= 1 ? medianAvgSmaList[i - 1] : 0;
            var devLow = lowBandMult * devHigh;

            var prevUb = GetLastOrDefault(ubList);
            var ub = medianAvgEma + devHigh;
            ubList.Add(ub);

            var prevLb = GetLastOrDefault(lbList);
            var lb = medianAvgEma - devLow;
            lbList.Add(lb);

            var signal = GetBollingerBandsSignal(currentValue - midline, prevValue - prevMidline, currentValue, prevValue, ub, prevUb, lb, prevLb);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", ubList },
            { "MiddleBand", medianAvgSmaList },
            { "LowerBand", lbList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.VervoortVolatilityBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Trend Trader Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <param name="bandStep"></param>
    /// <returns></returns>
    public static StockData CalculateTrendTraderBands(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 21, double mult = 3, double bandStep = 20)
    {
        List<double> retList = new(stockData.Count);
        List<double> outerUpperBandList = new(stockData.Count);
        List<double> outerLowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var prevAtr = i >= 1 ? atrList[i - 1] : 0;
            var atrMult = prevAtr * mult;
            var highLimit = prevHighest - atrMult;
            var lowLimit = prevLowest + atrMult;

            var ret = close > highLimit && close > lowLimit ? highLimit : close < lowLimit && close < highLimit ? lowLimit : GetLastOrDefault(retList);
            retList.Add(ret);
        }

        var retEmaList = GetMovingAverageList(stockData, maType, length, retList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var retEma = retEmaList[i];
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevRetEma = i >= 1 ? retEmaList[i - 1] : 0;

            var prevOuterUpperBand = GetLastOrDefault(outerUpperBandList);
            var outerUpperBand = retEma + bandStep;
            outerUpperBandList.Add(outerUpperBand);

            var prevOuterLowerBand = GetLastOrDefault(outerLowerBandList);
            var outerLowerBand = retEma - bandStep;
            outerLowerBandList.Add(outerLowerBand);

            var signal = GetBollingerBandsSignal(close - retEma, prevClose - prevRetEma, close, prevClose, outerUpperBand, 
                prevOuterUpperBand, outerLowerBand, prevOuterLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", outerUpperBandList },
            { "MiddleBand", retEmaList },
            { "LowerBand", outerLowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.TrendTraderBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Time and Money Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTimeAndMoneyChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length1 = 41, int length2 = 82)
    {
        List<double> yomList = new(stockData.Count);
        List<double> yomSquaredList = new(stockData.Count);
        List<double> varyomList = new(stockData.Count);
        List<double> somList = new(stockData.Count);
        List<double> chPlus1List = new(stockData.Count);
        List<double> chMinus1List = new(stockData.Count);
        List<double> chPlus2List = new(stockData.Count);
        List<double> chMinus2List = new(stockData.Count);
        List<double> chPlus3List = new(stockData.Count);
        List<double> chMinus3List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var halfLength = MinOrMax((int)Math.Ceiling((double)length1 / 2));

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevBasis = i >= halfLength ? smaList[i - halfLength] : 0;

            var yom = prevBasis != 0 ? 100 * (currentValue - prevBasis) / prevBasis : 0;
            yomList.Add(yom);

            var yomSquared = Pow(yom, 2);
            yomSquaredList.Add(yomSquared);
        }

        var avyomList = GetMovingAverageList(stockData, maType, length2, yomList);
        var yomSquaredSmaList = GetMovingAverageList(stockData, maType, length2, yomSquaredList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var prevVaryom = i >= halfLength ? varyomList[i - halfLength] : 0;
            var avyom = avyomList[i];
            var yomSquaredSma = yomSquaredSmaList[i];

            var varyom = yomSquaredSma - (avyom * avyom);
            varyomList.Add(varyom);

            var som = prevVaryom >= 0 ? Sqrt(prevVaryom) : 0;
            somList.Add(som);
        }

        var sigomList = GetMovingAverageList(stockData, maType, length1, somList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var som = somList[i];
            var prevSom = i >= 1 ? somList[i - 1] : 0;
            var sigom = sigomList[i];
            var prevSigom = i >= 1 ? sigomList[i - 1] : 0;
            var basis = smaList[i];

            var chPlus1 = basis * (1 + (0.01 * sigom));
            chPlus1List.Add(chPlus1);

            var chMinus1 = basis * (1 - (0.01 * sigom));
            chMinus1List.Add(chMinus1);

            var chPlus2 = basis * (1 + (0.02 * sigom));
            chPlus2List.Add(chPlus2);

            var chMinus2 = basis * (1 - (0.02 * sigom));
            chMinus2List.Add(chMinus2);

            var chPlus3 = basis * (1 + (0.03 * sigom));
            chPlus3List.Add(chPlus3);

            var chMinus3 = basis * (1 - (0.03 * sigom));
            chMinus3List.Add(chMinus3);

            var signal = GetCompareSignal(som - sigom, prevSom - prevSigom);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ch+1", chPlus1List },
            { "Ch-1", chMinus1List },
            { "Ch+2", chPlus2List },
            { "Ch-2", chMinus2List },
            { "Ch+3", chPlus3List },
            { "Ch-3", chMinus3List },
            { "Median", sigomList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.TrendTraderBands;

        return stockData;
    }


    /// <summary>
    /// Calculates the Tirone Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTironeLevels(this StockData stockData, int length = 20)
    {
        List<double> tlhList = new(stockData.Count);
        List<double> clhList = new(stockData.Count);
        List<double> blhList = new(stockData.Count);
        List<double> amList = new(stockData.Count);
        List<double> ehList = new(stockData.Count);
        List<double> elList = new(stockData.Count);
        List<double> rhList = new(stockData.Count);
        List<double> rlList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var hh = highestList[i];
            var ll = lowestList[i];

            var tlh = hh - ((hh - ll) / 3);
            tlhList.Add(tlh);

            var clh = ll + ((hh - ll) / 2);
            clhList.Add(clh);

            var blh = ll + ((hh - ll) / 3);
            blhList.Add(blh);

            var prevAm = GetLastOrDefault(amList);
            var am = (hh + ll + currentValue) / 3;
            amList.Add(am);

            var eh = am + (hh - ll);
            ehList.Add(eh);

            var el = am - (hh - ll);
            elList.Add(el);

            var rh = (2 * am) - ll;
            rhList.Add(rh);

            var rl = (2 * am) - hh;
            rlList.Add(rl);

            var signal = GetCompareSignal(currentValue - am, prevValue - prevAm);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tlh", tlhList },
            { "Clh", clhList },
            { "Blh", blhList },
            { "Am", amList },
            { "Eh", ehList },
            { "El", elList },
            { "Rh", rhList },
            { "Rl", rlList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.TironeLevels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Time Series Forecast
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTimeSeriesForecast(this StockData stockData, int length = 500)
    {
        List<double> absDiffList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double absDiffSum = 0;
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var tsList = CalculateLinearRegression(stockData, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ts = tsList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevTs = i >= 1 ? tsList[i - 1] : 0;

            var absDiff = Math.Abs(currentValue - ts);
            absDiffList.Add(absDiff);

            absDiffSum += absDiff;
            var e = i != 0 ? absDiffSum / i : 0;
            var prevA = GetLastOrDefault(aList);
            var a = ts + e;
            aList.Add(a);

            var prevB = GetLastOrDefault(bList);
            var b = ts - e;
            bList.Add(b);

            var signal = GetBollingerBandsSignal(currentValue - ts, prevValue - prevTs, currentValue, prevValue, a, prevA, b, prevB);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aList },
            { "MiddleBand", tsList },
            { "LowerBand", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.TimeSeriesForecast;

        return stockData;
    }


    /// <summary>
    /// Calculates the Smart Envelope
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateSmartEnvelope(this StockData stockData, int length = 14, double factor = 1)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> aSignalList = new(stockData.Count);
        List<double> bSignalList = new(stockData.Count);
        List<double> avgList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var prevASignal = GetLastOrDefault(aSignalList);
            var prevBSignal = GetLastOrDefault(bSignalList);
            var diff = Math.Abs(MinPastValues(i, 1, currentValue - prevValue));

            var a = Math.Max(currentValue, prevA) - (Math.Min(Math.Abs(currentValue - prevA), diff) / length * prevASignal);
            aList.Add(a);

            var b = Math.Min(currentValue, prevB) + (Math.Min(Math.Abs(currentValue - prevB), diff) / length * prevBSignal);
            bList.Add(b);

            var aSignal = b < prevB ? -factor : factor;
            aSignalList.Add(aSignal);

            var bSignal = a > prevA ? -factor : factor;
            bSignalList.Add(bSignal);

            var prevAvg = GetLastOrDefault(avgList);
            var avg = (a + b) / 2;
            avgList.Add(avg);

            var signal = GetCompareSignal(currentValue - avg, prevValue - prevAvg);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", aList },
            { "MiddleBand", avgList },
            { "LowerBand", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.SmartEnvelope;

        return stockData;
    }


    /// <summary>
    /// Calculates the Support Resistance
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateSupportResistance(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        List<double> resList = new(stockData.Count);
        List<double> suppList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var sma = i >= 1 ? smaList[i - 1] : 0;
            var crossAbove = prevValue < sma && currentValue >= sma;
            var crossBelow = prevValue > sma && currentValue <= sma;

            var prevRes = GetLastOrDefault(resList);
            var res = crossBelow ? highest : i >= 1 ? prevRes : highest;
            resList.Add(res);

            var prevSupp = GetLastOrDefault(suppList);
            var supp = crossAbove ? lowest : i >= 1 ? prevSupp : lowest;
            suppList.Add(supp);

            var signal = GetBullishBearishSignal(currentValue - res, prevValue - prevRes, currentValue - supp, prevValue - prevSupp);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Support", suppList },
            { "Resistance", resList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.SupportResistance;

        return stockData;
    }


    /// <summary>
    /// Calculates the Stationary Extrapolated Levels
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateStationaryExtrapolatedLevels(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 200)
    {
        List<double> extList = new(stockData.Count);
        List<double> yList = new(stockData.Count);
        List<double> xList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var priorY = i >= length ? yList[i - length] : 0;
            var priorY2 = i >= length * 2 ? yList[i - (length * 2)] : 0;
            var priorX = i >= length ? xList[i - length] : 0;
            var priorX2 = i >= length * 2 ? xList[i - (length * 2)] : 0;

            double x = i;
            xList.Add(i);

            var y = currentValue - sma;
            yList.Add(y);

            var ext = priorX2 - priorX != 0 && priorY2 - priorY != 0 ? (priorY + ((x - priorX) / (priorX2 - priorX) * (priorY2 - priorY))) / 2 : 0;
            extList.Add(ext);
        }

        var (highestList1, lowestList1) = GetMaxAndMinValuesList(extList, length);
        var (upperBandList, lowerBandList) = GetMaxAndMinValuesList(highestList1, lowestList1, length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var y = yList[i];
            var ext = extList[i];
            var prevY = i >= 1 ? yList[i - 1] : 0;
            var prevExt = i >= 1 ? extList[i - 1] : 0;

            var signal = GetCompareSignal(y - ext, prevY - prevExt);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", yList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.StationaryExtrapolatedLevels;

        return stockData;
    }


    /// <summary>
    /// Calculates the Scalper's Channel
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateScalpersChannel(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 15, 
        int length2 = 20)
    {
        List<double> scalperList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        var smaList = GetMovingAverageList(stockData, maType, length2, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentSma = smaList[i];
            var currentAtr = atrList[i];

            var prevScalper = GetLastOrDefault(scalperList);
            var scalper = Math.PI * currentAtr > 0 ? currentSma - Math.Log(Math.PI * currentAtr) : currentSma;
            scalperList.Add(scalper);

            var signal = GetCompareSignal(currentValue - scalper, prevValue - prevScalper);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", highestList },
            { "MiddleBand", scalperList },
            { "LowerBand", lowestList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ScalpersChannel;

        return stockData;
    }


    /// <summary>
    /// Calculates the Smoothed Volatility Bands
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="deviation"></param>
    /// <param name="bandAdjust"></param>
    /// <returns></returns>
    public static StockData CalculateSmoothedVolatilityBands(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 20, int length2 = 21, double deviation = 2.4, double bandAdjust = 0.9)
    {
        List<double> upperBandList = new(stockData.Count);
        List<double> lowerBandList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrPeriod = (length1 * 2) - 1;

        var atrList = CalculateAverageTrueRange(stockData, maType, atrPeriod).CustomValuesList;
        var maList = GetMovingAverageList(stockData, maType, length1, inputList);
        var middleBandList = GetMovingAverageList(stockData, maType, length2, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var middleBand = middleBandList[i];
            var ma = maList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? middleBandList[i - 1] : 0;
            var atrBuf = atr * deviation;

            var prevUpperBand = GetLastOrDefault(upperBandList);
            var upperBand = currentValue != 0 ? ma + (ma * atrBuf / currentValue) : ma;
            upperBandList.Add(upperBand);

            var prevLowerBand = GetLastOrDefault(lowerBandList);
            var lowerBand = currentValue != 0 ? ma - (ma * atrBuf * bandAdjust / currentValue) : ma;
            lowerBandList.Add(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue,
                upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.SmoothedVolatilityBands;

        return stockData;
    }

}

