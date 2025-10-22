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
    /// Calculates the adaptive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveTrailingStop(this StockData stockData, int length = 100, double factor = 3)
    {
        List<double> upList = new();
        List<double> dnList = new();
        List<double> aList = new();
        List<double> bList = new();
        List<double> osList = new();
        List<double> tsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var perList = CalculatePoweredKaufmanAdaptiveMovingAverage(stockData, length, factor).OutputValues["Per"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var per = perList[i];

            var prevA = i >= 1 ? aList.LastOrDefault() : currentValue;
            var a = Math.Max(currentValue, prevA) - (Math.Abs(currentValue - prevA) * per);
            aList.AddRounded(a);

            var prevB = i >= 1 ? bList.LastOrDefault() : currentValue;
            var b = Math.Min(currentValue, prevB) + (Math.Abs(currentValue - prevB) * per);
            bList.AddRounded(b);

            var prevUp = upList.LastOrDefault();
            var up = a > prevA ? a : a < prevA && b < prevB ? a : prevUp;
            upList.AddRounded(up);

            var prevDn = dnList.LastOrDefault();
            var dn = b < prevB ? b : b > prevB && a > prevA ? b : prevDn;
            dnList.AddRounded(dn);

            var prevOs = osList.LastOrDefault();
            var os = up > currentValue ? 1 : dn > currentValue ? 0 : prevOs;
            osList.AddRounded(os);

            var prevTs = tsList.LastOrDefault();
            var ts = (os * dn) + ((1 - os) * up);
            tsList.AddRounded(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ts", tsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsList;
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
        List<double> tsList = new();
        List<double> osList = new();
        List<double> upperList = new();
        List<double> lowerList = new();
        List<Signal> signalsList = new();
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

            var prevUpper = upperList.LastOrDefault();
            var upper = ma2 + d;
            upperList.AddRounded(upper);

            var prevLower = lowerList.LastOrDefault();
            var lower = ma2 - d;
            lowerList.AddRounded(lower);

            var prevOs = osList.LastOrDefault();
            var os = currentValue > prevUpper ? 1 : currentValue < prevLower ? 0 : prevOs;
            osList.AddRounded(os);

            var prevTs = tsList.LastOrDefault();
            var ts = (os * lower) + ((1 - os) * upper);
            tsList.AddRounded(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ts", tsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsList;
        stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;

        return stockData;
    }

    /// <summary>
    /// Calculates the parabolic sar.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="start">The start.</param>
    /// <param name="increment">The increment.</param>
    /// <param name="maximum">The maximum.</param>
    /// <returns></returns>
    public static StockData CalculateParabolicSAR(this StockData stockData, double start = 0.02, double increment = 0.02, double maximum = 0.2)
    {
        List<double> sarList = new();
        List<double> nextSarList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;

            bool uptrend;
            double ep, prevSAR, prevEP, SAR, af = start;
            if (currentValue > prevValue)
            {
                uptrend = true;
                ep = currentHigh;
                prevSAR = prevLow1;
                prevEP = currentHigh;
            }
            else
            {
                uptrend = false;
                ep = currentLow;
                prevSAR = prevHigh1;
                prevEP = currentLow;
            }
            SAR = prevSAR + (start * (prevEP - prevSAR));

            if (uptrend)
            {
                if (SAR > currentLow)
                {
                    uptrend = false;
                    SAR = Math.Max(ep, currentHigh);
                    ep = currentLow;
                    af = start;
                }
            }
            else
            {
                if (SAR < currentHigh)
                {
                    uptrend = true;
                    SAR = Math.Min(ep, currentLow);
                    ep = currentHigh;
                    af = start;
                }
            }

            if (uptrend)
            {
                if (currentHigh > ep)
                {
                    ep = currentHigh;
                    af = Math.Min(af + increment, maximum);
                }
            }
            else
            {
                if (currentLow < ep)
                {
                    ep = currentLow;
                    af = Math.Min(af + increment, maximum);
                }
            }

            if (uptrend)
            {
                SAR = i > 1 ? Math.Min(SAR, prevLow2) : Math.Min(SAR, prevLow1);
            }
            else
            {
                SAR = i > 1 ? Math.Max(SAR, prevHigh2) : Math.Max(SAR, prevHigh1);
            }
            sarList.AddRounded(SAR);

            var prevNextSar = nextSarList.LastOrDefault();
            var nextSar = SAR + (af * (ep - SAR));
            nextSarList.AddRounded(nextSar);

            var signal = GetCompareSignal(currentHigh - nextSar, prevHigh1 - prevNextSar);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Sar", nextSarList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nextSarList;
        stockData.IndicatorName = IndicatorName.ParabolicSAR;

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
        List<double> chandelierExitLongList = new();
        List<double> chandelierExitShortList = new();
        List<Signal> signalsList = new();
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

            var prevExitLong = chandelierExitLongList.LastOrDefault();
            var chandelierExitLong = highestHigh - (currentAvgTrueRange * mult);
            chandelierExitLongList.AddRounded(chandelierExitLong);

            var prevExitShort = chandelierExitShortList.LastOrDefault();
            var chandelierExitShort = lowestLow + (currentAvgTrueRange * mult);
            chandelierExitShortList.AddRounded(chandelierExitShort);

            var signal = GetBullishBearishSignal(currentValue - chandelierExitLong, prevValue - prevExitLong, currentValue - chandelierExitShort, prevValue - prevExitShort);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "ExitLong", chandelierExitLongList },
            { "ExitShort", chandelierExitShortList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> atrtsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length2).CustomValuesList;
        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = emaList[i];
            var currentAtr = atrList[i];
            var prevAtrts = i >= 1 ? atrtsList.LastOrDefault() : currentValue;
            var upTrend = currentValue > currentEma;
            var dnTrend = currentValue <= currentEma;

            var atrts = upTrend ? Math.Max(currentValue - (factor * currentAtr), prevAtrts) : dnTrend ?
                Math.Min(currentValue + (factor * currentAtr), prevAtrts) : prevAtrts;
            atrtsList.AddRounded(atrts);

            var signal = GetCompareSignal(currentValue - atrts, prevValue - prevAtrts);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Atrts", atrtsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = atrtsList;
        stockData.IndicatorName = IndicatorName.AverageTrueRangeTrailingStops;

        return stockData;
    }

    /// <summary>
    /// Calculates the Linear Trailing Stop
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateLinearTrailingStop(this StockData stockData, int length = 14, double mult = 28)
    {
        List<double> aList = new();
        List<double> osList = new();
        List<double> tsList = new();
        List<double> upperList = new();
        List<double> lowerList = new();
        List<Signal> signalsList = new();
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
            aList.AddRounded(a);

            var up = a + (Math.Abs(a - prevA) * mult);
            var dn = a - (Math.Abs(a - prevA) * mult);

            var prevUpper = upperList.LastOrDefault();
            var upper = up == a ? prevUpper : up;
            upperList.AddRounded(upper);

            var prevLower = lowerList.LastOrDefault();
            var lower = dn == a ? prevLower : dn;
            lowerList.AddRounded(lower);

            var prevOs = osList.LastOrDefault();
            var os = currentValue > upper ? 1 : currentValue > lower ? 0 : prevOs;
            osList.AddRounded(os);

            var prevTs = tsList.LastOrDefault();
            var ts = (os * lower) + ((1 - os) * upper);
            tsList.AddRounded(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ts", tsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsList;
        stockData.IndicatorName = IndicatorName.LinearTrailingStop;

        return stockData;
    }

    /// <summary>
    /// Calculates the Nick Rypock Trailing Reverse
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateNickRypockTrailingReverse(this StockData stockData, int length = 2)
    {
        List<double> nrtrList = new();
        List<double> hpList = new();
        List<double> lpList = new();
        List<double> trendList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var pct = length * 0.01;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevTrend = trendList.LastOrDefault();
            var prevHp = hpList.LastOrDefault();
            var prevLp = lpList.LastOrDefault();
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevNrtr = nrtrList.LastOrDefault();
            double nrtr, hp = 0, lp = 0, trend = 0;
            if (prevTrend >= 0)
            {
                hp = currentValue > prevHp ? currentValue : prevHp;
                nrtr = hp * (1 - pct);

                if (currentValue <= nrtr)
                {
                    trend = -1;
                    lp = currentValue;
                    nrtr = lp * (1 + pct);
                }
            }
            else
            {
                lp = currentValue < prevLp ? currentValue : prevLp;
                nrtr = lp * (1 + pct);

                if (currentValue > nrtr)
                {
                    trend = 1;
                    hp = currentValue;
                    nrtr = hp * (1 - pct);
                }
            }
            trendList.AddRounded(trend);
            hpList.AddRounded(hp);
            lpList.AddRounded(lp);
            nrtrList.AddRounded(nrtr);

            var signal = GetCompareSignal(currentValue - nrtr, prevValue - prevNrtr);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nrtr", nrtrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nrtrList;
        stockData.IndicatorName = IndicatorName.NickRypockTrailingReverse;

        return stockData;
    }

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
        List<double> trendList = new();
        List<double> nextTrendList = new();
        List<double> upList = new();
        List<double> downList = new();
        List<double> htList = new();
        List<Signal> signalsList = new();
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
            var prevNextTrend = nextTrendList.LastOrDefault();
            var prevTrend = trendList.LastOrDefault();
            var prevUp = upList.LastOrDefault();
            var prevDown = downList.LastOrDefault();
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
            trendList.AddRounded(trend);
            nextTrendList.AddRounded(nextTrend);

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
            upList.AddRounded(up);
            downList.AddRounded(down);

            var ht = trend == 0 ? up : down;
            htList.AddRounded(ht);

            var signal = GetConditionSignal(arrowUp != 0 && trend == 0 && prevTrend == 1, arrowDown != 0 && trend == 1 && prevTrend == 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ht", htList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = htList;
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
        List<double> warningLineList = new();
        List<double> dev1List = new();
        List<double> dev2List = new();
        List<double> dev3List = new();
        List<double> dtrList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevClose = i >= 2 ? closeList[i - 2] : 0;
            var prevLow = i >= 2 ? lowList[i - 2] : 0;

            var dtr = Math.Max(Math.Max(currentHigh - prevLow, Math.Abs(currentHigh - prevClose)), Math.Abs(currentLow - prevClose));
            dtrList.AddRounded(dtr);
        }

        var dtrAvgList = GetMovingAverageList(stockData, maType, length, dtrList);
        var smaSlowList = GetMovingAverageList(stockData, maType, slowLength, inputList);
        var smaFastList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        stockData.CustomValuesList = dtrList;
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
            warningLineList.AddRounded(warningLine);

            var dev1 = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev2 * dtrStd) : currentTypicalPrice - dtrAvg - (stdDev2 * dtrStd);
            dev1List.AddRounded(dev1);

            var dev2 = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev3 * dtrStd) : currentTypicalPrice - dtrAvg - (stdDev3 * dtrStd);
            dev2List.AddRounded(dev2);

            var dev3 = maFast < maSlow ? currentTypicalPrice + dtrAvg + (stdDev4 * dtrStd) : currentTypicalPrice - dtrAvg - (stdDev4 * dtrStd);
            dev3List.AddRounded(dev3);

            var signal = GetCompareSignal(maFast - maSlow, prevMaFast - prevMaSlow);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dev1", dev1List },
            { "Dev2", dev2List },
            { "Dev3", dev3List },
            { "WarningLine", warningLineList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
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
        List<double> valList = new();
        List<double> val1List = new();
        List<double> val2List = new();
        List<double> val3List = new();
        List<double> rrangeList = new();
        List<double> priceList = new();
        List<double> trendList = new();
        List<Signal> signalsList = new();
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
            trendList.AddRounded(trend);

            var price = trend == 1 ? currentHigh : currentLow;
            price = trend > 0 ? Math.Max(price, currentHigh) : Math.Min(price, currentLow);
            priceList.AddRounded(price);

            var mmax = Math.Max(Math.Max(currentHigh, prevHigh), prevClose);
            var mmin = Math.Min(Math.Min(currentLow, prevLow), prevClose);
            var rrange = mmax - mmin;
            rrangeList.AddRounded(rrange);
        }

        var rangeAvgList = GetMovingAverageList(stockData, maType, length, rrangeList);
        stockData.CustomValuesList = rrangeList;
        var rangeStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var price = priceList[i];
            var trend = trendList[i];
            var avg = rangeAvgList[i];
            var dev = rangeStdDevList[i];
            var prevPrice = i >= 1 ? priceList[i - 1] : 0;

            var val = (price + ((-1) * trend)) * (avg + (stdDev1 * dev));
            valList.AddRounded(val);

            var val1 = (price + ((-1) * trend)) * (avg + (stdDev2 * dev));
            val1List.AddRounded(val1);

            var val2 = (price + ((-1) * trend)) * (avg + (stdDev3 * dev));
            val2List.AddRounded(val2);

            var prevVal3 = val3List.LastOrDefault();
            var val3 = (price + ((-1) * trend)) * (avg + (stdDev4 * dev));
            val3List.AddRounded(val3);

            var signal = GetCompareSignal(price - val3, prevPrice - prevVal3);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Dev1", valList },
            { "Dev2", val1List },
            { "Dev3", val2List },
            { "Dev4", val3List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.KaseDevStopV2;

        return stockData;
    }

    /// <summary>
    /// Calculates the Percentage Trailing Stops
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="pct"></param>
    /// <returns></returns>
    public static StockData CalculatePercentageTrailingStops(this StockData stockData, int length = 100, double pct = 10)
    {
        List<double> stopSList = new();
        List<double> stopLList = new();
        List<Signal> signalsList = new();
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
            var pSS = i >= 1 ? stopSList.LastOrDefault() : currentClose;
            var pSL = i >= 1 ? stopLList.LastOrDefault() : currentClose;

            var stopL = currentHigh > prevHH ? currentHigh - (pct * currentHigh) : pSL;
            stopLList.AddRounded(stopL);

            var stopS = currentLow < prevLL ? currentLow + (pct * currentLow) : pSS;
            stopSList.AddRounded(stopS);

            var signal = GetConditionSignal(prevHigh < stopS && currentHigh > stopS, prevLow > stopL && currentLow < stopL);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "LongStop", stopLList },
            { "ShortStop", stopSList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PercentageTrailingStops;

        return stockData;
    }

    /// <summary>
    /// Calculates the Motion To Attraction Trailing Stop
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMotionToAttractionTrailingStop(this StockData stockData, int length = 14)
    {
        List<double> osList = new();
        List<double> tsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mtaList = CalculateMotionToAttractionChannels(stockData, length);
        var aList = mtaList.OutputValues["UpperBand"];
        var bList = mtaList.OutputValues["LowerBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var a = aList[i];
            var b = bList[i];

            var prevOs = osList.LastOrDefault();
            var os = currentValue > prevA ? 1 : currentValue < prevB ? 0 : prevOs;
            osList.AddRounded(os);

            var prevTs = tsList.LastOrDefault();
            var ts = (os * b) + ((1 - os) * a);
            tsList.AddRounded(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ts", tsList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tsList;
        stockData.IndicatorName = IndicatorName.MotionToAttractionTrailingStop;

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
        List<double> safeZPlusList = new();
        List<double> safeZMinusList = new();
        List<double> dmPlusCountList = new();
        List<double> dmMinusCountList = new();
        List<double> dmMinusList = new();
        List<double> dmPlusList = new();
        List<double> stopList = new();
        List<Signal> signalsList = new();
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
            dmMinusList.AddRounded(dmMinus);

            double dmMinusCount = prevLow > currentLow ? 1 : 0;
            dmMinusCountList.AddRounded(dmMinusCount);

            var dmPlus = currentHigh > prevHigh ? currentHigh - prevHigh : 0;
            dmPlusList.AddRounded(dmPlus);

            double dmPlusCount = currentHigh > prevHigh ? 1 : 0;
            dmPlusCountList.AddRounded(dmPlusCount);

            var countM = dmMinusCountList.TakeLastExt(length2).Sum();
            var dmMinusSum = dmMinusList.TakeLastExt(length2).Sum();
            var dmAvgMinus = countM != 0 ? dmMinusSum / countM : 0;
            var countP = dmPlusCountList.TakeLastExt(length2).Sum();
            var dmPlusSum = dmPlusList.TakeLastExt(length2).Sum();
            var dmAvgPlus = countP != 0 ? dmPlusSum / countP : 0;

            var safeZMinus = prevLow - (factor * dmAvgMinus);
            safeZMinusList.AddRounded(safeZMinus);

            var safeZPlus = prevHigh + (factor * dmAvgPlus);
            safeZPlusList.AddRounded(safeZPlus);

            var highest = safeZMinusList.TakeLastExt(length3).Max();
            var lowest = safeZPlusList.TakeLastExt(length3).Min();

            var prevStop = stopList.LastOrDefault();
            var stop = currentValue >= currentEma ? highest : lowest;
            stopList.AddRounded(stop);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(currentEma, stop), prevValue - Math.Max(prevEma, prevStop),
                currentValue - Math.Min(currentEma, stop), prevValue - Math.Min(prevEma, prevStop));
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eszs", stopList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stopList;
        stockData.IndicatorName = IndicatorName.ElderSafeZoneStops;

        return stockData;
    }
}