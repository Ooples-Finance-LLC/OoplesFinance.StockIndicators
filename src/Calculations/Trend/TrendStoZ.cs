
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
        List<double> ttfList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            ttfList.Add(ttf);

            var signal = GetRsiSignal(ttf - prevTtf1, prevTtf1 - prevTtf2, ttf, prevTtf1, 100, -100);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ttf", ttfList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ttfList);
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
        List<double> ctrPList = new(stockData.Count);
        List<double> ctrMList = new(stockData.Count);
        List<double> tprList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum ctrPSumWindow = new();
        RollingSum ctrMSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var maList = GetMovingAverageList(stockData, maType, smoothLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevMa1 = i >= 1 ? maList[i - 1] : 0;
            var prevMa2 = i >= 2 ? maList[i - 2] : 0;
            var diff = (prevMa1 - prevMa2) / mult;

            double ctrP = diff > threshold ? 1 : 0;
            ctrPList.Add(ctrP);
            ctrPSumWindow.Add(ctrP);

            double ctrM = diff < -threshold ? 1 : 0;
            ctrMList.Add(ctrM);
            ctrMSumWindow.Add(ctrM);

            var ctrPSum = ctrPSumWindow.Sum(length);
            var ctrMSum = ctrMSumWindow.Sum(length);

            var tpr = length != 0 ? Math.Abs(100 * (ctrPSum - ctrMSum) / length) : 0;
            tprList.Add(tpr);
        }

        var tprMaList = GetMovingAverageList(stockData, maType, smoothLength, tprList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tpr = tprList[i];
            var tprMa = tprMaList[i];
            var prevTpr = i >= 1 ? tprList[i - 1] : 0;
            var prevTprMa = i >= 1 ? tprMaList[i - 1] : 0;

            var signal = GetCompareSignal(tpr - tprMa, prevTpr - prevTprMa);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tpr", tprList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tprList);
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
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, length: length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var dev = stdDevList[i] * 2;

            var prevA = i >= 1 ? aList[i - 1] : currentValue;
            var a = i < length ? currentValue : currentValue > prevA + dev ? currentValue : currentValue < prevA - dev ? currentValue : prevA;
            aList.Add(a);

            var signal = GetCompareSignal(currentValue - a, prevValue - prevA);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ts", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
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
        List<double> teiList = new(stockData.Count);
        List<double> aCountList = new(stockData.Count);
        List<double> hCountList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<double> hList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double aCountSum = 0;
        double hCountSum = 0;
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
            aList.Add(a);

            double h = currentHigh > prevHighest ? 1 : 0;
            hList.Add(h);

            aCountSum += a;
            aCountList.Add(aCountSum);

            hCountSum += h;
            hCountList.Add(hCountSum);

            var haRatio = aCountSum != 0 ? hCountSum / aCountSum : 0;
            var prevTei = i >= 1 ? teiList[i - 1] : 0;
            var tei = prevTei + (sc * (haRatio - prevTei));
            teiList.Add(tei);
        }

        var teiSignalList = GetMovingAverageList(stockData, maType, length, teiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tei = teiList[i];
            var teiSignal = teiSignalList[i];
            var prevTei = i >= 1 ? teiList[i - 1] : 0;
            var prevTeiSignal = i >= 1 ? teiSignalList[i - 1] : 0;

            var signal = GetCompareSignal(tei - teiSignal, prevTei - prevTeiSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tei", teiList },
            { "Signal", teiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(teiList);
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
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            bList.Add(b);
        }

        var bEmaList = GetMovingAverageList(stockData, maType, length2, bList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var bEma = bEmaList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevBEma = i >= 1 ? bEmaList[i - 1] : 0;

            var signal = GetCompareSignal(currentValue - bEma, prevValue - prevBEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tif", bEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bEmaList);
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
        List<double> taiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(smaList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];

            var tai = currentValue != 0 ? (highest - lowest) * 100 / currentValue : 0;
            taiList.Add(tai);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tai", taiList },
            { "Signal", taiMaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(taiList);
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
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var slowMaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var fastMaList = GetMovingAverageList(stockData, maType, length2, inputList);
        stockData.SetCustomValues(slowMaList);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tai", taiList },
            { "Signal", taiSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(taiList);
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
        List<double> adList = new(stockData.Count);
        List<double> trndDnList = new(stockData.Count);
        List<double> trndUpList = new(stockData.Count);
        List<double> trndrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        stockData.SetCustomValues(atrList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var mpEma = emaList[i];
            var trEma = atrList[i];

            var ad = currentValue > prevValue ? mpEma + (trEma / 2) : currentValue < prevValue ? mpEma - (trEma / 2) : mpEma;
            adList.Add(ad);
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

            var prevTrndDn = i >= 1 ? trndDnList[i - 1] : 0;
            var trndDn = adm < mpEma && prevAdm > prevMpEma ? prevHigh : currentValue < prevValue ? currentValue + (stdDev * atrMult) : prevTrndDn;
            trndDnList.Add(trndDn);

            var prevTrndUp = i >= 1 ? trndUpList[i - 1] : 0;
            var trndUp = adm > mpEma && prevAdm < prevMpEma ? prevLow : currentValue > prevValue ? currentValue - (stdDev * atrMult) : prevTrndUp;
            trndUpList.Add(trndUp);

            var prevTrndr = i >= 1 ? trndrList[i - 1] : 0;
            var trndr = adm < mpEma ? trndDn : adm > mpEma ? trndUp : prevTrndr;
            trndrList.Add(trndr);

            var signal = GetCompareSignal(currentValue - trndr, prevValue - prevTrndr);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "TrendUp", trndUpList },
            { "TrendDn", trndDnList },
            { "Trender", trndrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trndrList);
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
        List<double> srcList = new(stockData.Count);
        List<double> absTdfList = new(stockData.Count);
        List<double> tdfiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax absTdfWindow = new(length2);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var halfLength = MinOrMax((int)Math.Ceiling((double)length1 / 2));

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i] * 1000;
            srcList.Add(currentValue);
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
            absTdfList.Add(absTdf);
            absTdfWindow.Add(absTdf);

            var tdfh = absTdfWindow.Max;
            var prevTdfi = i >= 1 ? tdfiList[i - 1] : 0;
            var tdfi = tdfh != 0 ? tdf / tdfh : 0;
            tdfiList.Add(tdfi);

            var signal = GetCompareSignal(tdfi, prevTdfi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tdfi", tdfiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tdfiList);
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
        List<double> tiiList = new(stockData.Count);
        List<double> deviationUpList = new(stockData.Count);
        List<double> deviationDownList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum deviationUpSum = new();
        RollingSum deviationDownSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];
            var prevTii1 = i >= 1 ? tiiList[i - 1] : 0;
            var prevTii2 = i >= 2 ? tiiList[i - 2] : 0;

            var deviationUp = currentValue > currentSma ? currentValue - currentSma : 0;
            deviationUpList.Add(deviationUp);
            deviationUpSum.Add(deviationUp);

            var deviationDown = currentValue < currentSma ? currentSma - currentValue : 0;
            deviationDownList.Add(deviationDown);
            deviationDownSum.Add(deviationDown);

            var sdPlus = deviationUpSum.Sum(fastLength);
            var sdMinus = deviationDownSum.Sum(fastLength);
            var tii = sdPlus + sdMinus != 0 ? sdPlus / (sdPlus + sdMinus) * 100 : 0;
            tiiList.Add(tii);

            var signal = GetRsiSignal(tii - prevTii1, prevTii1 - prevTii2, tii, prevTii1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tii", tiiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tiiList);
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
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> cList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<double> avgList = new(stockData.Count);
        List<double> oscList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        double avgSum = 0;
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

            var prevA = i >= 1 ? aList[i - 1] : 0;
            double a = currentValue > highest ? 1 : 0;
            aList.Add(a);

            var prevB = i >= 1 ? bList[i - 1] : 0;
            double b = currentValue < lowest ? 1 : 0;
            bList.Add(b);

            var prevC = i >= 1 ? cList[i - 1] : 0;
            var c = a == 1 ? prevC + 1 : b - prevB == 1 ? 0 : prevC;
            cList.Add(c);

            var prevD = i >= 1 ? dList[i - 1] : 0;
            var d = b == 1 ? prevD + 1 : a - prevA == 1 ? 0 : prevD;
            dList.Add(d);

            var avg = (c + d) / 2;
            avgList.Add(avg);

            avgSum += avg;
            var rmean = i != 0 ? avgSum / i : 0;
            var osc = avg - rmean;
            oscList.Add(osc);

            var signal = GetVolatilitySignal(currentValue - ema, prevValue - prevEma, osc, 0);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tfh", oscList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(oscList);
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
        List<double> tdiList = new(stockData.Count);
        List<double> momList = new(stockData.Count);
        List<double> tdiDirectionList = new(stockData.Count);
        List<double> momAbsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum momSumWindow = new();
        RollingSum momAbsSumWindow = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;

            var mom = MinPastValues(i, length1, currentValue - prevValue);
            momList.Add(mom);
            momSumWindow.Add(mom);

            var momAbs = Math.Abs(mom);
            momAbsList.Add(momAbs);
            momAbsSumWindow.Add(momAbs);

            var prevTdiDirection = i >= 1 ? tdiDirectionList[i - 1] : 0;
            var tdiDirection = momSumWindow.Sum(length1);
            tdiDirectionList.Add(tdiDirection);

            var momAbsSum1 = momAbsSumWindow.Sum(length1);
            var momAbsSum2 = momAbsSumWindow.Sum(length2);

            var prevTdi = i >= 1 ? tdiList[i - 1] : 0;
            var tdi = Math.Abs(tdiDirection) - momAbsSum2 + momAbsSum1;
            tdiList.Add(tdi);

            var signal = GetCompareSignal(tdiDirection - tdi, prevTdiDirection - prevTdi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tdi", tdiList },
            { "TdiDirection", tdiDirectionList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tdiList);
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
        List<double> tcfPlusList = new(stockData.Count);
        List<double> tcfMinusList = new(stockData.Count);
        List<double> cfPlusList = new(stockData.Count);
        List<double> cfMinusList = new(stockData.Count);
        List<double> diffPlusList = new(stockData.Count);
        List<double> diffMinusList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum diffPlusSum = new();
        RollingSum diffMinusSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priceChg = MinPastValues(i, 1, currentValue - prevValue);
            var chgPlus = priceChg > 0 ? priceChg : 0;
            var chgMinus = priceChg < 0 ? Math.Abs(priceChg) : 0;

            var prevCfPlus = i >= 1 ? cfPlusList[i - 1] : 0;
            var cfPlus = chgPlus == 0 ? 0 : chgPlus + prevCfPlus;
            cfPlusList.Add(cfPlus);

            var prevCfMinus = i >= 1 ? cfMinusList[i - 1] : 0;
            var cfMinus = chgMinus == 0 ? 0 : chgMinus + prevCfMinus;
            cfMinusList.Add(cfMinus);

            var diffPlus = chgPlus - cfMinus;
            diffPlusList.Add(diffPlus);
            diffPlusSum.Add(diffPlus);

            var diffMinus = chgMinus - cfPlus;
            diffMinusList.Add(diffMinus);
            diffMinusSum.Add(diffMinus);

            var prevTcfPlus = i >= 1 ? tcfPlusList[i - 1] : 0;
            var tcfPlus = diffPlusSum.Sum(length);
            tcfPlusList.Add(tcfPlus);

            var prevTcfMinus = i >= 1 ? tcfMinusList[i - 1] : 0;
            var tcfMinus = diffMinusSum.Sum(length);
            tcfMinusList.Add(tcfMinus);

            var signal = GetCompareSignal(tcfPlus - tcfMinus, prevTcfPlus - prevTcfMinus);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "TcfPlus", tcfPlusList },
            { "TcfMinus", tcfMinusList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> longStopList = new(stockData.Count);
        List<double> shortStopList = new(stockData.Count);
        List<double> dirList = new(stockData.Count);
        List<double> trendList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevLongStop = i >= 1 ? longStopList[i - 1] : tempLongStop;
            var longStop = prevValue > prevLongStop ? Math.Max(tempLongStop, prevLongStop) : tempLongStop;
            longStopList.Add(longStop);

            var prevShortStop = i >= 1 ? shortStopList[i - 1] : tempShortStop;
            var shortStop = prevValue < prevShortStop ? Math.Min(tempShortStop, prevShortStop) : tempShortStop;
            shortStopList.Add(shortStop);

            var prevDir = i >= 1 ? dirList[i - 1] : 1;
            var dir = prevDir == -1 && currentValue > prevShortStop ? 1 : prevDir == 1 && currentValue < prevLongStop ? -1 : prevDir;
            dirList.Add(dir);

            var prevTrend = i >= 1 ? trendList[i - 1] : 0;
            var trend = dir > 0 ? longStop : shortStop;
            trendList.Add(trend);

            var signal = GetCompareSignal(currentValue - trend, prevValue - prevTrend);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Trend", trendList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(trendList);
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
        List<double> macdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var ema23List = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var ema50List = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEma23 = ema23List[i];
            var currentEma50 = ema50List[i];

            var macd = currentEma23 - currentEma50;
            macdList.Add(macd);
        }

        stockData.SetCustomValues(macdList);
        var stcList = CalculateStochasticOscillator(stockData, maType, length: cycleLength).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stc = stcList[i];
            var prevStc1 = i >= 1 ? stcList[i - 1] : 0;
            var prevStc2 = i >= 2 ? stcList[i - 2] : 0;

            var signal = GetRsiSignal(stc - prevStc1, prevStc1 - prevStc2, stc, prevStc1, 75, 25);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Stc", stcList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stcList);
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
        List<double> advList = new(stockData.Count);
        List<double> decList = new(stockData.Count);
        List<double> advVolList = new(stockData.Count);
        List<double> decVolList = new(stockData.Count);
        List<double> utiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum advSumWindow = new();
        RollingSum decSumWindow = new();
        RollingSum advVolSumWindow = new();
        RollingSum decVolSumWindow = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentVolume = volumeList[i];
            var prevUti1 = i >= 1 ? utiList[i - 1] : 0;
            var prevUti2 = i >= 2 ? utiList[i - 2] : 0;

            var adv = i >= 1 && currentValue > prevValue ? MinPastValues(i, 1, currentValue - prevValue) : 0;
            advList.Add(adv);
            advSumWindow.Add(adv);

            var dec = i >= 1 && currentValue < prevValue ? MinPastValues(i, 1, prevValue - currentValue) : 0;
            decList.Add(dec);
            decSumWindow.Add(dec);

            var advSum = advSumWindow.Sum(length);
            var decSum = decSumWindow.Sum(length);

            var advVol = i >= 1 && currentValue > prevValue && advSum != 0 ? currentVolume / advSum : 0;
            advVolList.Add(advVol);
            advVolSumWindow.Add(advVol);

            var decVol = i >= 1 && currentValue < prevValue && decSum != 0 ? currentVolume / decSum : 0;
            decVolList.Add(decVol);
            decVolSumWindow.Add(decVol);

            var advVolSum = advVolSumWindow.Sum(length);
            var decVolSum = decVolSumWindow.Sum(length);
            var top = decSum != 0 ? advSum / decSum : 0;
            var bot = decVolSum != 0 ? advVolSum / decVolSum : 0;
            var ut = bot != 0 ? top / bot : 0;

            var uti = ut + 1 != 0 ? (ut - 1) / (ut + 1) : 0;
            utiList.Add(uti);

            var signal = GetCompareSignal(uti - prevUti1, prevUti1 - prevUti2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Uti", utiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(utiList);
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
        List<double> absApEsaList = new(stockData.Count);
        List<double> ciList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _, _) = GetInputValuesList(inputName, stockData);

        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var ap = inputList[i];
            var esa = emaList[i];

            var absApEsa = Math.Abs(ap - esa);
            absApEsaList.Add(absApEsa);
        }

        var dList = GetMovingAverageList(stockData, maType, length1, absApEsaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ap = inputList[i];
            var esa = emaList[i];
            var d = dList[i];

            var ci = d != 0 ? (ap - esa) / (0.015 * d) : 0;
            ciList.Add(ci);
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
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Wto", tciList },
            { "Signal", wt2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tciList);
        stockData.IndicatorName = IndicatorName.WaveTrendOscillator;

        return stockData;
    }

}

