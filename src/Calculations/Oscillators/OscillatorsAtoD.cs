using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    public static StockData CalculateAbsoluteStrengthIndex(this StockData stockData, int length = 10, int maLength = 21, int signalLength = 34)
    {
        List<double> AList = new(stockData.Count);
        List<double> MList = new(stockData.Count);
        List<double> DList = new(stockData.Count);
        List<double> mtList = new(stockData.Count);
        List<double> utList = new(stockData.Count);
        List<double> abssiEmaList = new(stockData.Count);
        List<double> dList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var alp = (double)2 / (signalLength + 1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevA = GetLastOrDefault(AList);
            var A = currentValue > prevValue && prevValue != 0 ? prevA + ((currentValue / prevValue) - 1) : prevA;
            AList.Add(A);

            var prevM = GetLastOrDefault(MList);
            var M = currentValue == prevValue ? prevM + ((double)1 / length) : prevM;
            MList.Add(M);

            var prevD = GetLastOrDefault(DList);
            var D = currentValue < prevValue && currentValue != 0 ? prevD + ((prevValue / currentValue) - 1) : prevD;
            DList.Add(D);

            var abssi = (D + M) / 2 != 0 ? 1 - (1 / (1 + ((A + M) / 2 / ((D + M) / 2)))) : 1;
            var abssiEma = CalculateEMA(abssi, GetLastOrDefault(abssiEmaList), maLength);
            abssiEmaList.Add(abssiEma);

            var abssio = abssi - abssiEma;
            var prevMt = GetLastOrDefault(mtList);
            var mt = (alp * abssio) + ((1 - alp) * prevMt);
            mtList.Add(mt);

            var prevUt = GetLastOrDefault(utList);
            var ut = (alp * mt) + ((1 - alp) * prevUt);
            utList.Add(ut);

            var s = (2 - alp) * (mt - ut) / (1 - alp);
            var prevd = GetLastOrDefault(dList);
            var d = abssio - s;
            dList.Add(d);

            var signal = GetCompareSignal(d, prevd);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Asi", dList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dList);
        stockData.IndicatorName = IndicatorName.AbsoluteStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Accumulative Swing Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAccumulativeSwingIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> accumulativeSwingIndexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevHighCurrentClose = prevHigh - currentClose;
            var prevLowCurrentClose = prevLow - currentClose;
            var prevClosePrevOpen = prevClose - prevOpen;
            var currentHighPrevClose = currentHigh - prevClose;
            var currentLowPrevClose = currentLow - prevClose;
            var t = currentHigh - currentLow;
            var k = Math.Max(Math.Abs(prevHighCurrentClose), Math.Abs(prevLowCurrentClose));
            var r = currentHighPrevClose > Math.Max(currentLowPrevClose, t) ? currentHighPrevClose - (0.5 * currentLowPrevClose) + (0.25 * prevClosePrevOpen) :
                currentLowPrevClose > Math.Max(currentHighPrevClose, t) ? currentLowPrevClose - (0.5 * currentHighPrevClose) + (0.25 * prevClosePrevOpen) :
                t > Math.Max(currentHighPrevClose, currentLowPrevClose) ? t + (0.25 * prevClosePrevOpen) : 0;
            var swingIndex = r != 0 && t != 0 ? 50 * ((prevClose - currentClose + (0.5 * prevClosePrevOpen) +
                                                       (0.25 * (currentClose - currentOpen))) / r) * (k / t) : 0;

            var prevSwingIndex = GetLastOrDefault(accumulativeSwingIndexList);
            var accumulativeSwingIndex = prevSwingIndex + swingIndex;
            accumulativeSwingIndexList.Add(accumulativeSwingIndex);
        }

        var asiOscillatorList = GetMovingAverageList(stockData, maType, length, accumulativeSwingIndexList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var asi = accumulativeSwingIndexList[i];
            var prevAsi = i >= 1 ? accumulativeSwingIndexList[i - 1] : 0;

            var signal = GetCompareSignal(asi, prevAsi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Asi", accumulativeSwingIndexList },
            { "Signal", asiOscillatorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(accumulativeSwingIndexList);
        stockData.IndicatorName = IndicatorName.AccumulativeSwingIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Adaptive Ergodic Candlestick Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <param name="stochLength">Length of the stoch.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveErgodicCandlestickOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int smoothLength = 5, int stochLength = 14, int signalLength = 9)
    {
        List<double> came1List = new(stockData.Count);
        List<double> came2List = new(stockData.Count);
        List<double> came11List = new(stockData.Count);
        List<double> came22List = new(stockData.Count);
        List<double> ecoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        var mep = (double)2 / (smoothLength + 1);
        double ce = (stochLength + smoothLength) * 2;

        var stochList = CalculateStochasticOscillator(stockData, maType, length: stochLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var stoch = stochList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var vrb = Math.Abs(stoch - 50) / 50;

            var prevCame1 = GetLastOrDefault(came1List);
            var came1 = i < ce ? currentClose - currentOpen : prevCame1 + (mep * vrb * (currentClose - currentOpen - prevCame1));
            came1List.Add(came1);

            var prevCame2 = GetLastOrDefault(came2List);
            var came2 = i < ce ? currentHigh - currentLow : prevCame2 + (mep * vrb * (currentHigh - currentLow - prevCame2));
            came2List.Add(came2);

            var prevCame11 = GetLastOrDefault(came11List);
            var came11 = i < ce ? came1 : prevCame11 + (mep * vrb * (came1 - prevCame11));
            came11List.Add(came11);

            var prevCame22 = GetLastOrDefault(came22List);
            var came22 = i < ce ? came2 : prevCame22 + (mep * vrb * (came2 - prevCame22));
            came22List.Add(came22);

            var eco = came22 != 0 ? came11 / came22 * 100 : 0;
            ecoList.Add(eco);
        }

        var seList = GetMovingAverageList(stockData, maType, signalLength, ecoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var eco = ecoList[i];
            var se = seList[i];
            var prevEco = i >= 1 ? ecoList[i - 1] : 0;
            var prevSe = i >= 1 ? seList[i - 1] : 0;

            var signal = GetCompareSignal(eco - se, prevEco - prevSe);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Eco", ecoList },
            { "Signal", seList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ecoList);
        stockData.IndicatorName = IndicatorName.AdaptiveErgodicCandlestickOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Absolute Strength MTF Indicator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAbsoluteStrengthMTFIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int length = 50, int smoothLength = 25)
    {
        List<double> prevValuesList = new(stockData.Count);
        List<double> bulls0List = new(stockData.Count);
        List<double> bears0List = new(stockData.Count);
        List<double> bulls1List = new(stockData.Count);
        List<double> bears1List = new(stockData.Count);
        List<double> bulls2List = new(stockData.Count);
        List<double> bears2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            prevValuesList.Add(prevValue);
        }

        var price1List = GetMovingAverageList(stockData, maType, length, inputList);
        var price2List = GetMovingAverageList(stockData, maType, length, prevValuesList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var price1 = price1List[i];
            var price2 = price2List[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var high = highList[i];
            var low = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var bulls0 = 0.5 * (Math.Abs(price1 - price2) + (price1 - price2));
            bulls0List.Add(bulls0);

            var bears0 = 0.5 * (Math.Abs(price1 - price2) - (price1 - price2));
            bears0List.Add(bears0);

            var bulls1 = price1 - lowest;
            bulls1List.Add(bulls1);

            var bears1 = highest - price1;
            bears1List.Add(bears1);

            var bulls2 = 0.5 * (Math.Abs(high - prevHigh) + (high - prevHigh));
            bulls2List.Add(bulls2);

            var bears2 = 0.5 * (Math.Abs(prevLow - low) + (prevLow - low));
            bears2List.Add(bears2);
        }

        var smthBulls0List = GetMovingAverageList(stockData, maType, smoothLength, bulls0List);
        var smthBears0List = GetMovingAverageList(stockData, maType, smoothLength, bears0List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bulls = smthBulls0List[i];
            var bears = smthBears0List[i];
            var prevBulls = i >= 1 ? smthBulls0List[i - 1] : 0;
            var prevBears = i >= 1 ? smthBears0List[i - 1] : 0;

            var signal = GetCompareSignal(bulls - bears, prevBulls - prevBears);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Bulls", smthBulls0List },
            { "Bears", smthBears0List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.AbsoluteStrengthMTFIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bayesian Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stdDevMult"></param>
    /// <param name="lowerThreshold"></param>
    /// <returns></returns>
    public static StockData CalculateBayesianOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20, double stdDevMult = 2.5, double lowerThreshold = 15)
    {
        List<double> sigmaProbsDownList = new(stockData.Count);
        List<double> sigmaProbsUpList = new(stockData.Count);
        List<double> probPrimeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var probBbUpperUpSumWindow = new RollingSum();
        var probBbUpperDownSumWindow = new RollingSum();
        var probBbBasisUpSumWindow = new RollingSum();
        var probBbBasisDownSumWindow = new RollingSum();

        var bbList = CalculateBollingerBands(stockData, maType, length, stdDevMult);
        var upperBbList = bbList.OutputValues["UpperBand"];
        var basisList = bbList.OutputValues["MiddleBand"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var upperBb = upperBbList[i];
            var basis = basisList[i];

            double probBbUpperUpSeq = currentValue > upperBb ? 1 : 0;
            probBbUpperUpSumWindow.Add(probBbUpperUpSeq);
            var probBbUpperUp = probBbUpperUpSumWindow.Average(length);

            double probBbUpperDownSeq = currentValue < upperBb ? 1 : 0;
            probBbUpperDownSumWindow.Add(probBbUpperDownSeq);
            var probBbUpperDown = probBbUpperDownSumWindow.Average(length);
            var probUpBbUpper = probBbUpperUp + probBbUpperDown != 0 ? probBbUpperUp / (probBbUpperUp + probBbUpperDown) : 0;
            var probDownBbUpper = probBbUpperUp + probBbUpperDown != 0 ? probBbUpperDown / (probBbUpperUp + probBbUpperDown) : 0;

            double probBbBasisUpSeq = currentValue > basis ? 1 : 0;
            probBbBasisUpSumWindow.Add(probBbBasisUpSeq);
            var probBbBasisUp = probBbBasisUpSumWindow.Average(length);

            double probBbBasisDownSeq = currentValue < basis ? 1 : 0;
            probBbBasisDownSumWindow.Add(probBbBasisDownSeq);
            var probBbBasisDown = probBbBasisDownSumWindow.Average(length);

            var probUpBbBasis = probBbBasisUp + probBbBasisDown != 0 ? probBbBasisUp / (probBbBasisUp + probBbBasisDown) : 0;
            var probDownBbBasis = probBbBasisUp + probBbBasisDown != 0 ? probBbBasisDown / (probBbBasisUp + probBbBasisDown) : 0;

            var prevSigmaProbsDown = GetLastOrDefault(sigmaProbsDownList);
            var sigmaProbsDown = probUpBbUpper != 0 && probUpBbBasis != 0 ? ((probUpBbUpper * probUpBbBasis) / (probUpBbUpper * probUpBbBasis)) +
                                                                            ((1 - probUpBbUpper) * (1 - probUpBbBasis)) : 0;
            sigmaProbsDownList.Add(sigmaProbsDown);

            var prevSigmaProbsUp = GetLastOrDefault(sigmaProbsUpList);
            var sigmaProbsUp = probDownBbUpper != 0 && probDownBbBasis != 0 ? ((probDownBbUpper * probDownBbBasis) / (probDownBbUpper * probDownBbBasis)) +
                                                                              ((1 - probDownBbUpper) * (1 - probDownBbBasis)) : 0;
            sigmaProbsUpList.Add(sigmaProbsUp);

            var prevProbPrime = GetLastOrDefault(probPrimeList);
            var probPrime = sigmaProbsDown != 0 && sigmaProbsUp != 0 ? ((sigmaProbsDown * sigmaProbsUp) / (sigmaProbsDown * sigmaProbsUp)) +
                                                                       ((1 - sigmaProbsDown) * (1 - sigmaProbsUp)) : 0;
            probPrimeList.Add(probPrime);

            var longUsingProbPrime = probPrime > lowerThreshold / 100 && prevProbPrime == 0;
            var longUsingSigmaProbsUp = sigmaProbsUp < 1 && prevSigmaProbsUp == 1;
            var shortUsingProbPrime = probPrime == 0 && prevProbPrime > lowerThreshold / 100;
            var shortUsingSigmaProbsDown = sigmaProbsDown < 1 && prevSigmaProbsDown == 1;

            var signal = GetConditionSignal(longUsingProbPrime || longUsingSigmaProbsUp, shortUsingProbPrime || shortUsingSigmaProbsDown);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "SigmaProbsDown", sigmaProbsDownList },
            { "SigmaProbsUp", sigmaProbsUpList },
            { "ProbPrime", probPrimeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BayesianOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bear Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateBearPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> bpiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            var bpi = close < open ? high - low : prevClose > open ? Math.Max(close - open, high - low) :
                close > open ? Math.Max(open - low, high - close) : prevClose > open ? Math.Max(prevClose - low, high - close) :
                high - close > close - low ? high - low : prevClose > open ? Math.Max(prevClose - open, high - low) :
                high - close < close - low ? open - low : close > open ? Math.Max(close - low, high - close) :
                close > open ? Math.Max(prevClose - open, high - close) : prevClose < open ? Math.Max(open - low, high - close) : high - low;
            bpiList.Add(bpi);
        }

        var bpiEmaList = GetMovingAverageList(stockData, maType, length, bpiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bpi = bpiList[i];
            var bpiEma = bpiEmaList[i];
            var prevBpi = i >= 1 ? bpiList[i - 1] : 0;
            var prevBpiEma = i >= 1 ? bpiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(bpi - bpiEma, prevBpi - prevBpiEma, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "BearPower", bpiList },
            { "Signal", bpiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bpiList);
        stockData.IndicatorName = IndicatorName.BearPowerIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Bull Power Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateBullPowerIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> bpiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var close = inputList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var open = openList[i];
            var high = highList[i];
            var low = lowList[i];

            var bpi = close < open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(high - prevClose, close - low) :
                close > open ? Math.Max(open - prevClose, high - low) : prevClose > open ? high - low :
                high - close > close - low ? high - open : prevClose < open ? Math.Max(high - prevClose, close - low) :
                high - close < close - low ? Math.Max(open - close, high - low) : prevClose > open ? high - low :
                prevClose > open ? Math.Max(high - open, close - low) : prevClose < open ? Math.Max(open - close, high - low) : high - low;
            bpiList.Add(bpi);
        }

        var bpiEmaList = GetMovingAverageList(stockData, maType, length, bpiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var bpi = bpiList[i];
            var bpiEma = bpiEmaList[i];
            var prevBpi = i >= 1 ? bpiList[i - 1] : 0;
            var prevBpiEma = i >= 1 ? bpiEmaList[i - 1] : 0;

            var signal = GetCompareSignal(bpi - bpiEma, prevBpi - prevBpiEma, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "BullPower", bpiList },
            { "Signal", bpiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bpiList);
        stockData.IndicatorName = IndicatorName.BullPowerIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Belkhayate Timing
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateBelkhayateTiming(this StockData stockData)
    {
        List<double> bList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh1 = i >= 1 ? highList[i - 1] : 0;
            var prevLow1 = i >= 1 ? lowList[i - 1] : 0;
            var prevHigh2 = i >= 2 ? highList[i - 2] : 0;
            var prevLow2 = i >= 2 ? lowList[i - 2] : 0;
            var prevHigh3 = i >= 3 ? highList[i - 3] : 0;
            var prevLow3 = i >= 3 ? lowList[i - 3] : 0;
            var prevHigh4 = i >= 4 ? highList[i - 4] : 0;
            var prevLow4 = i >= 4 ? lowList[i - 4] : 0;
            var prevB1 = i >= 1 ? bList[i - 1] : 0;
            var prevB2 = i >= 2 ? bList[i - 2] : 0;
            var middle = (((currentHigh + currentLow) / 2) + ((prevHigh1 + prevLow1) / 2) + ((prevHigh2 + prevLow2) / 2) +
                          ((prevHigh3 + prevLow3) / 2) + ((prevHigh4 + prevLow4) / 2)) / 5;
            var scale = ((currentHigh - currentLow + (prevHigh1 - prevLow1) + (prevHigh2 - prevLow2) + (prevHigh3 - prevLow3) +
                          (prevHigh4 - prevLow4)) / 5) * 0.2;

            var b = scale != 0 ? (currentValue - middle) / scale : 0;
            bList.Add(b);

            var signal = GetRsiSignal(b - prevB1, prevB1 - prevB2, b, prevB1, 4, -4);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Belkhayate", bList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(bList);
        stockData.IndicatorName = IndicatorName.BelkhayateTiming;

        return stockData;
    }


    /// <summary>
    /// Calculates the Detrended Price Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDetrendedPriceOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 20)
    {
        List<double> dpoList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var prevPeriods = MinOrMax((int)Math.Ceiling(((double)length / 2) + 1));

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentSma = smaList[i];
            var prevValue = i >= prevPeriods ? inputList[i - prevPeriods] : 0;

            var prevDpo = GetLastOrDefault(dpoList);
            var dpo = prevValue - currentSma;
            dpoList.Add(dpo);

            var signal = GetCompareSignal(dpo, prevDpo);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dpo", dpoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dpoList);
        stockData.IndicatorName = IndicatorName.DetrendedPriceOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chartmill Value Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateChartmillValueIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        InputName inputName = InputName.MedianPrice, int length = 5)
    {
        List<double> cmvCList = new(stockData.Count);
        List<double> cmvOList = new(stockData.Count);
        List<double> cmvHList = new(stockData.Count);
        List<double> cmvLList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, openList, closeList, _) = GetInputValuesList(inputName, stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var fList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var v = atrList[i];
            var f = fList[i];
            var prevCmvc1 = i >= 1 ? cmvCList[i - 1] : 0;
            var prevCmvc2 = i >= 2 ? cmvCList[i - 2] : 0;
            var currentClose = closeList[i];
            var currentOpen = openList[i];

            var cmvC = v != 0 ? MinOrMax((currentClose - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvCList.Add(cmvC);

            var cmvO = v != 0 ? MinOrMax((currentOpen - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvOList.Add(cmvO);

            var cmvH = v != 0 ? MinOrMax((currentHigh - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvHList.Add(cmvH);

            var cmvL = v != 0 ? MinOrMax((currentLow - f) / (v * Pow(length, 0.5)), 1, -1) : 0;
            cmvLList.Add(cmvL);

            var signal = GetRsiSignal(cmvC - prevCmvc1, prevCmvc1 - prevCmvc2, cmvC, prevCmvc1, 0.5, -0.5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmvc", cmvCList },
            { "Cmvo", cmvOList },
            { "Cmvh", cmvHList },
            { "Cmvl", cmvLList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ChartmillValueIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Conditional Accumulator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="increment"></param>
    /// <returns></returns>
    public static StockData CalculateConditionalAccumulator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double increment = 1)
    {
        List<double> valueList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;

            var prevValue = GetLastOrDefault(valueList);
            var value = currentLow >= prevHigh ? prevValue + increment : currentHigh <= prevLow ? prevValue - increment : prevValue;
            valueList.Add(value);
        }

        var valueEmaList = GetMovingAverageList(stockData, maType, length, valueList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = valueList[i];
            var valueEma = valueEmaList[i];
            var prevValue = i >= 1 ? valueList[i - 1] : 0;
            var prevValueEma = i >= 1 ? valueEmaList[i - 1] : 0;

            var signal = GetCompareSignal(value - valueEma, prevValue - prevValueEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ca", valueList },
            { "Signal", valueEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(valueList);
        stockData.IndicatorName = IndicatorName.ConditionalAccumulator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Contract High Low Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateContractHighLow(this StockData stockData)
    {
        List<double> conHiList = new(stockData.Count);
        List<double> conLowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var prevConHi = GetLastOrDefault(conHiList);
            var conHi = i >= 1 ? Math.Max(prevConHi, currentHigh) : currentHigh;
            conHiList.Add(conHi);

            var prevConLow = GetLastOrDefault(conLowList);
            var conLow = i >= 1 ? Math.Min(prevConLow, currentLow) : currentLow;
            conLowList.Add(conLow);

            var signal = GetConditionSignal(conHi > prevConHi, conLow < prevConLow);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ch", conHiList },
            { "Cl", conLowList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ContractHighLow;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chop Zone Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateChopZone(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        InputName inputName = InputName.TypicalPrice, int length1 = 30, int length2 = 34)
    {
        List<double> emaAngleList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, closeList, _) = GetInputValuesList(inputName, stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        var emaList = GetMovingAverageList(stockData, maType, length2, closeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var ema = emaList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var range = highest - lowest != 0 ? 25 / (highest - lowest) * lowest : 0;
            var avg = inputList[i];
            var y = avg != 0 && range != 0 ? (prevEma - ema) / avg * range : 0;
            var c = Sqrt(1 + (y * y));
            var emaAngle1 = c != 0 ? Math.Round(Math.Acos(1 / c).ToDegrees()) : 0;

            var prevEmaAngle = GetLastOrDefault(emaAngleList);
            var emaAngle = y > 0 ? -emaAngle1 : emaAngle1;
            emaAngleList.Add(emaAngle);

            var signal = GetCompareSignal(emaAngle, prevEmaAngle);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cz", emaAngleList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(emaAngleList);
        stockData.IndicatorName = IndicatorName.ChopZone;

        return stockData;
    }


    /// <summary>
    /// Calculates the Center of Linearity
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateCenterOfLinearity(this StockData stockData, int length = 14)
    {
        List<double> colList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var aSumWindow = new RollingSum();

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;

            var a = (i + 1) * (priorValue - prevValue);
            aSumWindow.Add(a);

            var prevCol = GetLastOrDefault(colList);
            var col = aSumWindow.Sum(length);
            colList.Add(col);

            var signal = GetCompareSignal(col, prevCol);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Col", colList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(colList);
        stockData.IndicatorName = IndicatorName.CenterOfLinearity;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chaikin Volatility
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateChaikinVolatility(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 10, int length2 = 12)
    {
        List<double> chaikinVolatilityList = new(stockData.Count);
        List<double> highLowList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];

            var highLow = currentHigh - currentLow;
            highLowList.Add(highLow);
        }

        var highLowEmaList = GetMovingAverageList(stockData, maType, length1, highLowList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var highLowEma = highLowEmaList[i];
            var prevHighLowEma = i >= length2 ? highLowEmaList[i - length2] : 0;

            var prevChaikinVolatility = GetLastOrDefault(chaikinVolatilityList);
            var chaikinVolatility = prevHighLowEma != 0 ? (highLowEma - prevHighLowEma) / prevHighLowEma * 100 : 0;
            chaikinVolatilityList.Add(chaikinVolatility);

            var signal = GetCompareSignal(chaikinVolatility, prevChaikinVolatility, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cv", chaikinVolatilityList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(chaikinVolatilityList);
        stockData.IndicatorName = IndicatorName.ChaikinVolatility;

        return stockData;
    }


    /// <summary>
    /// Calculates the Confluence Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateConfluenceIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        InputName inputName = InputName.FullTypicalPrice, int length = 10)
    {
        List<double> value5List = new(stockData.Count);
        List<double> value6List = new(stockData.Count);
        List<double> value7List = new(stockData.Count);
        List<double> momList = new(stockData.Count);
        List<double> sumList = new(stockData.Count);
        List<double> errSumList = new(stockData.Count);
        List<double> value70List = new(stockData.Count);
        List<double> confluenceList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, closeList, _) = GetInputValuesList(inputName, stockData);
        var errSumWindow = new RollingSum();
        var value70SumWindow = new RollingSum();

        var stl = (int)Math.Ceiling((length * 2) - 1 - 0.5m);
        var itl = (int)Math.Ceiling((stl * 2) - 1 - 0.5m);
        var ltl = (int)Math.Ceiling((itl * 2) - 1 - 0.5m);
        var hoff = (int)Math.Ceiling(((double)length / 2) - 0.5);
        var soff = (int)Math.Ceiling(((double)stl / 2) - 0.5);
        var ioff = (int)Math.Ceiling(((double)itl / 2) - 0.5);
        var hLength = MinOrMax(length - 1);
        var sLength = stl - 1;
        var iLength = itl - 1;
        var lLength = ltl - 1;

        var hAvgList = GetMovingAverageList(stockData, maType, length, closeList);
        var sAvgList = GetMovingAverageList(stockData, maType, stl, closeList);
        var iAvgList = GetMovingAverageList(stockData, maType, itl, closeList);
        var lAvgList = GetMovingAverageList(stockData, maType, ltl, closeList);
        var h2AvgList = GetMovingAverageList(stockData, maType, hLength, closeList);
        var s2AvgList = GetMovingAverageList(stockData, maType, sLength, closeList);
        var i2AvgList = GetMovingAverageList(stockData, maType, iLength, closeList);
        var l2AvgList = GetMovingAverageList(stockData, maType, lLength, closeList);
        var ftpAvgList = GetMovingAverageList(stockData, maType, lLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var sAvg = sAvgList[i];
            var priorSAvg = i >= soff ? sAvgList[i - soff] : 0;
            var priorHAvg = i >= hoff ? hAvgList[i - hoff] : 0;
            var iAvg = iAvgList[i];
            var priorIAvg = i >= ioff ? iAvgList[i - ioff] : 0;
            var lAvg = lAvgList[i];
            var hAvg = hAvgList[i];
            var prevSAvg = i >= 1 ? sAvgList[i - 1] : 0;
            var prevHAvg = i >= 1 ? hAvgList[i - 1] : 0;
            var prevIAvg = i >= 1 ? iAvgList[i - 1] : 0;
            var prevLAvg = i >= 1 ? lAvgList[i - 1] : 0;
            var h2 = h2AvgList[i];
            var s2 = s2AvgList[i];
            var i2 = i2AvgList[i];
            var l2 = l2AvgList[i];
            var ftpAvg = ftpAvgList[i];
            var priorValue5 = i >= hoff ? value5List[i - hoff] : 0;
            var priorValue6 = i >= soff ? value6List[i - soff] : 0;
            var priorValue7 = i >= ioff ? value7List[i - ioff] : 0;
            var priorSum = i >= soff ? sumList[i - soff] : 0;
            var priorHAvg2 = i >= soff ? hAvgList[i - soff] : 0;
            var prevErrSum = i >= 1 ? errSumList[i - 1] : 0;
            var prevMom = i >= 1 ? momList[i - 1] : 0;
            var prevValue70 = i >= 1 ? value70List[i - 1] : 0;
            var prevConfluence1 = i >= 1 ? confluenceList[i - 1] : 0;
            var prevConfluence2 = i >= 2 ? confluenceList[i - 2] : 0;
            var value2 = sAvg - priorHAvg;
            var value3 = iAvg - priorSAvg;
            var value12 = lAvg - priorIAvg;
            var momSig = value2 + value3 + value12;
            var derivH = (hAvg * 2) - prevHAvg;
            var derivS = (sAvg * 2) - prevSAvg;
            var derivI = (iAvg * 2) - prevIAvg;
            var derivL = (lAvg * 2) - prevLAvg;
            var sumDH = length * derivH;
            var sumDS = stl * derivS;
            var sumDI = itl * derivI;
            var sumDL = ltl * derivL;
            var n1h = h2 * hLength;
            var n1s = s2 * sLength;
            var n1i = i2 * iLength;
            var n1l = l2 * lLength;
            var drh = sumDH - n1h;
            var drs = sumDS - n1s;
            var dri = sumDI - n1i;
            var drl = sumDL - n1l;
            var hSum = h2 * (length - 1);
            var sSum = s2 * (stl - 1);
            var iSum = i2 * (itl - 1);
            var lSum = ftpAvg * (ltl - 1);

            var value5 = (hSum + drh) / length;
            value5List.Add(value5);

            var value6 = (sSum + drs) / stl;
            value6List.Add(value6);

            var value7 = (iSum + dri) / itl;
            value7List.Add(value7);

            var value13 = (lSum + drl) / ltl;
            var value9 = value6 - priorValue5;
            var value10 = value7 - priorValue6;
            var value14 = value13 - priorValue7;

            var mom = value9 + value10 + value14;
            momList.Add(mom);

            var ht = Math.Sin(value5 * 2 * Math.PI / 360) + Math.Cos(value5 * 2 * Math.PI / 360);
            var hta = Math.Sin(hAvg * 2 * Math.PI / 360) + Math.Cos(hAvg * 2 * Math.PI / 360);
            var st = Math.Sin(value6 * 2 * Math.PI / 360) + Math.Cos(value6 * 2 * Math.PI / 360);
            var sta = Math.Sin(sAvg * 2 * Math.PI / 360) + Math.Cos(sAvg * 2 * Math.PI / 360);
            var it = Math.Sin(value7 * 2 * Math.PI / 360) + Math.Cos(value7 * 2 * Math.PI / 360);
            var ita = Math.Sin(iAvg * 2 * Math.PI / 360) + Math.Cos(iAvg * 2 * Math.PI / 360);

            var sum = ht + st + it;
            sumList.Add(sum);

            var err = hta + sta + ita;
            double cond2 = (sum > priorSum && hAvg < priorHAvg2) || (sum < priorSum && hAvg > priorHAvg2) ? 1 : 0;
            double phase = cond2 == 1 ? -1 : 1;

            var errSum = (sum - err) * phase;
            errSumList.Add(errSum);
            errSumWindow.Add(errSum);

            var value70 = value5 - value13;
            value70List.Add(value70);
            value70SumWindow.Add(value70);

            var errSig = errSumWindow.Average(soff);
            var value71 = value70SumWindow.Average(length);
            double errNum = errSum > 0 && errSum < prevErrSum && errSum < errSig ? 1 : errSum > 0 && errSum < prevErrSum && errSum > errSig ? 2 :
                errSum > 0 && errSum > prevErrSum && errSum < errSig ? 2 : errSum > 0 && errSum > prevErrSum && errSum > errSig ? 3 :
                errSum < 0 && errSum > prevErrSum && errSum > errSig ? -1 : errSum < 0 && errSum < prevErrSum && errSum > errSig ? -2 :
                errSum < 0 && errSum > prevErrSum && errSum < errSig ? -2 : errSum < 0 && errSum < prevErrSum && errSum < errSig ? -3 : 0;
            double momNum = mom > 0 && mom < prevMom && mom < momSig ? 1 : mom > 0 && mom < prevMom && mom > momSig ? 2 :
                mom > 0 && mom > prevMom && mom < momSig ? 2 : mom > 0 && mom > prevMom && mom > momSig ? 3 :
                mom < 0 && mom > prevMom && mom > momSig ? -1 : mom < 0 && mom < prevMom && mom > momSig ? -2 :
                mom < 0 && mom > prevMom && mom < momSig ? -2 : mom < 0 && mom < prevMom && mom < momSig ? -3 : 0;
            double tcNum = value70 > 0 && value70 < prevValue70 && value70 < value71 ? 1 : value70 > 0 && value70 < prevValue70 && value70 > value71 ? 2 :
                value70 > 0 && value70 > prevValue70 && value70 < value71 ? 2 : value70 > 0 && value70 > prevValue70 && value70 > value71 ? 3 :
                value70 < 0 && value70 > prevValue70 && value70 > value71 ? -1 : value70 < 0 && value70 < prevValue70 && value70 > value71 ? -2 :
                value70 < 0 && value70 > prevValue70 && value70 < value71 ? -2 : value70 < 0 && value70 < prevValue70 && value70 < value71 ? -3 : 0;
            var value42 = errNum + momNum + tcNum;

            var confluence = value42 > 0 && value70 > 0 ? value42 : value42 < 0 && value70 < 0 ? value42 :
                (value42 > 0 && value70 < 0) || (value42 < 0 && value70 > 0) ? value42 / 10 : 0;
            confluenceList.Add(confluence);

            var res1 = confluence >= 1 ? confluence : 0;
            var res2 = confluence <= -1 ? confluence : 0;
            var res3 = confluence == 0 ? 0 : confluence > -1 && confluence < 1 ? 10 * confluence : 0;

            var signal = GetCompareSignal(confluence - prevConfluence1, prevConfluence1 - prevConfluence2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Ci", confluenceList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(confluenceList);
        stockData.IndicatorName = IndicatorName.ConfluenceIndicator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Coppock Curve
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateCoppockCurve(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 10,
        int fastLength = 11, int slowLength = 14)
    {
        List<double> rocTotalList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var roc11List = CalculateRateOfChange(stockData, fastLength).CustomValuesList;
        var roc14List = CalculateRateOfChange(stockData, slowLength).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRoc11 = roc11List[i];
            var currentRoc14 = roc14List[i];

            var rocTotal = currentRoc11 + currentRoc14;
            rocTotalList.Add(rocTotal);
        }

        var coppockCurveList = GetMovingAverageList(stockData, maType, length, rocTotalList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var coppockCurve = coppockCurveList[i];
            var prevCoppockCurve = i >= 1 ? coppockCurveList[i - 1] : 0;

            var signal = GetCompareSignal(coppockCurve, prevCoppockCurve);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cc", coppockCurveList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(coppockCurveList);
        stockData.IndicatorName = IndicatorName.CoppockCurve;

        return stockData;
    }


    /// <summary>
    /// Calculates the Constance Brown Composite Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateConstanceBrownCompositeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, 
        int fastLength = 13, int slowLength = 33, int length1 = 14, int length2 = 9, int smoothLength = 3)
    {
        List<double> sList = new(stockData.Count);
        List<double> bullSlopeList = new(stockData.Count);
        List<double> bearSlopeList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);

        var rsi1List = CalculateRelativeStrengthIndex(stockData, length: length1).CustomValuesList;
        var rsi2List = CalculateRelativeStrengthIndex(stockData, length: smoothLength).CustomValuesList;
        var rsiSmaList = GetMovingAverageList(stockData, maType, smoothLength, rsi2List);

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsiSma = rsiSmaList[i];
            var rsiDelta = i >= length2 ? rsi1List[i - length2] : 0;

            var s = rsiDelta + rsiSma;
            sList.Add(s);
        }

        var sFastSmaList = GetMovingAverageList(stockData, maType, fastLength, sList);
        var sSlowSmaList = GetMovingAverageList(stockData, maType, slowLength, sList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var s = sList[i];
            var sFastSma = sFastSmaList[i];
            var sSlowSma = sSlowSmaList[i];

            var prevBullSlope = GetLastOrDefault(bullSlopeList);
            var bullSlope = s - Math.Max(sFastSma, sSlowSma);
            bullSlopeList.Add(bullSlope);

            var prevBearSlope = GetLastOrDefault(bearSlopeList);
            var bearSlope = s - Math.Min(sFastSma, sSlowSma);
            bearSlopeList.Add(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cbci", sList },
            { "FastSignal", sFastSmaList },
            { "SlowSignal", sSlowSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sList);
        stockData.IndicatorName = IndicatorName.ConstanceBrownCompositeIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Commodity Selection Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="pointValue"></param>
    /// <param name="margin"></param>
    /// <param name="commission"></param>
    /// <returns></returns>
    public static StockData CalculateCommoditySelectionIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14, double pointValue = 50, double margin = 3000, double commission = 10)
    {
        List<double> csiList = new(stockData.Count);
        List<double> csiSmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var csiSumWindow = new RollingSum();

        var k = 100 * (pointValue / Sqrt(margin) / (150 + commission));

        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;
        var adxList = CalculateAverageDirectionalIndex(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var atr = atrList[i];
            var adxRating = adxList[i];

            var prevCsi = GetLastOrDefault(csiList);
            var csi = k * atr * adxRating;
            csiList.Add(csi);

            var prevCsiSma = GetLastOrDefault(csiSmaList);
            csiSumWindow.Add(csi);
            var csiSma = csiSumWindow.Average(length);
            csiSmaList.Add(csiSma);

            var signal = GetCompareSignal(csi - csiSma, prevCsi - prevCsiSma, true);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Csi", csiList },
            { "Signal", csiSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(csiList);
        stockData.IndicatorName = IndicatorName.CommoditySelectionIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Decision Point Breadth Swenlin Trading Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDecisionPointBreadthSwenlinTradingOscillator(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 5)
    {
        List<double> iList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            double advance = currentValue > prevValue ? 1 : 0;
            double decline = currentValue < prevValue ? 1 : 0;

            var iVal = advance + decline != 0 ? 1000 * (advance - decline) / (advance + decline) : 0;
            iList.Add(iVal);
        }

        var ivalEmaList = GetMovingAverageList(stockData, maType, length, iList);
        var stoList = GetMovingAverageList(stockData, maType, length, ivalEmaList);
        var stoEmaList = GetMovingAverageList(stockData, maType, length, stoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sto = stoList[i];
            var stoEma = stoEmaList[i];
            var prevSto = i >= 1 ? stoList[i - 1] : 0;
            var prevStoEma = i >= 1 ? stoEmaList[i - 1] : 0;

            var signal = GetCompareSignal(sto - stoEma, prevSto - prevStoEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dpbsto", stoList },
            { "Signal", stoEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(stoList);
        stockData.IndicatorName = IndicatorName.DecisionPointBreadthSwenlinTradingOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Delta Moving Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateDeltaMovingAverage(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length1 = 10, int length2 = 5)
    {
        List<double> deltaList = new(stockData.Count);
        List<double> deltaHistogramList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var prevOpen = i >= length2 ? openList[i - length2] : 0;

            var delta = currentClose - prevOpen;
            deltaList.Add(delta);
        }

        var deltaSmaList = GetMovingAverageList(stockData, maType, length1, deltaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var delta = deltaList[i];
            var deltaSma = deltaSmaList[i];

            var prevDeltaHistogram = GetLastOrDefault(deltaHistogramList);
            var deltaHistogram = delta - deltaSma;
            deltaHistogramList.Add(deltaHistogram);

            var signal = GetCompareSignal(deltaHistogram, prevDeltaHistogram);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Delta", deltaList },
            { "Signal", deltaSmaList },
            { "Histogram", deltaHistogramList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(deltaList);
        stockData.IndicatorName = IndicatorName.DeltaMovingAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Detrended Synthetic Price
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDetrendedSyntheticPrice(this StockData stockData, int length = 14)
    {
        List<double> dspList = new(stockData.Count);
        List<double> ema1List = new(stockData.Count);
        List<double> ema2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        var alpha = length > 2 ? (double)2 / (length + 1) : 0.67;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var high = Math.Max(currentHigh, prevHigh);
            var low = Math.Min(currentLow, prevLow);
            var price = (high + low) / 2;
            var prevEma1 = i >= 1 ? ema1List[i - 1] : price;
            var prevEma2 = i >= 1 ? ema2List[i - 1] : price;

            var ema1 = (alpha * price) + ((1 - alpha) * prevEma1);
            ema1List.Add(ema1);

            var ema2 = (alpha / 2 * price) + ((1 - (alpha / 2)) * prevEma2);
            ema2List.Add(ema2);

            var prevDsp = GetLastOrDefault(dspList);
            var dsp = ema1 - ema2;
            dspList.Add(dsp);

            var signal = GetCompareSignal(dsp, prevDsp);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsp", dspList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dspList);
        stockData.IndicatorName = IndicatorName.DetrendedSyntheticPrice;

        return stockData;
    }


    /// <summary>
    /// Calculates the Derivative Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDerivativeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 9, int length3 = 5, int length4 = 3)
    {
        List<double> s1List = new(stockData.Count);
        List<double> s2List = new(stockData.Count);
        List<double> s1SmaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var s1SumWindow = new RollingSum();

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length: length1).CustomValuesList;
        var rsiEma1List = GetMovingAverageList(stockData, maType, length3, rsiList);
        var rsiEma2List = GetMovingAverageList(stockData, maType, length4, rsiEma1List);

        for (var i = 0; i < rsiList.Count; i++)
        {
            var prevS1 = GetLastOrDefault(s1List);
            var s1 = rsiEma2List[i];
            s1List.Add(s1);
            s1SumWindow.Add(s1);

            var prevS1Sma = GetLastOrDefault(s1SmaList);
            var s1Sma = s1SumWindow.Average(length2);
            s1SmaList.Add(s1Sma);

            var s2 = s1 - s1Sma;
            s2List.Add(s2);

            var signal = GetCompareSignal(s1 - s1Sma, prevS1 - prevS1Sma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Do", s2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(s2List);
        stockData.IndicatorName = IndicatorName.DerivativeOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Demand Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDemandOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 10, int length2 = 2, int length3 = 20)
    {
        List<double> rangeList = new(stockData.Count);
        List<double> doList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];

            var range = highest - lowest;
            rangeList.Add(range);
        }

        var vaList = GetMovingAverageList(stockData, maType, length1, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var va = vaList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var pctChg = prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / Math.Abs(prevValue) * 100 : 0;
            var currentVolume = stockData.Volumes[i];
            var k = va != 0 ? (3 * currentValue) / va : 0;
            var pctK = pctChg * k;
            var volPctK = pctK != 0 ? currentVolume / pctK : 0;
            var bp = currentValue > prevValue ? currentVolume : volPctK;
            var sp = currentValue > prevValue ? volPctK : currentVolume;

            var dosc = bp - sp;
            doList.Add(dosc);
        }

        var doEmaList = GetMovingAverageList(stockData, maType, length3, doList);
        var doSigList = GetMovingAverageList(stockData, maType, length1, doEmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var doSig = doSigList[i];
            var prevSig1 = i >= 1 ? doSigList[i - 1] : 0;
            var prevSig2 = i >= 2 ? doSigList[i - 1] : 0;

            var signal = GetCompareSignal(doSig - prevSig1, prevSig1 - prevSig2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Do", doEmaList },
            { "Signal", doSigList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(doEmaList);
        stockData.IndicatorName = IndicatorName.DemandOscillator;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Smoothed Momenta
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleSmoothedMomenta(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2,
        int length2 = 5, int length3 = 25)
    {
        List<double> momList = new(stockData.Count);
        List<double> srcLcList = new(stockData.Count);
        List<double> hcLcList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(inputList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var hc = highestList[i];
            var lc = lowestList[i];

            var srcLc = currentValue - lc;
            srcLcList.Add(srcLc);

            var hcLc = hc - lc;
            hcLcList.Add(hcLc);
        }

        var topEma1List = GetMovingAverageList(stockData, maType, length2, srcLcList);
        var topEma2List = GetMovingAverageList(stockData, maType, length3, topEma1List);
        var botEma1List = GetMovingAverageList(stockData, maType, length2, hcLcList);
        var botEma2List = GetMovingAverageList(stockData, maType, length3, botEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var top = topEma2List[i];
            var bot = botEma2List[i];

            var mom = bot != 0 ? MinOrMax(100 * top / bot, 100, 0) : 0;
            momList.Add(mom);
        }

        var momEmaList = GetMovingAverageList(stockData, maType, length3, momList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mom = momList[i];
            var momEma = momEmaList[i];
            var prevMom = i >= 1 ? momList[i - 1] : 0;
            var prevMomEma = i >= 1 ? momEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mom - momEma, prevMom - prevMomEma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsm", momList },
            { "Signal", momEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(momList);
        stockData.IndicatorName = IndicatorName.DoubleSmoothedMomenta;

        return stockData;
    }


    /// <summary>
    /// Calculates the Didi Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDidiIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length1 = 3, int length2 = 8,
        int length3 = 20)
    {
        List<double> curtaList = new(stockData.Count);
        List<double> longaList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var mediumSmaList = GetMovingAverageList(stockData, maType, length2, inputList);
        var shortSmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var longSmaList = GetMovingAverageList(stockData, maType, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var mediumSma = mediumSmaList[i];
            var shortSma = shortSmaList[i];
            var longSma = longSmaList[i];

            var prevCurta = GetLastOrDefault(curtaList);
            var curta = mediumSma != 0 ? shortSma / mediumSma : 0;
            curtaList.Add(curta);

            var prevLonga = GetLastOrDefault(longaList);
            var longa = mediumSma != 0 ? longSma / mediumSma : 0;
            longaList.Add(longa);

            var signal = GetCompareSignal(curta - longa, prevCurta - prevLonga);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Curta", curtaList },
            { "Media", mediumSmaList },
            { "Longa", longSmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DidiIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Disparity Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDisparityIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> disparityIndexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentSma = smaList[i];

            var prevDisparityIndex = GetLastOrDefault(disparityIndexList);
            var disparityIndex = currentSma != 0 ? (currentValue - currentSma) / currentSma * 100 : 0;
            disparityIndexList.Add(disparityIndex);

            var signal = GetCompareSignal(disparityIndex, prevDisparityIndex);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Di", disparityIndexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(disparityIndexList);
        stockData.IndicatorName = IndicatorName.DisparityIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Damping Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="threshold"></param>
    /// <returns></returns>
    public static StockData CalculateDampingIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 5, 
        double threshold = 1.5)
    {
        List<double> rangeList = new(stockData.Count);
        List<double> diList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);

        var smaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            
            var range = currentHigh - currentLow;
            rangeList.Add(range);
        }

        var rangeSmaList = GetMovingAverageList(stockData, maType, length, rangeList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevSma1 = i >= 1 ? rangeSmaList[i - 1] : 0;
            var prevSma6 = i >= 6 ? rangeSmaList[i - 6] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevSma = i >= 1 ? smaList[i - 1] : 0;
            var currentSma = smaList[i];

            var di = prevSma6 != 0 ? prevSma1 / prevSma6 : 0;
            diList.Add(di);

            var signal = GetVolatilitySignal(currentValue - currentSma, prevValue - prevSma, di, threshold);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Di", diList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(diList);
        stockData.IndicatorName = IndicatorName.DampingIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Directional Trend Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDirectionalTrendIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 14,
        int length2 = 10, int length3 = 5)
    {
        List<double> dtiList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (_, highList, lowList, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var hmu = currentHigh - prevHigh > 0 ? currentHigh - prevHigh : 0;
            var lmd = currentLow - prevLow < 0 ? (currentLow - prevLow) * -1 : 0;

            var diff = hmu - lmd;
            diffList.Add(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.Add(absDiff);
        }
        
        var diffEma1List = GetMovingAverageList(stockData, maType, length1, diffList);
        var absDiffEma1List = GetMovingAverageList(stockData, maType, length1, absDiffList);
        var diffEma2List = GetMovingAverageList(stockData, maType, length2, diffEma1List);
        var absDiffEma2List = GetMovingAverageList(stockData, maType, length2, absDiffEma1List);
        var diffEma3List = GetMovingAverageList(stockData, maType, length3, diffEma2List);
        var absDiffEma3List = GetMovingAverageList(stockData, maType, length3, absDiffEma2List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var diffEma3 = diffEma3List[i];
            var absDiffEma3 = absDiffEma3List[i];
            var prevDti1 = i >= 1 ? dtiList[i - 1] : 0;
            var prevDti2 = i >= 2 ? dtiList[i - 2] : 0;

            var dti = absDiffEma3 != 0 ? MinOrMax(100 * diffEma3 / absDiffEma3, 100, -100) : 0;
            dtiList.Add(dti);

            var signal = GetRsiSignal(dti - prevDti1, prevDti1 - prevDti2, dti, prevDti1, 25, -25);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dti", dtiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(dtiList);
        stockData.IndicatorName = IndicatorName.DirectionalTrendIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Drunkard Walk
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateDrunkardWalk(this StockData stockData, int length1 = 80, int length2 = 14)
    {
        List<double> tempHighList = new(stockData.Count);
        List<double> tempLowList = new(stockData.Count);
        List<double> upAtrList = new(stockData.Count);
        List<double> dnAtrList = new(stockData.Count);
        List<double> upwalkList = new(stockData.Count);
        List<double> dnwalkList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length1);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentHigh = highList[i];
            tempHighList.Add(currentHigh);

            var currentLow = lowList[i];
            tempLowList.Add(currentLow);

            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            var maxIndex = tempHighList.LastIndexOf(highestHigh);
            var minIndex = tempLowList.LastIndexOf(lowestLow);
            var dnRun = i - maxIndex;
            var upRun = i - minIndex;

            var prevAtrUp = GetLastOrDefault(upAtrList);
            var upK = upRun != 0 ? (double)1 / upRun : 0;
            var atrUp = (tr * upK) + (prevAtrUp * (1 - upK));
            upAtrList.Add(atrUp);

            var prevAtrDn = GetLastOrDefault(dnAtrList);
            var dnK = dnRun != 0 ? (double)1 / dnRun : 0;
            var atrDn = (tr * dnK) + (prevAtrDn * (1 - dnK));
            dnAtrList.Add(atrDn);

            var upDen = atrUp > 0 ? atrUp : 1;
            var prevUpWalk = GetLastOrDefault(upwalkList);
            var upWalk = upRun > 0 ? (currentHigh - lowestLow) / (Sqrt(upRun) * upDen) : 0;
            upwalkList.Add(upWalk);

            var dnDen = atrDn > 0 ? atrDn : 1;
            var prevDnWalk = GetLastOrDefault(dnwalkList);
            var dnWalk = dnRun > 0 ? (highestHigh - currentLow) / (Sqrt(dnRun) * dnDen) : 0;
            dnwalkList.Add(dnWalk);

            var signal = GetCompareSignal(upWalk - dnWalk, prevUpWalk - prevDnWalk);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "UpWalk", upwalkList },
            { "DnWalk", dnwalkList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.DrunkardWalk;

        return stockData;
    }


    /// <summary>
    /// Calculates the DT Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    public static StockData CalculateDTOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length1 = 13, 
        int length2 = 8, int length3 = 5, int length4 = 3)
    {
        List<double> stoRsiList = new(stockData.Count);
        List<double> skList = new(stockData.Count);
        List<double> sdList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);
        var stoRsiSumWindow = new RollingSum();
        var skSumWindow = new RollingSum();

        var wilderMovingAvgList = GetMovingAverageList(stockData, maType, length1, inputList);
        var (highestList, lowestList) = GetMaxAndMinValuesList(wilderMovingAvgList, length2);

        for (var i = 0; i < stockData.Count; i++)
        {
            var wima = wilderMovingAvgList[i];
            var highest = highestList[i];
            var lowest = lowestList[i];
            var prevSd1 = i >= 1 ? sdList[i - 1] : 0;
            var prevSd2 = i >= 2 ? sdList[i - 2] : 0;

            var stoRsi = highest - lowest != 0 ? MinOrMax(100 * (wima - lowest) / (highest - lowest), 100, 0) : 0;
            stoRsiList.Add(stoRsi);

            stoRsiSumWindow.Add(stoRsi);
            var sk = stoRsiSumWindow.Average(length3);
            skList.Add(sk);

            skSumWindow.Add(sk);
            var sd = skSumWindow.Average(length4);
            sdList.Add(sd);

            var signal = GetRsiSignal(sd - prevSd1, prevSd1 - prevSd2, sd, prevSd1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dto", skList },
            { "Signal", sdList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(skList);
        stockData.IndicatorName = IndicatorName.DTOscillator;

        return stockData;
    }

}

