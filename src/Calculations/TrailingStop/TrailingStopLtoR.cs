
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
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
        List<double> sarList = new(stockData.Count);
        List<double> nextSarList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            sarList.Add(SAR);

            var prevNextSar = GetLastOrDefault(nextSarList);
            var nextSar = SAR + (af * (ep - SAR));
            nextSarList.Add(nextSar);

            var signal = GetCompareSignal(currentHigh - nextSar, prevHigh1 - prevNextSar);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sar", nextSarList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nextSarList);
        stockData.IndicatorName = IndicatorName.ParabolicSAR;

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
        List<double> aList = new(stockData.Count);
        List<double> osList = new(stockData.Count);
        List<double> tsList = new(stockData.Count);
        List<double> upperList = new(stockData.Count);
        List<double> lowerList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            aList.Add(a);

            var up = a + (Math.Abs(a - prevA) * mult);
            var dn = a - (Math.Abs(a - prevA) * mult);

            var prevUpper = GetLastOrDefault(upperList);
            var upper = up == a ? prevUpper : up;
            upperList.Add(upper);

            var prevLower = GetLastOrDefault(lowerList);
            var lower = dn == a ? prevLower : dn;
            lowerList.Add(lower);

            var prevOs = GetLastOrDefault(osList);
            var os = currentValue > upper ? 1 : currentValue > lower ? 0 : prevOs;
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
        List<double> nrtrList = new(stockData.Count);
        List<double> hpList = new(stockData.Count);
        List<double> lpList = new(stockData.Count);
        List<double> trendList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var pct = length * 0.01;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevTrend = GetLastOrDefault(trendList);
            var prevHp = GetLastOrDefault(hpList);
            var prevLp = GetLastOrDefault(lpList);
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevNrtr = GetLastOrDefault(nrtrList);
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
            trendList.Add(trend);
            hpList.Add(hp);
            lpList.Add(lp);
            nrtrList.Add(nrtr);

            var signal = GetCompareSignal(currentValue - nrtr, prevValue - prevNrtr);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Nrtr", nrtrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(nrtrList);
        stockData.IndicatorName = IndicatorName.NickRypockTrailingReverse;

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
        List<double> stopSList = new(stockData.Count);
        List<double> stopLList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            var pSS = i >= 1 ? GetLastOrDefault(stopSList) : currentClose;
            var pSL = i >= 1 ? GetLastOrDefault(stopLList) : currentClose;

            var stopL = currentHigh > prevHH ? currentHigh - (pct * currentHigh) : pSL;
            stopLList.Add(stopL);

            var stopS = currentLow < prevLL ? currentLow + (pct * currentLow) : pSS;
            stopSList.Add(stopS);

            var signal = GetConditionSignal(prevHigh < stopS && currentHigh > stopS, prevLow > stopL && currentLow < stopL);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "LongStop", stopLList },
            { "ShortStop", stopSList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
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
        List<double> osList = new(stockData.Count);
        List<double> tsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevOs = GetLastOrDefault(osList);
            var os = currentValue > prevA ? 1 : currentValue < prevB ? 0 : prevOs;
            osList.Add(os);

            var prevTs = GetLastOrDefault(tsList);
            var ts = (os * b) + ((1 - os) * a);
            tsList.Add(ts);

            var signal = GetCompareSignal(currentValue - ts, prevValue - prevTs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ts", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.MotionToAttractionTrailingStop;

        return stockData;
    }

}

