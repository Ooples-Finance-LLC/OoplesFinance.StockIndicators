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
    /// Calculates the Trend Trigger Factor
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrendTriggerFactor(this StockData stockData, int length = 15)
    {
        List<double> ttfList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevHighest = i >= length ? highestList[i - length] : 0;
            var prevLowest = i >= length ? lowestList[i - length] : 0;
            var buyPower = highest - prevLowest;
            var sellPower = prevHighest - lowest;
            var prevTtf1 = i >= 1 ? ttfList[i - 1] : 0;
            var prevTtf2 = i >= 2 ? ttfList[i - 2] : 0;

            var ttf = buyPower + sellPower != 0 ? 200 * (buyPower - sellPower) / (buyPower + sellPower) : 0;
            ttfList.AddRounded(ttf);

            var signal = GetRsiSignal(ttf - prevTtf1, prevTtf1 - prevTtf2, ttf, prevTtf1, 100, -100);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ttf", ttfList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ttfList;
        stockData.IndicatorName = IndicatorName.TrendTriggerFactor;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Persistence Rate
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <param name="mult"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public static StockData CalculateTrendPersistenceRate(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, int smoothLength = 5, double mult = 0.01, double threshold = 1)
    {
        List<double> ctrPList = new();
        List<double> ctrMList = new();
        List<double> tprList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, smoothLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevMa1 = i >= 1 ? maList[i - 1] : 0;
            var prevMa2 = i >= 2 ? maList[i - 2] : 0;
            var diff = (prevMa1 - prevMa2) / mult;

            double ctrP = diff > threshold ? 1 : 0;
            ctrPList.AddRounded(ctrP);

            double ctrM = diff < -threshold ? 1 : 0;
            ctrMList.AddRounded(ctrM);

            var ctrPSum = ctrPList.TakeLastExt(length).Sum();
            var ctrMSum = ctrMList.TakeLastExt(length).Sum();

            var tpr = length != 0 ? Math.Abs(100 * (ctrPSum - ctrMSum) / length) : 0;
            tprList.AddRounded(tpr);
        }

        var tprMaList = GetMovingAverageList(stockData, maType, smoothLength, tprList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tpr = tprList[i];
            var tprMa = tprMaList[i];
            var prevTpr = i >= 1 ? tprList[i - 1] : 0;
            var prevTprMa = i >= 1 ? tprMaList[i - 1] : 0;

            var signal = GetCompareSignal(tpr - tprMa, prevTpr - prevTprMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tpr", tprList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tprList;
        stockData.IndicatorName = IndicatorName.TrendPersistenceRate;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Step
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrendStep(this StockData stockData, int length = 50)
    {
        List<double> aList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dev = stdDevList[i] * 2;

            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var a = i < length ? currentValue : currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue : prevA;
            aList.AddRounded(a);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ts", aList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = aList;
        stockData.IndicatorName = IndicatorName.TrendStep;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Exhaustion Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrendExhaustionIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 10)
    {
        List<double> teiList = new();
        List<double> aCountList = new();
        List<double> hCountList = new();
        List<double> aList = new();
        List<double> hList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, _, _, _) = GetInputValuesList(stockData);
        var (highestList, _) = GetMaxAndMinValuesList(highList, length);

        var sc = (double)2 / (length + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var currentHigh = highList[i];

            double a = currentValue > prevValue ? 1 : 0;
            aList.AddRounded(a);

            double h = currentHigh > prevHighest ? 1 : 0;
            hList.AddRounded(h);

            var aCount = aList.Sum();
            aCountList.AddRounded(aCount);

            var hCount = hList.Sum();
            hCountList.AddRounded(hCount);

            var haRatio = aCount != 0 ? hCount / aCount : 0;
            var prevTei = teiList.LastOrDefault();
            var tei = prevTei + (sc * (haRatio - prevTei));
            teiList.AddRounded(tei);
        }

        var teiSignalList = GetMovingAverageList(stockData, maType, length, teiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tei = teiList[i];
            var teiSignal = teiSignalList[i];
            var prevTei = i >= 1 ? teiList[i - 1] : 0;
            var prevTeiSignal = i >= 1 ? teiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(tei - teiSignal, prevTei - prevTeiSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tei", teiList },
            { "Signal", teiSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = teiList;
        stockData.IndicatorName = IndicatorName.TrendExhaustionIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Impulse Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTrendImpulseFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 100, int length2 = 10)
    {
        List<double> bList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevB = i >= 1 ? bList[i - 1] : currentValue;
            var highest = i >= 1 ? highestList[i - 1] : 0;
            var lowest = i >= 1 ? lowestList[i - 1] : 0;
            double a = currentValue > highest || currentValue < lowest ? 1 : 0;

            var b = (a * currentValue) + ((1 - a) * prevB);
            bList.AddRounded(b);
        }

        var bEmaList = GetMovingAverageList(stockData, maType, length2, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var bEma = bEmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBEma = i >= 1 ? bEmaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - bEma, prevValue - prevBEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tif", bEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bEmaList;
        stockData.IndicatorName = IndicatorName.TrendImpulseFilter;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Analysis Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTrendAnalysisIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 28, int length2 = 5)
    {
        List<double> taiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(smaList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];

            var tai = currentValue != 0 ? (highest - lowest) * 100 / currentValue : 0;
            taiList.AddRounded(tai);
        }

        var taiMaList = GetMovingAverageList(stockData, maType, length2, taiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var tai = taiList[i];
            var taiSma = taiMaList[i];

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, tai, taiSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tai", taiList },
            { "Signal", taiMaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = taiList;
        stockData.IndicatorName = IndicatorName.TrendAnalysisIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Analysis Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTrendAnalysisIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 21, int length2 = 4)
    {
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var slowMaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var fastMaList = GetMovingAverageList(stockData, maType, length2, inputList);
        stockData.CustomValuesList = slowMaList;
        var taiList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        var taiSmaList = GetMovingAverageList(stockData, maType, length1, taiList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var tai = taiList[i];
            var fastMa = fastMaList[i];
            var slowMa = slowMaList[i];
            var taiMa = taiSmaList[i];
            var prevFastMa = i >= 1 ? fastMaList[i - 1] : 0;
            var prevSlowMa = i >= 1 ? slowMaList[i - 1] : 0;

            var signal = GetVolatilitySignal(fastMa - slowMa, prevFastMa - prevSlowMa, tai, taiMa);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tai", taiList },
            { "Signal", taiSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = taiList;
        stockData.IndicatorName = IndicatorName.TrendAnalysisIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trender
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="atrMult"></param>
    /// <returns></returns>
    public static StockData CalculateTrender(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14,
        double atrMult = 2)
    {
        List<double> adList = new();
        List<double> trndDnList = new();
        List<double> trndUpList = new();
        List<double> trndrList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = atrList;
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var mpEma = emaList[i];
            var trEma = atrList[i];

            var ad = currentValue > prevValue ? mpEma + (trEma / 2) : currentValue < prevValue ? mpEma - (trEma / 2) : mpEma;
            adList.AddRounded(ad);
        }

        var admList = GetMovingAverageList(stockData, maType, length, adList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var adm = admList[i];
            var prevAdm = i >= 1 ? admList[i - 1] : 0;
            var mpEma = emaList[i];
            var prevMpEma = i >= 1 ? emaList[i - 1] : 0;
            var prevHigh = i >= 2 ? highList[i - 2] : 0;
            var prevLow = i >= 2 ? lowList[i - 2] : 0;
            var stdDev = stdDevList[i];

            var prevTrndDn = trndDnList.LastOrDefault();
            var trndDn = adm < mpEma && prevAdm > prevMpEma ? prevHigh : currentValue < prevValue ? currentValue + (stdDev * atrMult) : prevTrndDn;
            trndDnList.AddRounded(trndDn);

            var prevTrndUp = trndUpList.LastOrDefault();
            var trndUp = adm > mpEma && prevAdm < prevMpEma ? prevLow : currentValue > prevValue ? currentValue - (stdDev * atrMult) : prevTrndUp;
            trndUpList.AddRounded(trndUp);

            var prevTrndr = trndrList.LastOrDefault();
            var trndr = adm < mpEma ? trndDn : adm > mpEma ? trndUp : prevTrndr;
            trndrList.AddRounded(trndr);

            var signal = GetCompareSignal(currentValue - trndr, prevValue - prevTrndr);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "TrendUp", trndUpList },
            { "TrendDn", trndDnList },
            { "Trender", trndrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trndrList;
        stockData.IndicatorName = IndicatorName.Trender;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Direction Force Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTrendDirectionForceIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 10, int length2 = 30)
    {
        List<double> srcList = new();
        List<double> absTdfList = new();
        List<double> tdfiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var halfLength = MinOrMax((int)Math.Ceiling((double)length1 / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i] * 1000;
            srcList.AddRounded(currentValue);
        }

        var ema1List = GetMovingAverageList(stockData, maType, halfLength, srcList);
        var ema2List = GetMovingAverageList(stockData, maType, halfLength, ema1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema1 = ema1List[i];
            var ema2 = ema2List[i];
            var prevEma1 = i >= 1 ? ema1List[i - 1] : 0;
            var prevEma2 = i >= 1 ? ema2List[i - 1] : 0;
            var ema1Diff = ema1 - prevEma1;
            var ema2Diff = ema2 - prevEma2;
            var emaDiffAvg = (ema1Diff + ema2Diff) / 2;

            double tdf;
            try
            {
                tdf = Math.Abs(ema1 - ema2) * Pow(emaDiffAvg, 3);
            }
            catch (OverflowException)
            {
                tdf = double.MaxValue;
            }

            var absTdf = Math.Abs(tdf);
            absTdfList.AddRounded(absTdf);

            var tdfh = absTdfList.TakeLastExt(length2).Max();
            var prevTdfi = tdfiList.LastOrDefault();
            var tdfi = tdfh != 0 ? tdf / tdfh : 0;
            tdfiList.AddRounded(tdfi);

            var signal = GetCompareSignal(tdfi, prevTdfi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tdfi", tdfiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tdfiList;
        stockData.IndicatorName = IndicatorName.TrendDirectionForceIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Intensity Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateTrendIntensityIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 30, int slowLength = 60)
    {
        List<double> tiiList = new();
        List<double> deviationUpList = new();
        List<double> deviationDownList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevTii1 = i >= 1 ? tiiList[i - 1] : 0;
            var prevTii2 = i >= 2 ? tiiList[i - 2] : 0;

            var deviationUp = currentValue > currentSma ? currentValue - currentSma : 0;
            deviationUpList.AddRounded(deviationUp);

            var deviationDown = currentValue < currentSma ? currentSma - currentValue : 0;
            deviationDownList.AddRounded(deviationDown);

            var sdPlus = deviationUpList.TakeLastExt(fastLength).Sum();
            var sdMinus = deviationDownList.TakeLastExt(fastLength).Sum();
            var tii = sdPlus + sdMinus != 0 ? sdPlus / (sdPlus + sdMinus) * 100 : 0;
            tiiList.AddRounded(tii);

            var signal = GetRsiSignal(tii - prevTii1, prevTii1 - prevTii2, tii, prevTii1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tii", tiiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tiiList;
        stockData.IndicatorName = IndicatorName.TrendIntensityIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Force Histogram
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrendForceHistogram(this StockData stockData, int length = 14)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> cList = new();
        List<double> dList = new();
        List<double> avgList = new();
        List<double> oscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, length, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var highest = i >= 1 ? highestList[i - 1] : 0;
            var lowest = i >= 1 ? lowestList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevA = aList.LastOrDefault();
            double a = currentValue > highest ? 1 : 0;
            aList.AddRounded(a);

            var prevB = bList.LastOrDefault();
            double b = currentValue < lowest ? 1 : 0;
            bList.AddRounded(b);

            var prevC = cList.LastOrDefault();
            var c = a == 1 ? prevC + 1 : b - prevB == 1 ? 0 : prevC;
            cList.AddRounded(c);

            var prevD = dList.LastOrDefault();
            var d = b == 1 ? prevD + 1 : a - prevA == 1 ? 0 : prevD;
            dList.AddRounded(d);

            var avg = (c + d) / 2;
            avgList.AddRounded(avg);

            var rmean = i != 0 ? avgList.Sum() / i : 0;
            var osc = avg - rmean;
            oscList.AddRounded(osc);

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, osc, 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tfh", oscList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = oscList;
        stockData.IndicatorName = IndicatorName.TrendForceHistogram;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Detection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateTrendDetectionIndex(this StockData stockData, int length1 = 20, int length2 = 40)
    {
        List<double> tdiList = new();
        List<double> momList = new();
        List<double> tdiDirectionList = new();
        List<double> momAbsList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;

            var mom = MinPastValues(i, length1, currentValue - prevValue);
            momList.AddRounded(mom);

            var momAbs = Math.Abs(mom);
            momAbsList.AddRounded(momAbs);

            var prevTdiDirection = tdiDirectionList.LastOrDefault();
            var tdiDirection = momList.TakeLastExt(length1).Sum();
            tdiDirectionList.AddRounded(tdiDirection);

            var momAbsSum1 = momAbsList.TakeLastExt(length1).Sum();
            var momAbsSum2 = momAbsList.TakeLastExt(length2).Sum();

            var prevTdi = tdiList.LastOrDefault();
            var tdi = Math.Abs(tdiDirection) - momAbsSum2 + momAbsSum1;
            tdiList.AddRounded(tdi);

            var signal = GetCompareSignal(tdiDirection - tdi, prevTdiDirection - prevTdi);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tdi", tdiList },
            { "TdiDirection", tdiDirectionList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tdiList;
        stockData.IndicatorName = IndicatorName.TrendDetectionIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trend Continuation Factor
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTrendContinuationFactor(this StockData stockData, int length = 35)
    {
        List<double> tcfPlusList = new();
        List<double> tcfMinusList = new();
        List<double> cfPlusList = new();
        List<double> cfMinusList = new();
        List<double> diffPlusList = new();
        List<double> diffMinusList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priceChg = MinPastValues(i, 1, currentValue - prevValue);
            var chgPlus = priceChg > 0 ? priceChg : 0;
            var chgMinus = priceChg < 0 ? Math.Abs(priceChg) : 0;

            var prevCfPlus = cfPlusList.LastOrDefault();
            var cfPlus = chgPlus == 0 ? 0 : chgPlus + prevCfPlus;
            cfPlusList.AddRounded(cfPlus);

            var prevCfMinus = cfMinusList.LastOrDefault();
            var cfMinus = chgMinus == 0 ? 0 : chgMinus + prevCfMinus;
            cfMinusList.AddRounded(cfMinus);

            var diffPlus = chgPlus - cfMinus;
            diffPlusList.AddRounded(diffPlus);

            var diffMinus = chgMinus - cfPlus;
            diffMinusList.AddRounded(diffMinus);

            var prevTcfPlus = tcfPlusList.LastOrDefault();
            var tcfPlus = diffPlusList.TakeLastExt(length).Sum();
            tcfPlusList.AddRounded(tcfPlus);

            var prevTcfMinus = tcfMinusList.LastOrDefault();
            var tcfMinus = diffMinusList.TakeLastExt(length).Sum();
            tcfMinusList.AddRounded(tcfMinus);

            var signal = GetCompareSignal(tcfPlus - tcfMinus, prevTcfPlus - prevTcfMinus);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "TcfPlus", tcfPlusList },
            { "TcfMinus", tcfMinusList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.TrendContinuationFactor;

        return stockData;
    }

    /// <summary>
    /// Calculates the Super Trend
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="atrMult">The atr mult.</param>
    /// <returns></returns>
    public static StockData CalculateSuperTrend(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 22, double atrMult = 3)
    {
        List<double> longStopList = new();
        List<double> shortStopList = new();
        List<double> dirList = new();
        List<double> trendList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAtr = atrList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var atrValue = atrMult * currentAtr;
            var tempLongStop = currentValue - atrValue;
            var tempShortStop = currentValue + atrValue;

            var prevLongStop = i >= 1 ? longStopList.LastOrDefault() : tempLongStop;
            var longStop = prevValue > prevLongStop ? Math.Max(tempLongStop, prevLongStop) : tempLongStop;
            longStopList.AddRounded(longStop);

            var prevShortStop = i >= 1 ? shortStopList.LastOrDefault() : tempShortStop;
            var shortStop = prevValue < prevShortStop ? Math.Min(tempShortStop, prevShortStop) : tempShortStop;
            shortStopList.AddRounded(shortStop);

            var prevDir = i >= 1 ? dirList.LastOrDefault() : 1;
            var dir = prevDir == -1 && currentValue > prevShortStop ? 1 : prevDir == 1 && currentValue < prevLongStop ? -1 : prevDir;
            dirList.AddRounded(dir);

            var prevTrend = trendList.LastOrDefault();
            var trend = dir > 0 ? longStop : shortStop;
            trendList.AddRounded(trend);

            var signal = GetCompareSignal(currentValue - trend, prevValue - prevTrend);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Trend", trendList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trendList;
        stockData.IndicatorName = IndicatorName.SuperTrend;

        return stockData;
    }

    /// <summary>
    /// Calculates the Schaff Trend Cycle
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="cycleLength">Length of the cycle.</param>
    /// <returns></returns>
    public static StockData CalculateSchaffTrendCycle(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 23, int slowLength = 50, int cycleLength = 10)
    {
        List<double> macdList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema23List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema50List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma23 = ema23List[i];
            var currentEma50 = ema50List[i];

            var macd = currentEma23 - currentEma50;
            macdList.AddRounded(macd);
        }

        stockData.CustomValuesList = macdList;
        var stcList = CalculateStochasticOscillator(stockData, maType, length: cycleLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stc = stcList[i];
            var prevStc1 = i >= 1 ? stcList[i - 1] : 0;
            var prevStc2 = i >= 2 ? stcList[i - 2] : 0;

            var signal = GetRsiSignal(stc - prevStc1, prevStc1 - prevStc2, stc, prevStc1, 75, 25);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Stc", stcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = stcList;
        stockData.IndicatorName = IndicatorName.SchaffTrendCycle;

        return stockData;
    }

    /// <summary>
    /// Calculates the Uber Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateUberTrendIndicator(this StockData stockData, int length = 14)
    {
        List<double> advList = new();
        List<double> decList = new();
        List<double> advVolList = new();
        List<double> decVolList = new();
        List<double> utiList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];
            var prevUti1 = i >= 1 ? utiList[i - 1] : 0;
            var prevUti2 = i >= 2 ? utiList[i - 2] : 0;

            var adv = i >= 1 && currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            advList.AddRounded(adv);

            var dec = i >= 1 && currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            decList.AddRounded(dec);

            var advSum = advList.TakeLastExt(length).Sum();
            var decSum = decList.TakeLastExt(length).Sum();

            var advVol = i >= 1 && currentValue > prevValue && advSum != 0 ? currentVolume / advSum : 0;
            advVolList.AddRounded(advVol);

            var decVol = i >= 1 && currentValue < prevValue && decSum != 0 ? currentVolume / decSum : 0;
            decVolList.AddRounded(decVol);

            var advVolSum = advVolList.TakeLastExt(length).Sum();
            var decVolSum = decVolList.TakeLastExt(length).Sum();
            var top = decSum != 0 ? advSum / decSum : 0;
            var bot = decVolSum != 0 ? advVolSum / decVolSum : 0;
            var ut = bot != 0 ? top / bot : 0;

            var uti = ut + 1 != 0 ? (ut - 1) / (ut + 1) : 0;
            utiList.AddRounded(uti);

            var signal = GetCompareSignal(uti - prevUti1, prevUti1 - prevUti2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Uti", utiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = utiList;
        stockData.IndicatorName = IndicatorName.UberTrendIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Wave Trend Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateWaveTrendOscillator(this StockData stockData, InputName inputName = InputName.FullTypicalPrice,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 10, int length2 = 21, int smoothLength = 4)
    {
        List<double> absApEsaList = new();
        List<double> ciList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ap = inputList[i];
            var esa = emaList[i];

            var absApEsa = Math.Abs(ap - esa);
            absApEsaList.AddRounded(absApEsa);
        }

        var dList = GetMovingAverageList(stockData, maType, length1, absApEsaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ap = inputList[i];
            var esa = emaList[i];
            var d = dList[i];

            var ci = d != 0 ? (ap - esa) / (0.015 * d) : 0;
            ciList.AddRounded(ci);
        }

        var tciList = GetMovingAverageList(stockData, maType, length2, ciList);
        var wt2List = GetMovingAverageList(stockData, maType, smoothLength, tciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tci = tciList[i];
            var wt2 = wt2List[i];
            var prevTci = i >= 1 ? tciList[i - 1] : 0;
            var prevWt2 = i >= 1 ? wt2List[i - 1] : 0;

            var signal = GetRsiSignal(tci - wt2, prevTci - prevWt2, tci, prevTci, 53, -53);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Wto", tciList },
            { "Signal", wt2List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tciList;
        stockData.IndicatorName = IndicatorName.WaveTrendOscillator;

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
    public static StockData CalculateOptimizedTrendTracker(this StockData stockData, MovingAvgType maType = MovingAvgType.VariableIndexDynamicAverage,
        int length = 2, double percent = 1.4)
    {
        List<double> longStopList = new();
        List<double> shortStopList = new();
        List<double> ottList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ma = maList[i];
            var fark = ma * percent * 0.01;

            var prevLongStop = longStopList.LastOrDefault();
            var longStop = ma - fark;
            longStop = ma > prevLongStop ? Math.Max(longStop, prevLongStop) : longStop;
            longStopList.AddRounded(longStop);

            var prevShortStop = shortStopList.LastOrDefault();
            var shortStop = ma + fark;
            shortStopList.AddRounded(shortStop);

            var prevOtt = ottList.LastOrDefault();
            var mt = ma > prevShortStop ? longStop : ma < prevLongStop ? shortStop : 0;
            var ott = ma > mt ? mt * (200 + percent) / 200 : mt * (200 - percent) / 200;
            ottList.AddRounded(ott);

            var signal = GetCompareSignal(currentValue - ott, prevValue - prevOtt);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Ott", ottList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ottList;
        stockData.IndicatorName = IndicatorName.OptimizedTrendTracker;

        return stockData;
    }

    /// <summary>
    /// Calculates the Gann Trend Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateGannTrendOscillator(this StockData stockData, int length = 3)
    {
        List<double> gannTrendOscillatorList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevHighest1 = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest1 = i >= 1 ? lowestList[i - 1] : 0;
            var prevHighest2 = i >= 2 ? highestList[i - 2] : 0;
            var prevLowest2 = i >= 2 ? lowestList[i - 2] : 0;

            var prevGto = gannTrendOscillatorList.LastOrDefault();
            var gto = prevHighest2 > prevHighest1 && highestHigh > prevHighest1 ? 1 : prevLowest2 < prevLowest1 && lowestLow < prevLowest1 ? -1 : prevGto;
            gannTrendOscillatorList.AddRounded(gto);

            var signal = GetCompareSignal(gto, prevGto);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Gto", gannTrendOscillatorList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = gannTrendOscillatorList;
        stockData.IndicatorName = IndicatorName.GannTrendOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Grand Trend Forecasting
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="forecastLength"></param>
    /// <param name="mult"></param>
    /// <returns></returns>
    public static StockData CalculateGrandTrendForecasting(this StockData stockData, int length = 100, int forecastLength = 200, double mult = 2)
    {
        List<double> upperList = new();
        List<double> lowerList = new();
        List<double> tList = new();
        List<double> trendList = new();
        List<double> chgList = new();
        List<double> fcastList = new();
        List<double> diffList = new();
        List<double> bullSlopeList = new();
        List<double> bearSlopeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevT = i >= length ? tList[i - length] : currentValue;
            var priorT = i >= forecastLength ? tList[i - forecastLength] : 0;
            var prevFcast = i >= forecastLength ? fcastList[i - forecastLength] : 0;
            var prevChg = i >= length ? chgList[i - length] : currentValue;

            var chg = 0.9 * prevT;
            chgList.AddRounded(chg);

            var t = (0.9 * prevT) + (0.1 * currentValue) + (chg - prevChg);
            tList.AddRounded(t);

            var trend = tList.TakeLastExt(length).Average();
            trendList.AddRounded(trend);

            var fcast = t + (t - priorT);
            fcastList.AddRounded(fcast);

            var diff = Math.Abs(currentValue - prevFcast);
            diffList.AddRounded(diff);

            var diffSma = diffList.TakeLastExt(forecastLength).Average();
            var dev = diffSma * mult;

            var upper = fcast + dev;
            upperList.AddRounded(upper);

            var lower = fcast - dev;
            lowerList.AddRounded(lower);

            var prevBullSlope = bullSlopeList.LastOrDefault();
            var bullSlope = currentValue - Math.Max(fcast, Math.Max(t, trend));
            bullSlopeList.AddRounded(bullSlope);

            var prevBearSlope = bearSlopeList.LastOrDefault();
            var bearSlope = currentValue - Math.Min(fcast, Math.Min(t, trend));
            bearSlopeList.AddRounded(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Gtf", trendList },
            { "UpperBand", upperList },
            { "MiddleBand", fcastList },
            { "LowerBand", lowerList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trendList;
        stockData.IndicatorName = IndicatorName.GrandTrendForecasting;

        return stockData;
    }

    /// <summary>
    /// Calculates the Coral Trend Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="cd"></param>
    /// <returns></returns>
    public static StockData CalculateCoralTrendIndicator(this StockData stockData, int length = 21, double cd = 0.4)
    {
        List<double> i1List = new();
        List<double> i2List = new();
        List<double> i3List = new();
        List<double> i4List = new();
        List<double> i5List = new();
        List<double> i6List = new();
        List<double> bfrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var di = ((double)(length - 1) / 2) + 1;
        var c1 = 2 / (di + 1);
        var c2 = 1 - c1;
        var c3 = 3 * ((cd * cd) + (cd * cd * cd));
        var c4 = -3 * ((2 * cd * cd) + cd + (cd * cd * cd));
        var c5 = (3 * cd) + 1 + (cd * cd * cd) + (3 * cd * cd);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevI1 = i1List.LastOrDefault();
            var i1 = (c1 * currentValue) + (c2 * prevI1);
            i1List.AddRounded(i1);

            var prevI2 = i2List.LastOrDefault();
            var i2 = (c1 * i1) + (c2 * prevI2);
            i2List.AddRounded(i2);

            var prevI3 = i3List.LastOrDefault();
            var i3 = (c1 * i2) + (c2 * prevI3);
            i3List.AddRounded(i3);

            var prevI4 = i4List.LastOrDefault();
            var i4 = (c1 * i3) + (c2 * prevI4);
            i4List.AddRounded(i4);

            var prevI5 = i5List.LastOrDefault();
            var i5 = (c1 * i4) + (c2 * prevI5);
            i5List.AddRounded(i5);

            var prevI6 = i6List.LastOrDefault();
            var i6 = (c1 * i5) + (c2 * prevI6);
            i6List.AddRounded(i6);

            var prevBfr = bfrList.LastOrDefault();
            var bfr = (-1 * cd * cd * cd * i6) + (c3 * i5) + (c4 * i4) + (c5 * i3);
            bfrList.AddRounded(bfr);

            var signal = GetCompareSignal(currentValue - bfr, prevValue - prevBfr);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cti", bfrList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = bfrList;
        stockData.IndicatorName = IndicatorName.CoralTrendIndicator;

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
        List<double> priceVolumeTrendList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevPvt = priceVolumeTrendList.LastOrDefault();
            var pvt = prevValue != 0 ? prevPvt + (currentVolume * (MinPastValues(i, 1, currentValue - prevValue) / prevValue)) : prevPvt;
            priceVolumeTrendList.AddRounded(pvt);
        }

        var pvtEmaList = GetMovingAverageList(stockData, maType, length, priceVolumeTrendList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pvt = priceVolumeTrendList[i];
            var pvtEma = pvtEmaList[i];
            var prevPvt = i >= 1 ? priceVolumeTrendList[i - 1] : 0;
            var prevPvtEma = i >= 1 ? pvtEmaList[i - 1] : 0;

            var signal = GetCompareSignal(pvt - pvtEma, prevPvt - prevPvtEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pvt", priceVolumeTrendList },
            { "Signal", pvtEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = priceVolumeTrendList;
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
        List<double> trendList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentValue = inputList[i];

            var period = 0;
            var prevTrend = trendList.LastOrDefault();
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
            trendList.AddRounded(trend);

            var signal = GetCompareSignal(currentValue - trend, prevValue - prevTrend);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pti", trendList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = trendList;
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
        List<double> mpvtList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];
            var rv = currentVolume / 50000;

            var prevMpvt = mpvtList.LastOrDefault();
            var mpvt = prevValue != 0 ? prevMpvt + (rv * MinPastValues(i, 1, currentValue - prevValue) / prevValue) : 0;
            mpvtList.AddRounded(mpvt);
        }

        var mpvtSignalList = GetMovingAverageList(stockData, maType, length, mpvtList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mpvt = mpvtList[i];
            var mpvtSignal = mpvtSignalList[i];
            var prevMpvt = i >= 1 ? mpvtList[i - 1] : 0;
            var prevMpvtSignal = i >= 1 ? mpvtSignalList[i - 1] : 0;

            var signal = GetCompareSignal(mpvt - mpvtSignal, prevMpvt - prevMpvtSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mpvt", mpvtList },
            { "Signal", mpvtSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mpvtList;
        stockData.IndicatorName = IndicatorName.ModifiedPriceVolumeTrend;

        return stockData;
    }
}
