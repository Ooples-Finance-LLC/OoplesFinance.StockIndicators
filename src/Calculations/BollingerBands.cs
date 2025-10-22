//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the bollinger bands.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="stdDevMult">The standard dev mult.</param>
    /// <param name="maType">Average type of the moving.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateBollingerBands(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double stdDevMult = 2)
    {
        List<double> upperBandList = new();
        List<double> lowerBandList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDeviationList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var middleBand = smaList[i];
            var currentValue = inputList[i];
            var currentStdDeviation = stdDeviationList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevMiddleBand = i >= 1 ? smaList[i - 1] : 0;

            var prevUpperBand = upperBandList.LastOrDefault();
            var upperBand = middleBand + (currentStdDeviation * stdDevMult);
            upperBandList.AddRounded(upperBand);

            var prevLowerBand = lowerBandList.LastOrDefault();
            var lowerBand = middleBand - (currentStdDeviation * stdDevMult);
            lowerBandList.AddRounded(lowerBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand, prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.BollingerBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the adaptive price zone indicator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="pct">The PCT.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptivePriceZoneIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 20, double pct = 2)
    {
        List<double> xHLList = new();
        List<double> outerUpBandList = new();
        List<double> outerDnBandList = new();
        List<double> middleBandList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var nP = MinOrMax((int)Math.Ceiling(Sqrt(length)));

        var ema1List = GetMovingAverageList(stockData, maType, nP, inputList);
        var ema2List = GetMovingAverageList(stockData, maType, nP, ema1List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var xHL = currentHigh - currentLow;
            xHLList.AddRounded(xHL);
        }

        var xHLEma1List = GetMovingAverageList(stockData, maType, nP, xHLList);
        var xHLEma2List = GetMovingAverageList(stockData, maType, nP, xHLEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var xVal1 = ema2List[i];
            var xVal2 = xHLEma2List[i];

            var prevUpBand = outerUpBandList.LastOrDefault();
            var outerUpBand = (pct * xVal2) + xVal1;
            outerUpBandList.AddRounded(outerUpBand);

            var prevDnBand = outerDnBandList.LastOrDefault();
            var outerDnBand = xVal1 - (pct * xVal2);
            outerDnBandList.AddRounded(outerDnBand);

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (outerUpBand + outerDnBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, outerUpBand,
                prevUpBand, outerDnBand, prevDnBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", outerUpBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", outerDnBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.AdaptivePriceZoneIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Auto Dispersion Bands
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAutoDispersionBands(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 90, int smoothLength = 140)
    {
        List<double> middleBandList = new();
        List<double> aList = new();
        List<double> bList = new();
        List<double> aMaxList = new();
        List<double> bMinList = new();
        List<double> x2List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var x = MinPastValues(i, length, currentValue - prevValue);

            var x2 = x * x;
            x2List.AddRounded(x2);

            var x2Sma = x2List.TakeLastExt(length).Average();
            var sq = x2Sma >= 0 ? Sqrt(x2Sma) : 0;

            var a = currentValue + sq;
            aList.AddRounded(a);

            var b = currentValue - sq;
            bList.AddRounded(b);

            var aMax = aList.TakeLastExt(length).Max();
            aMaxList.AddRounded(aMax);

            var bMin = bList.TakeLastExt(length).Min();
            bMinList.AddRounded(bMin);
        }

        var aMaList = GetMovingAverageList(stockData, maType, length, aMaxList);
        var upperBandList = GetMovingAverageList(stockData, maType, smoothLength, aMaList);
        var bMaList = GetMovingAverageList(stockData, maType, length, bMinList);
        var lowerBandList = GetMovingAverageList(stockData, maType, smoothLength, bMaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];
            var prevUpperBand = i >= 1 ? upperBandList[i - 1] : 0;
            var prevLowerBand = i >= 1 ? lowerBandList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevMiddleBand = middleBandList.LastOrDefault();
            var middleBand = (upperBand + lowerBand) / 2;
            middleBandList.AddRounded(middleBand);

            var signal = GetBollingerBandsSignal(currentValue - middleBand, prevValue - prevMiddleBand, currentValue, prevValue, upperBand,
                prevUpperBand, lowerBand, prevLowerBand);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperBandList },
            { "MiddleBand", middleBandList },
            { "LowerBand", lowerBandList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.AutoDispersionBands;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bollinger Bands Fibonacci Ratios
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fibRatio1"></param>
    /// <param name="fibRatio2"></param>
    /// <param name="fibRatio3"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsFibonacciRatios(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double fibRatio1 = 1.618, double fibRatio2 = 2.618, double fibRatio3 = 4.236)
    {
        List<double> fibTop3List = new();
        List<double> fibBottom3List = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var sma = smaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var r1 = atr * fibRatio1;
            var r2 = atr * fibRatio2;
            var r3 = atr * fibRatio3;

            var prevFibTop3 = fibTop3List.LastOrDefault();
            var fibTop3 = sma + r3;
            fibTop3List.AddRounded(fibTop3);

            var fibTop2 = sma + r2;
            var fibTop1 = sma + r1;
            var fibBottom1 = sma - r1;
            var fibBottom2 = sma - r2;

            var prevFibBottom3 = fibBottom3List.LastOrDefault();
            var fibBottom3 = sma - r3;
            fibBottom3List.AddRounded(fibBottom3);

            var signal = GetBollingerBandsSignal(currentValue - sma, prevValue - prevSma, currentValue, prevValue, fibTop3, prevFibTop3, 
                fibBottom3, prevFibBottom3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", fibTop3List },
            { "MiddleBand", smaList },
            { "LowerBand", fibBottom3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.BollingerBandsFibonacciRatios;

        return stockData;
    }

    /// <summary>
    /// Calculates the Bollinger Bands Average True Range
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="atrLength"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <returns></returns>
    public static StockData CalculateBollingerBandsAvgTrueRange(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int atrLength = 22, int length = 55, double stdDevMult = 2)
    {
        List<double> atrDevList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var bollingerBands = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBandList = bollingerBands.OutputValues["UpperBand"];
        var lowerBandList = bollingerBands.OutputValues["LowerBand"];
        var emaList = GetMovingAverageList(stockData, maType, atrLength, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, atrLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var currentAtr = atrList[i];
            var upperBand = upperBandList[i];
            var lowerBand = lowerBandList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var bbDiff = upperBand - lowerBand;

            var atrDev = bbDiff != 0 ? currentAtr / bbDiff : 0;
            atrDevList.AddRounded(atrDev);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, atrDev, 0.5);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "AtrDev", atrDevList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = atrDevList;
        stockData.IndicatorName = IndicatorName.BollingerBandsAverageTrueRange;

        return stockData;
    }

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
        List<double> aptrList = new();
        List<double> upperList = new();
        List<double> lowerList = new();
        List<Signal> signalsList = new();
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

            var prevAptr = aptrList.LastOrDefault();
            var aptr = (100 * atrs * ratio) + (prevAptr * (1 - ratio));
            aptrList.AddRounded(aptr);

            var dev = stdDevMult * aptr;
            var prevUpper = upperList.LastOrDefault();
            var upper = basis + (basis * dev / 100);
            upperList.AddRounded(upper);

            var prevLower = lowerList.LastOrDefault();
            var lower = basis - (basis * dev / 100);
            lowerList.AddRounded(lower);

            var signal = GetBollingerBandsSignal(currentValue - basis, prevValue - prevBasis, currentValue, prevValue, upper, prevUpper, lower, prevLower);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", upperList },
            { "MiddleBand", smaList },
            { "LowerBand", lowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> pctBList = new();
        List<Signal> signalsList = new();
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
            pctBList.AddRounded(pctB);

            var signal = GetRsiSignal(pctB - prevPctB1, prevPctB1 - prevPctB2, pctB, prevPctB1, 100, 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "PctB", pctBList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pctBList;
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
        List<double> bbWidthList = new();
        List<Signal> signalsList = new();
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

            var prevBbWidth = bbWidthList.LastOrDefault();
            var bbWidth = middleBand != 0 ? (upperBand - lowerBand) / middleBand : 0;
            bbWidthList.AddRounded(bbWidth);

            var signal = GetVolatilitySignal(currentValue - middleBand, prevValue - prevMiddleBand, bbWidth, prevBbWidth);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "BbWidth", bbWidthList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bbWidthList;
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
        List<double> haOpenList = new();
        List<double> hacList = new();
        List<double> zlhaList = new();
        List<double> percbList = new();
        List<double> ubList = new();
        List<double> lbList = new();
        List<double> percbSignalList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var prevOhlc = i >= 1 ? inputList[i - 1] : 0;

            var prevHaOpen = haOpenList.LastOrDefault();
            var haOpen = (prevOhlc + prevHaOpen) / 2;
            haOpenList.AddRounded(haOpen);

            var haC = (currentValue + haOpen + Math.Max(currentHigh, haOpen) + Math.Min(currentLow, haOpen)) / 4;
            hacList.AddRounded(haC);
        }

        var tma1List = GetMovingAverageList(stockData, maType, smoothLength, hacList);
        var tma2List = GetMovingAverageList(stockData, maType, smoothLength, tma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tma1 = tma1List[i];
            var tma2 = tma2List[i];
            var diff = tma1 - tma2;

            var zlha = tma1 + diff;
            zlhaList.AddRounded(zlha);
        }

        var zlhaTemaList = GetMovingAverageList(stockData, maType, smoothLength, zlhaList);
        stockData.CustomValuesList = zlhaTemaList;
        var zlhaTemaStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length1).CustomValuesList;
        var wmaZlhaTemaList = GetMovingAverageList(stockData, MovingAvgType.WeightedMovingAverage, length1, zlhaTemaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var zihaTema = zlhaTemaList[i];
            var zihaTemaStdDev = zlhaTemaStdDevList[i];
            var wmaZihaTema = wmaZlhaTemaList[i];

            var percb = zihaTemaStdDev != 0 ? (zihaTema + (2 * zihaTemaStdDev) - wmaZihaTema) / (4 * zihaTemaStdDev) * 100 : 0;
            percbList.AddRounded(percb);
        }

        stockData.CustomValuesList = percbList;
        var percbStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = percbList[i];
            var percbStdDev = percbStdDevList[i];
            var prevValue = i >= 1 ? percbList[i - 1] : 0;

            var prevUb = ubList.LastOrDefault();
            var ub = 50 + (stdDevMult * percbStdDev);
            ubList.AddRounded(ub);

            var prevLb = lbList.LastOrDefault();
            var lb = 50 - (stdDevMult * percbStdDev);
            lbList.AddRounded(lb);

            var prevPercbSignal = percbSignalList.LastOrDefault();
            var percbSignal = (ub + lb) / 2;
            percbSignalList.AddRounded(percbSignal);

            var signal = GetBollingerBandsSignal(currentValue - percbSignal, prevValue - prevPercbSignal, currentValue,
                    prevValue, ub, prevUb, lb, prevLb);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "UpperBand", ubList },
            { "MiddleBand", percbList },
            { "LowerBand", lbList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.VervoortModifiedBollingerBandIndicator;

        return stockData;
    }
}