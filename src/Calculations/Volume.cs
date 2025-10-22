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
    /// Calculates the index of the force.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateForceIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 14)
    {
        List<double> rawForceList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var rawForce = MinPastValues(i, 1, currentValue - prevValue) * currentVolume;
            rawForceList.AddRounded(rawForce);
        }

        var forceList = GetMovingAverageList(stockData, maType, length, rawForceList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var force = forceList[i];
            var prevForce1 = i >= 1 ? forceList[i - 1] : 0;
            var prevForce2 = i >= 2 ? forceList[i - 2] : 0;

            var signal = GetCompareSignal(force - prevForce1, prevForce1 - prevForce2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fi", forceList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = forceList;
        stockData.IndicatorName = IndicatorName.ForceIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Money Flow Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="inputName">Name of the input.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateMoneyFlowIndex(this StockData stockData, InputName inputName = InputName.TypicalPrice, int length = 14)
    {
        List<double> mfiList = new();
        List<double> posMoneyFlowList = new();
        List<double> negMoneyFlowList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var typicalPrice = inputList[i];
            var prevTypicalPrice = i >= 1 ? inputList[i - 1] : 0;
            var prevMfi1 = i >= 1 ? mfiList[i - 1] : 0;
            var prevMfi2 = i >= 2 ? mfiList[i - 2] : 0;
            var rawMoneyFlow = typicalPrice * currentVolume;

            var posMoneyFlow = i >= 1 && typicalPrice > prevTypicalPrice ? rawMoneyFlow : 0;
            posMoneyFlowList.AddRounded(posMoneyFlow);

            var negMoneyFlow = i >= 1 && typicalPrice < prevTypicalPrice ? rawMoneyFlow : 0;
            negMoneyFlowList.AddRounded(negMoneyFlow);

            var posMoneyFlowTotal = posMoneyFlowList.TakeLastExt(length).Sum();
            var negMoneyFlowTotal = negMoneyFlowList.TakeLastExt(length).Sum();
            var mfiRatio = negMoneyFlowTotal != 0 ? posMoneyFlowTotal / negMoneyFlowTotal : 0;

            var mfi = negMoneyFlowTotal == 0 ? 100 : posMoneyFlowTotal == 0 ? 0 : MinOrMax(100 - (100 / (1 + mfiRatio)), 100, 0);
            mfiList.AddRounded(mfi);

            var signal = GetRsiSignal(mfi - prevMfi1, prevMfi1 - prevMfi2, mfi, prevMfi1, 80, 20);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mfi", mfiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mfiList;
        stockData.IndicatorName = IndicatorName.MoneyFlowIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Klinger Volume Oscillator
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="fastLength">Length of the fast.</param>
    /// <param name="slowLength">Length of the slow.</param>
    /// <param name="signalLength">Length of the signal.</param>
    /// <returns></returns>
    public static StockData CalculateKlingerVolumeOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int fastLength = 34, int slowLength = 55, int signalLength = 13)
    {
        List<double> kvoList = new();
        List<double> trendList = new();
        List<double> dmList = new();
        List<double> cmList = new();
        List<double> vfList = new();
        List<double> kvoHistoList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var mom = MinPastValues(i, 1, currentValue - prevValue);

            var prevTrend = trendList.LastOrDefault();
            var trend = mom > 0 ? 1 : mom < 0 ? -1 : prevTrend;
            trendList.AddRounded(trend);

            var prevDm = dmList.LastOrDefault();
            var dm = currentHigh - currentLow;
            dmList.AddRounded(dm);

            var prevCm = cmList.LastOrDefault();
            var cm = trend == prevTrend ? prevCm + dm : prevDm + dm;
            cmList.AddRounded(cm);

            var temp = cm != 0 ? Math.Abs((2 * (dm / cm)) - 1) : -1;
            var vf = currentVolume * temp * trend * 100;
            vfList.AddRounded(vf);
        }

        var ema34List = GetMovingAverageList(stockData, maType, fastLength, vfList);
        var ema55List = GetMovingAverageList(stockData, maType, slowLength, vfList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ema34 = ema34List[i];
            var ema55 = ema55List[i];

            var klingerOscillator = ema34 - ema55;
            kvoList.AddRounded(klingerOscillator);
        }

        var kvoSignalList = GetMovingAverageList(stockData, maType, signalLength, kvoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var klingerOscillator = kvoList[i];
            var koSignalLine = kvoSignalList[i];

            var prevKlingerOscillatorHistogram = kvoHistoList.LastOrDefault();
            var klingerOscillatorHistogram = klingerOscillator - koSignalLine;
            kvoHistoList.AddRounded(klingerOscillatorHistogram);

            var signal = GetCompareSignal(klingerOscillatorHistogram, prevKlingerOscillatorHistogram);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Kvo", kvoList },
            { "KvoSignal", kvoSignalList },
            { "KvoHistogram", kvoHistoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = kvoList;
        stockData.IndicatorName = IndicatorName.KlingerVolumeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the on balance volume.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolume(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20)
    {
        List<double> obvList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevObv = obvList.LastOrDefault();
            var obv = currentValue > prevValue ? prevObv + currentVolume : currentValue < prevValue ? prevObv - currentVolume : prevObv;
            obvList.AddRounded(obv);
        }

        var obvSignalList = GetMovingAverageList(stockData, maType, length, obvList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var obv = obvList[i];
            var prevObv = i >= 1 ? obvList[i - 1] : 0;
            var obvSig = obvSignalList[i];
            var prevObvSig = i >= 1 ? obvSignalList[i - 1] : 0;

            var signal = GetCompareSignal(obv - obvSig, prevObv - prevObvSig);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Obv", obvList },
            { "ObvSignal", obvSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = obvList;
        stockData.IndicatorName = IndicatorName.OnBalanceVolume;

        return stockData;
    }

    /// <summary>
    /// Calculates the index of the negative volume.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="initialValue">The initial Nvi value</param>
    /// <returns></returns>
    public static StockData CalculateNegativeVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 255, int initialValue = 1000)
    {
        List<double> nviList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentVolume = volumeList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;
            var prevNvi = i >= 1 ? nviList[i - 1] : initialValue;
            var pctChg = CalculatePercentChange(currentClose, prevClose);

            var nvi = currentVolume >= prevVolume ? prevNvi : prevNvi + pctChg;
            nviList.AddRounded(nvi);
        }

        var nviSignalList = GetMovingAverageList(stockData, maType, length, nviList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nvi = nviList[i];
            var prevNvi = i >= 1 ? nviList[i - 1] : 0;
            var nviSignal = nviSignalList[i];
            var prevNviSignal = i >= 1 ? nviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(nvi - nviSignal, prevNvi - prevNviSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nvi", nviList },
            { "NviSignal", nviSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nviList;
        stockData.IndicatorName = IndicatorName.NegativeVolumeIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the index of the positive volume.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="initialValue">The initial Pvi value</param>
    /// <returns></returns>
    public static StockData CalculatePositiveVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 255, int initialValue = 1000)
    {
        List<double> pviList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = inputList[i];
            var currentVolume = volumeList[i];
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;
            var prevPvi = i >= 1 ? pviList[i - 1] : initialValue;
            var pctChg = CalculatePercentChange(currentClose, prevClose);

            var pvi = currentVolume <= prevVolume ? prevPvi : prevPvi + pctChg;
            pviList.AddRounded(pvi);
        }

        var pviSignalList = GetMovingAverageList(stockData, maType, length, pviList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var pvi = pviList[i];
            var prevPvi = i >= 1 ? pviList[i - 1] : 0;
            var pviSignal = pviSignalList[i];
            var prevPviSignal = i >= 1 ? pviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(pvi - pviSignal, prevPvi - prevPviSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pvi", pviList },
            { "PviSignal", pviSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pviList;
        stockData.IndicatorName = IndicatorName.PositiveVolumeIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Chaikin Money Flow
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateChaikinMoneyFlow(this StockData stockData, int length = 20)
    {
        List<double> chaikinMoneyFlowList = new();
        List<double> tempVolumeList = new();
        List<double> moneyFlowVolumeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentLow = lowList[i];
            var currentHigh = highList[i];
            var currentClose = inputList[i];
            var moneyFlowMultiplier = currentHigh - currentLow != 0 ?
                (currentClose - currentLow - (currentHigh - currentClose)) / (currentHigh - currentLow) : 0;
            var prevCmf1 = i >= 1 ? chaikinMoneyFlowList[i - 1] : 0;
            var prevCmf2 = i >= 2 ? chaikinMoneyFlowList[i - 2] : 0;

            var currentVolume = volumeList[i];
            tempVolumeList.AddRounded(currentVolume);

            var moneyFlowVolume = moneyFlowMultiplier * currentVolume;
            moneyFlowVolumeList.AddRounded(moneyFlowVolume);

            var volumeSum = tempVolumeList.TakeLastExt(length).Sum();
            var mfVolumeSum = moneyFlowVolumeList.TakeLastExt(length).Sum();

            var cmf = volumeSum != 0 ? mfVolumeSum / volumeSum : 0;
            chaikinMoneyFlowList.AddRounded(cmf);

            var signal = GetCompareSignal(cmf - prevCmf1, prevCmf1 - prevCmf2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Cmf", chaikinMoneyFlowList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = chaikinMoneyFlowList;
        stockData.IndicatorName = IndicatorName.ChaikinMoneyFlow;

        return stockData;
    }

    /// <summary>
    /// Calculates the accumulation distribution line.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAccumulationDistributionLine(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length = 14)
    {
        List<double> adlList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentLow = lowList[i];
            var currentHigh = highList[i];
            var currentClose = inputList[i];
            var currentVolume = volumeList[i];
            var moneyFlowMultiplier = currentHigh - currentLow != 0 ?
                (currentClose - currentLow - (currentHigh - currentClose)) / (currentHigh - currentLow) : 0;
            var moneyFlowVolume = moneyFlowMultiplier * currentVolume;

            var prevAdl = adlList.LastOrDefault();
            var adl = prevAdl + moneyFlowVolume;
            adlList.AddRounded(adl);
        }

        var adlSignalList = GetMovingAverageList(stockData, maType, length, adlList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var adl = adlList[i];
            var prevAdl = i >= 1 ? adlList[i - 1] : 0;
            var adlSignal = adlSignalList[i];
            var prevAdlSignal = i >= 1 ? adlSignalList[i - 1] : 0;

            var signal = GetCompareSignal(adl - adlSignal, prevAdl - prevAdlSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Adl", adlList },
            { "AdlSignal", adlSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = adlList;
        stockData.IndicatorName = IndicatorName.AccumulationDistributionLine;

        return stockData;
    }

    /// <summary>
    /// Calculates the average money flow oscillator.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateAverageMoneyFlowOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage, 
        int length = 5, int smoothLength = 3)
    {
        List<double> chgList = new();
        List<double> rList = new();
        List<double> kList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var avgvList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var chg = MinPastValues(i, 1, currentValue - prevValue);
            chgList.AddRounded(chg);
        }

        var avgcList = GetMovingAverageList(stockData, maType, length, chgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avgv = avgvList[i];
            var avgc = avgcList[i];

            var r = Math.Abs(avgv * avgc) > 0 ? Math.Log(Math.Abs(avgv * avgc)) * Math.Sign(avgc) : 0;
            rList.AddRounded(r);

            var list = rList.TakeLastExt(length).ToList();
            var rh = list.Max();
            var rl = list.Min();
            var rs = rh != rl ? (r - rl) / (rh - rl) * 100 : 0;

            var k = (rs * 2) - 100;
            kList.AddRounded(k);
        }

        var ksList = GetMovingAverageList(stockData, maType, smoothLength, kList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ks = ksList[i];
            var prevKs = i >= 1 ? ksList[i - 1] : 0;

            var signal = GetCompareSignal(ks, prevKs);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Amfo", ksList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ksList;
        stockData.IndicatorName = IndicatorName.AverageMoneyFlowOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Better Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <returns></returns>
    public static StockData CalculateBetterVolumeIndicator(this StockData stockData, int length = 8, int lbLength = 2)
    {
        List<double> v1List = new();
        List<double> v2List = new();
        List<double> v3List = new();
        List<double> v4List = new();
        List<double> v5List = new();
        List<double> v6List = new();
        List<double> v7List = new();
        List<double> v8List = new();
        List<double> v9List = new();
        List<double> v10List = new();
        List<double> v11List = new();
        List<double> v12List = new();
        List<double> v13List = new();
        List<double> v14List = new();
        List<double> v15List = new();
        List<double> v16List = new();
        List<double> v17List = new();
        List<double> v18List = new();
        List<double> v19List = new();
        List<double> v20List = new();
        List<double> v21List = new();
        List<double> v22List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, openList, volumeList) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, lbLength);

        for (var i = 0; i < stockData.Count; i++)
        {
            var highest = highestList[i];
            var lowest = lowestList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var currentOpen = openList[i];
            var currentClose = inputList[i];
            var highLowRange = highest - lowest;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;
            var range = CalculateTrueRange(currentHigh, currentLow, prevClose);

            var prevV1 = v1List.LastOrDefault();
            var v1 = currentClose > currentOpen ? range / ((2 * range) + currentOpen - currentClose) * currentVolume :
                currentClose < currentOpen ? (range + currentClose - currentOpen) / ((2 * range) + currentClose - currentOpen) * currentVolume :
                0.5 * currentVolume;
            v1List.AddRounded(v1);

            var prevV2 = v2List.LastOrDefault();
            var v2 = currentVolume - v1;
            v2List.AddRounded(v2);

            var prevV3 = v3List.LastOrDefault();
            var v3 = v1 + v2;
            v3List.AddRounded(v3);

            var v4 = v1 * range;
            v4List.AddRounded(v4);

            var v5 = (v1 - v2) * range;
            v5List.AddRounded(v5);

            var v6 = v2 * range;
            v6List.AddRounded(v6);

            var v7 = (v2 - v1) * range;
            v7List.AddRounded(v7);

            var v8 = range != 0 ? v1 / range : 0;
            v8List.AddRounded(v8);

            var v9 = range != 0 ? (v1 - v2) / range : 0;
            v9List.AddRounded(v9);

            var v10 = range != 0 ? v2 / range : 0;
            v10List.AddRounded(v10);

            var v11 = range != 0 ? (v2 - v1) / range : 0;
            v11List.AddRounded(v11);

            var v12 = range != 0 ? v3 / range : 0;
            v12List.AddRounded(v12);

            var v13 = v3 + prevV3;
            v13List.AddRounded(v13);

            var v14 = (v1 + prevV1) * highLowRange;
            v14List.AddRounded(v14);

            var v15 = (v1 + prevV1 - v2 - prevV2) * highLowRange;
            v15List.AddRounded(v15);

            var v16 = (v2 + prevV2) * highLowRange;
            v16List.AddRounded(v16);

            var v17 = (v2 + prevV2 - v1 - prevV1) * highLowRange;
            v17List.AddRounded(v17);

            var v18 = highLowRange != 0 ? (v1 + prevV1) / highLowRange : 0;
            v18List.AddRounded(v18);

            var v19 = highLowRange != 0 ? (v1 + prevV1 - v2 - prevV2) / highLowRange : 0;
            v19List.AddRounded(v19);

            var v20 = highLowRange != 0 ? (v2 + prevV2) / highLowRange : 0;
            v20List.AddRounded(v20);

            var v21 = highLowRange != 0 ? (v2 + prevV2 - v1 - prevV1) / highLowRange : 0;
            v21List.AddRounded(v21);

            var v22 = highLowRange != 0 ? v13 / highLowRange : 0;
            v22List.AddRounded(v22);

            var c1 = v3 == v3List.TakeLastExt(length).Min();
            var c2 = v4 == v4List.TakeLastExt(length).Max() && currentClose > currentOpen;
            var c3 = v5 == v5List.TakeLastExt(length).Max() && currentClose > currentOpen;
            var c4 = v6 == v6List.TakeLastExt(length).Max() && currentClose < currentOpen;
            var c5 = v7 == v7List.TakeLastExt(length).Max() && currentClose < currentOpen;
            var c6 = v8 == v8List.TakeLastExt(length).Min() && currentClose < currentOpen;
            var c7 = v9 == v9List.TakeLastExt(length).Min() && currentClose < currentOpen;
            var c8 = v10 == v10List.TakeLastExt(length).Min() && currentClose > currentOpen;
            var c9 = v11 == v11List.TakeLastExt(length).Min() && currentClose > currentOpen;
            var c10 = v12 == v12List.TakeLastExt(length).Max();
            var c11 = v13 == v13List.TakeLastExt(length).Min() && currentClose > currentOpen && prevClose > prevOpen;
            var c12 = v14 == v14List.TakeLastExt(length).Max() && currentClose > currentOpen && prevClose > prevOpen;
            var c13 = v15 == v15List.TakeLastExt(length).Max() && currentClose > currentOpen && prevClose < prevOpen;
            var c14 = v16 == v16List.TakeLastExt(length).Min() && currentClose < currentOpen && prevClose < prevOpen;
            var c15 = v17 == v17List.TakeLastExt(length).Min() && currentClose < currentOpen && prevClose < prevOpen;
            var c16 = v18 == v18List.TakeLastExt(length).Min() && currentClose < currentOpen && prevClose < prevOpen;
            var c17 = v19 == v19List.TakeLastExt(length).Min() && currentClose > currentOpen && prevClose < prevOpen;
            var c18 = v20 == v20List.TakeLastExt(length).Min() && currentClose > currentOpen && prevClose > prevOpen;
            var c19 = v21 == v21List.TakeLastExt(length).Min() && currentClose > currentOpen && prevClose > prevOpen;
            var c20 = v22 == v22List.TakeLastExt(length).Min();
            var climaxUp = c2 || c3 || c8 || c9 || c12 || c13 || c18 || c19;
            var climaxDown = c4 || c5 || c6 || c7 || c14 || c15 || c16 || c17;
            var churn = c10 || c20;
            var lowVolue = c1 || c11;

            var signal = GetConditionSignal(climaxUp, climaxDown);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Bvi", v1List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = v1List;
        stockData.IndicatorName = IndicatorName.BetterVolumeIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Buff Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculateBuffAverage(this StockData stockData, int fastLength = 5, int slowLength = 20)
    {
        List<double> priceVolList = new();
        List<double> fastBuffList = new();
        List<double> slowBuffList = new();
        List<double> tempVolumeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var currentVolume = volumeList[i];
            tempVolumeList.AddRounded(currentVolume);

            var priceVol = currentValue * currentVolume;
            priceVolList.AddRounded(priceVol);

            var fastBuffNum = priceVolList.TakeLastExt(fastLength).Sum();
            var fastBuffDenom = tempVolumeList.TakeLastExt(fastLength).Sum();

            var prevFastBuff = fastBuffList.LastOrDefault();
            var fastBuff = fastBuffDenom != 0 ? fastBuffNum / fastBuffDenom : 0;
            fastBuffList.AddRounded(fastBuff);

            var slowBuffNum = priceVolList.TakeLastExt(slowLength).Sum();
            var slowBuffDenom = tempVolumeList.TakeLastExt(slowLength).Sum();

            var prevSlowBuff = slowBuffList.LastOrDefault();
            var slowBuff = slowBuffDenom != 0 ? slowBuffNum / slowBuffDenom : 0;
            slowBuffList.AddRounded(slowBuff);

            var signal = GetCompareSignal(fastBuff - slowBuff, prevFastBuff - prevSlowBuff);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "FastBuff", fastBuffList },
            { "SlowBuff", slowBuffList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.BuffAverage;

        return stockData;
    }

    /// <summary>
    /// Calculates the Upside Downside Volume
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateUpsideDownsideVolume(this StockData stockData, int length = 50)
    {
        List<double> upVolList = new();
        List<double> downVolList = new();
        List<double> upDownVolumeList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var upVol = currentValue > prevValue ? currentVolume : 0;
            upVolList.AddRounded(upVol);

            var downVol = currentValue < prevValue ? currentVolume * -1 : 0;
            downVolList.AddRounded(downVol);

            var upVolSum = upVolList.TakeLastExt(length).Sum();
            var downVolSum = downVolList.TakeLastExt(length).Sum();

            var prevUpDownVol = upDownVolumeList.LastOrDefault();
            var upDownVol = downVolSum != 0 ? upVolSum / downVolSum : 0;
            upDownVolumeList.AddRounded(upDownVol);

            var signal = GetCompareSignal(upDownVol, prevUpDownVol);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Udv", upDownVolumeList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = upDownVolumeList;
        stockData.IndicatorName = IndicatorName.UpsideDownsideVolume;

        return stockData;
    }

    /// <summary>
    /// Calculates the Ease Of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateEaseOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14,
        double divisor = 1000000)
    {
        List<double> halfRangeList = new();
        List<double> midpointMoveList = new();
        List<double> emvList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var prevHalfRange = halfRangeList.LastOrDefault();
            var halfRange = (currentHigh - currentLow) * 0.5;
            var boxRatio = currentHigh - currentLow != 0 ? currentVolume / (currentHigh - currentLow) : 0;

            var prevMidpointMove = midpointMoveList.LastOrDefault();
            var midpointMove = halfRange - prevHalfRange;
            midpointMoveList.AddRounded(midpointMove);

            var emv = boxRatio != 0 ? divisor * ((midpointMove - prevMidpointMove) / boxRatio) : 0;
            emvList.AddRounded(emv);
        }

        var emvSmaList = GetMovingAverageList(stockData, maType, length, emvList);
        var emvSignalList = GetMovingAverageList(stockData, maType, length, emvSmaList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var emv = emvList[i];
            var emvSignal = emvSignalList[i];
            var prevEmv = i >= 1 ? emvList[i - 1] : 0;
            var prevEmvSignal = i >= 1 ? emvSignalList[i - 1] : 0;

            var signal = GetCompareSignal(emv - emvSignal, prevEmv - prevEmvSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Eom", emvList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = emvList;
        stockData.IndicatorName = IndicatorName.EaseOfMovement;

        return stockData;
    }

    /// <summary>
    /// Calculates the On Balance Volume Modified
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolumeModified(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 7, int length2 = 10)
    {
        List<Signal> signalsList = new();

        var obvList = CalculateOnBalanceVolume(stockData, maType, length1).CustomValuesList;
        var obvmList = GetMovingAverageList(stockData, maType, length1, obvList);
        var sigList = GetMovingAverageList(stockData, maType, length2, obvmList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var obvm = obvmList[i];
            var sig = sigList[i];
            var prevObvm = i >= 1 ? obvmList[i - 1] : 0;
            var prevSig = i >= 1 ? sigList[i - 1] : 0;

            var signal = GetCompareSignal(obvm - sig, prevObvm - prevSig);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Obvm", obvmList },
            { "Signal", sigList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = obvmList;
        stockData.IndicatorName = IndicatorName.OnBalanceVolumeModified;

        return stockData;
    }

    /// <summary>
    /// Calculates the On Balance Volume Reflex
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolumeReflex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 4, int signalLength = 14)
    {
        List<double> ovrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var prevOvr = ovrList.LastOrDefault();
            var ovr = currentValue > prevValue ? prevOvr + currentVolume : currentValue < prevValue ? prevOvr - currentVolume : prevOvr;
            ovrList.AddRounded(ovr);
        }

        var ovrSmaList = GetMovingAverageList(stockData, maType, signalLength, ovrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ovr = ovrList[i];
            var ovrEma = ovrSmaList[i];
            var prevOvr = i >= 1 ? ovrList[i - 1] : 0;
            var prevOvrEma = i >= 1 ? ovrSmaList[i - 1] : 0;

            var signal = GetCompareSignal(ovr - ovrEma, prevOvr - prevOvrEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Obvr", ovrList },
            { "Signal", ovrSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = ovrList;
        stockData.IndicatorName = IndicatorName.OnBalanceVolumeReflex;

        return stockData;
    }

    /// <summary>
    /// Calculates the On Balance Volume Disparity Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <param name="top"></param>
    /// <param name="bottom"></param>
    /// <returns></returns>
    public static StockData CalculateOnBalanceVolumeDisparityIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 33, int signalLength = 4, double top = 1.1, double bottom = 0.9)
    {
        List<double> obvdiList = new();
        List<double> bscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var obvList = CalculateOnBalanceVolume(stockData, maType, length).CustomValuesList;
        var obvSmaList = GetMovingAverageList(stockData, maType, length, obvList);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = obvList;
        var obvStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var obvSma = obvSmaList[i];
            var obvStdDev = obvStdDevList[i];
            var aTop = currentValue - (sma - (2 * stdDev));
            var aBot = currentValue + (2 * stdDev) - (sma - (2 * stdDev));
            var obv = obvList[i];
            var a = aBot != 0 ? aTop / aBot : 0;
            var bTop = obv - (obvSma - (2 * obvStdDev));
            var bBot = obvSma + (2 * obvStdDev) - (obvSma - (2 * obvStdDev));
            var b = bBot != 0 ? bTop / bBot : 0;

            var obvdi = 1 + b != 0 ? (1 + a) / (1 + b) : 0;
            obvdiList.AddRounded(obvdi);
        }

        var obvdiEmaList = GetMovingAverageList(stockData, maType, signalLength, obvdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var obvdi = obvdiList[i];
            var obvdiEma = obvdiEmaList[i];
            var prevObvdi = i >= 1 ? obvdiList[i - 1] : 0;

            var prevBsc = bscList.LastOrDefault();
            var bsc = (prevObvdi < bottom && obvdi > bottom) || obvdi > obvdiEma ? 1 : (prevObvdi > top && obvdi < top) ||
                obvdi < bottom ? -1 : prevBsc;
            bscList.AddRounded(bsc);

            var signal = GetCompareSignal(bsc, prevBsc);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Obvdi", obvdiList },
            { "Signal", obvdiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = obvdiList;
        stockData.IndicatorName = IndicatorName.OnBalanceVolumeDisparityIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Negative Volume Disparity Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <param name="top"></param>
    /// <param name="bottom"></param>
    /// <returns></returns>
    public static StockData CalculateNegativeVolumeDisparityIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 33, int signalLength = 4, double top = 1.1, double bottom = 0.9)
    {
        List<double> nvdiList = new();
        List<double> bscList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var nviList = CalculateNegativeVolumeIndex(stockData, maType, length).CustomValuesList;
        var nviSmaList = GetMovingAverageList(stockData, maType, length, nviList);
        var smaList = GetMovingAverageList(stockData, maType, length, inputList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        stockData.CustomValuesList = nviList;
        var nviStdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var sma = smaList[i];
            var stdDev = stdDevList[i];
            var nviSma = nviSmaList[i];
            var nviStdDev = nviStdDevList[i];
            var aTop = currentValue - (sma - (2 * stdDev));
            var aBot = (currentValue + (2 * stdDev)) - (sma - (2 * stdDev));
            var nvi = nviList[i];
            var a = aBot != 0 ? aTop / aBot : 0;
            var bTop = nvi - (nviSma - (2 * nviStdDev));
            var bBot = (nviSma + (2 * nviStdDev)) - (nviSma - (2 * nviStdDev));
            var b = bBot != 0 ? bTop / bBot : 0;

            var nvdi = 1 + b != 0 ? (1 + a) / (1 + b) : 0;
            nvdiList.AddRounded(nvdi);
        }

        var nvdiEmaList = GetMovingAverageList(stockData, maType, signalLength, nvdiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var nvdi = nvdiList[i];
            var nvdiEma = nvdiEmaList[i];
            var prevNvdi = i >= 1 ? nvdiList[i - 1] : 0;

            var prevBsc = bscList.LastOrDefault();
            var bsc = (prevNvdi < bottom && nvdi > bottom) || nvdi > nvdiEma ? 1 : (prevNvdi > top && nvdi < top) ||
                nvdi < bottom ? -1 : prevBsc;
            bscList.AddRounded(bsc);

            var signal = GetCompareSignal(bsc, prevBsc);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Nvdi", nvdiList },
            { "Signal", nvdiEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = nvdiList;
        stockData.IndicatorName = IndicatorName.NegativeVolumeDisparityIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Hawkeye Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <param name="divisor"></param>
    /// <returns></returns>
    public static StockData CalculateHawkeyeVolumeIndicator(this StockData stockData, InputName inputName = InputName.MedianPrice, int length = 200,
        double divisor = 3.6)
    {
        List<double> tempRangeList = new();
        List<double> tempVolumeList = new();
        List<double> u1List = new();
        List<double> d1List = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentValue = closeList[i];

            var currentVolume = volumeList[i];
            tempVolumeList.AddRounded(currentVolume);

            var range = currentHigh - currentLow;
            tempRangeList.AddRounded(range);

            var volumeSma = tempVolumeList.TakeLastExt(length).Average();
            var rangeSma = tempRangeList.TakeLastExt(length).Average();
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            var prevMidpoint = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;

            var u1 = divisor != 0 ? prevMidpoint + ((prevHigh - prevLow) / divisor) : prevMidpoint;
            u1List.AddRounded(u1);

            var d1 = divisor != 0 ? prevMidpoint - ((prevHigh - prevLow) / divisor) : prevMidpoint;
            d1List.AddRounded(d1);

            var rEnabled1 = range > rangeSma && currentValue < d1 && currentVolume > volumeSma;
            var rEnabled2 = currentValue < prevMidpoint;
            var rEnabled = rEnabled1 || rEnabled2;

            var gEnabled1 = currentValue > prevMidpoint;
            var gEnabled2 = range > rangeSma && currentValue > u1 && currentVolume > volumeSma;
            var gEnabled3 = currentHigh > prevHigh && range < rangeSma / 1.5 && currentVolume < volumeSma;
            var gEnabled4 = currentLow < prevLow && range < rangeSma / 1.5 && currentVolume > volumeSma;
            var gEnabled = gEnabled1 || gEnabled2 || gEnabled3 || gEnabled4;

            var grEnabled1 = range > rangeSma && currentValue > d1 && currentValue < u1 && currentVolume > volumeSma && currentVolume < volumeSma * 1.5 && currentVolume > prevVolume;
            var grEnabled2 = range < rangeSma / 1.5 && currentVolume < volumeSma / 1.5;
            var grEnabled3 = currentValue > d1 && currentValue < u1;
            var grEnabled = grEnabled1 || grEnabled2 || grEnabled3;

            var signal = GetConditionSignal(gEnabled, rEnabled);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Up", u1List },
            { "Dn", d1List }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.HawkeyeVolumeIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Herrick Payoff Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="pointValue"></param>
    /// <returns></returns>
    public static StockData CalculateHerrickPayoffIndex(this StockData stockData, InputName inputName = InputName.MedianPrice, double pointValue = 100)
    {
        List<double> kList = new();
        List<double> hpicList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentClose = closeList[i];
            var currentOpen = openList[i];
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevClose = i >= 1 ? closeList[i - 1] : 0;
            var prevOpen = i >= 1 ? openList[i - 1] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevK = kList.LastOrDefault();
            var absDiff = Math.Abs(currentClose - prevClose);
            var g = Math.Min(currentOpen, prevOpen);
            var k = MinPastValues(i, 1, currentValue - prevValue) * pointValue * currentVolume;
            var temp = g != 0 ? currentValue < prevValue ? 1 - (absDiff / 2 / g) : 1 + (absDiff / 2 / g) : 1;

            k *= temp;
            kList.AddRounded(k);

            var prevHpic = hpicList.LastOrDefault();
            var hpic = prevK + (k - prevK);
            hpicList.AddRounded(hpic);

            var signal = GetCompareSignal(hpic, prevHpic);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Hpi", hpicList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = hpicList;
        stockData.IndicatorName = IndicatorName.HerrickPayoffIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Finite Volume Elements
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="factor"></param>
    /// <returns></returns>
    public static StockData CalculateFiniteVolumeElements(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 22, double factor = 0.3)
    {
        List<double> fveList = new();
        List<double> bullList = new();
        List<double> bearList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var medianPriceList = CalculateMedianPrice(stockData).CustomValuesList;
        var typicalPriceList = CalculateTypicalPrice(stockData).CustomValuesList;
        var volumeSmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var medianPrice = medianPriceList[i];
            var typicalPrice = typicalPriceList[i];
            var prevTypicalPrice = i >= 1 ? typicalPriceList[i - 1] : 0;
            var volumeSma = volumeSmaList[i];
            var volume = volumeList[i];
            var close = inputList[i];
            var nmf = close - medianPrice + typicalPrice - prevTypicalPrice;
            var nvlm = nmf > factor * close / 100 ? volume : nmf < -factor * close / 100 ? -volume : 0;

            var prevFve = fveList.LastOrDefault();
            var prevFve2 = i >= 2 ? fveList[i - 2] : 0;
            var fve = volumeSma != 0 && length != 0 ? prevFve + (nvlm / volumeSma / length * 100) : prevFve;
            fveList.AddRounded(fve);

            var prevBullSlope = bullList.LastOrDefault();
            var bullSlope = fve - Math.Max(prevFve, prevFve2);
            bullList.AddRounded(bullSlope);

            var prevBearSlope = bearList.LastOrDefault();
            var bearSlope = fve - Math.Min(prevFve, prevFve2);
            bearList.AddRounded(bearSlope);

            var signal = GetBullishBearishSignal(bullSlope, prevBullSlope, bearSlope, prevBearSlope);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fve", fveList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = fveList;
        stockData.IndicatorName = IndicatorName.FiniteVolumeElements;

        return stockData;
    }

    /// <summary>
    /// Calculates the Relative Volume Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateRelativeVolumeIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        List<double> relVolList = new();
        List<double> dplList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var smaVolumeList = GetMovingAverageList(stockData, maType, length, volumeList);
        stockData.CustomValuesList = volumeList;
        var stdDevVolumeList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentVolume = volumeList[i];
            var currentValue = inputList[i];
            var av = smaVolumeList[i];
            var sd = stdDevVolumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var relVol = sd != 0 ? (currentVolume - av) / sd : 0;
            relVolList.AddRounded(relVol);

            var prevDpl = dplList.LastOrDefault();
            var dpl = relVol >= 2 ? prevValue : i >= 1 ? prevDpl : currentValue;
            dplList.AddRounded(dpl);

            var signal = GetCompareSignal(currentValue - dpl, prevValue - prevDpl);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Rvi", relVolList },
            { "Dpl", dplList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = relVolList;
        stockData.IndicatorName = IndicatorName.RelativeVolumeIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Freedom of Movement
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateFreedomOfMovement(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 60)
    {
        List<double> aMoveList = new();
        List<double> vBymList = new();
        List<double> theFomList = new();
        List<double> avfList = new();
        List<double> dplList = new();
        List<double> tempList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var relVolList = CalculateRelativeVolumeIndicator(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var currentRelVol = relVolList[i];
            tempList.AddRounded(currentRelVol);

            var aMove = prevValue != 0 ? Math.Abs(MinPastValues(i, 1, currentValue - prevValue) / prevValue) : 0;
            aMoveList.AddRounded(aMove);

            var list = aMoveList.TakeLastExt(length).ToList();
            var aMoveMax = list.Max();
            var aMoveMin = list.Min();
            var theMove = aMoveMax - aMoveMin != 0 ? (1 + ((aMove - aMoveMin) * (10 - 1))) / (aMoveMax - aMoveMin) : 0;
            var tList = tempList.TakeLastExt(length).ToList();
            var relVolMax = tList.Max();
            var relVolMin = tList.Min();
            var theVol = relVolMax - relVolMin != 0 ? (1 + ((currentRelVol - relVolMin) * (10 - 1))) / (relVolMax - relVolMin) : 0;

            var vBym = theMove != 0 ? theVol / theMove : 0;
            vBymList.AddRounded(vBym);

            var avf = vBymList.TakeLastExt(length).Average();
            avfList.AddRounded(avf);
        }

        stockData.CustomValuesList = vBymList;
        var sdfList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var vBym = vBymList[i];
            var avf = avfList[i];
            var sdf = sdfList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var theFom = sdf != 0 ? (vBym - avf) / sdf : 0;
            theFomList.AddRounded(theFom);

            var prevDpl = dplList.LastOrDefault();
            var dpl = theFom >= 2 ? prevValue : i >= 1 ? prevDpl : currentValue;
            dplList.AddRounded(dpl);

            var signal = GetCompareSignal(currentValue - dpl, prevValue - prevDpl);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Fom", theFomList },
            { "Dpl", dplList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = theFomList;
        stockData.IndicatorName = IndicatorName.FreedomOfMovement;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Price Confirmation Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolumePriceConfirmationIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int fastLength = 5, int slowLength = 20, int length = 8)
    {
        List<double> vpciList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var vwmaShortList = GetMovingAverageList(stockData, MovingAvgType.VolumeWeightedMovingAverage, fastLength, inputList);
        var vwmaLongList = GetMovingAverageList(stockData, MovingAvgType.VolumeWeightedMovingAverage, slowLength, inputList);
        var volumeSmaShortList = GetMovingAverageList(stockData, maType, fastLength, volumeList);
        var volumeSmaLongList = GetMovingAverageList(stockData, maType, slowLength, volumeList);
        var smaShortList = GetMovingAverageList(stockData, maType, fastLength, inputList);
        var smaLongList = GetMovingAverageList(stockData, maType, slowLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var vwmaLong = vwmaLongList[i];
            var vwmaShort = vwmaShortList[i];
            var volumeSmaLong = volumeSmaLongList[i];
            var volumeSmaShort = volumeSmaShortList[i];
            var smaLong = smaLongList[i];
            var smaShort = smaShortList[i];
            var vpc = vwmaLong - smaLong;
            var vpr = smaShort != 0 ? vwmaShort / smaShort : 0;
            var vm = volumeSmaLong != 0 ? volumeSmaShort / volumeSmaLong : 0;

            var vpci = vpc * vpr * vm;
            vpciList.AddRounded(vpci);
        }

        var vpciSmaList = GetMovingAverageList(stockData, maType, length, vpciList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vpci = vpciList[i];
            var vpciSma = vpciSmaList[i];
            var prevVpci = i >= 1 ? vpciList[i - 1] : 0;
            var prevVpciSma = i >= 1 ? vpciSmaList[i - 1] : 0;

            var signal = GetCompareSignal(vpci - vpciSma, prevVpci - prevVpciSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vpci", vpciList },
            { "Signal", vpciSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vpciList;
        stockData.IndicatorName = IndicatorName.VolumePriceConfirmationIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Positive Negative Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    public static StockData CalculateVolumePositiveNegativeIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, InputName inputName = InputName.TypicalPrice, int length = 30,
        int smoothLength = 3)
    {
        List<double> vmpList = new();
        List<double> vmnList = new();
        List<double> vpnList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, _, volumeList) = GetInputValuesList(inputName, stockData);

        var mavList = GetMovingAverageList(stockData, maType, length, volumeList);
        var atrList = CalculateAverageTrueRange(stockData, maType, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var mav = mavList[i];
            mav = mav > 0 ? mav : 1;
            var tp = inputList[i];
            var prevTp = i >= 1 ? inputList[i - 1] : 0;
            var atr = atrList[i];
            var currentVolume = volumeList[i];
            var mf = tp - prevTp;
            var mc = 0.1 * atr;

            var vmp = mf > mc ? currentVolume : 0;
            vmpList.AddRounded(vmp);

            var vmn = mf < -mc ? currentVolume : 0;
            vmnList.AddRounded(vmn);

            var vn = vmnList.TakeLastExt(length).Sum();
            var vp = vmpList.TakeLastExt(length).Sum();

            var vpn = mav != 0 && length != 0 ? (vp - vn) / mav / length * 100 : 0;
            vpnList.AddRounded(vpn);
        }

        var vpnEmaList = GetMovingAverageList(stockData, maType, smoothLength, vpnList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vpnEma = vpnEmaList[i];
            var prevVpnEma = i >= 1 ? vpnEmaList[i - 1] : 0;

            var signal = GetCompareSignal(vpnEma, prevVpnEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vpni", vpnList },
            { "Signal", vpnEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vpnList;
        stockData.IndicatorName = IndicatorName.VolumePositiveNegativeIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Accumulation Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAccumulationOscillator(this StockData stockData, int length = 14)
    {
        List<double> vaoList = new();
        List<double> vaoSumList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var medianValue = (currentHigh + currentLow) / 2;

            var vao = currentValue != medianValue ? currentVolume * (currentValue - medianValue) : currentVolume;
            vaoList.AddRounded(vao);

            var prevVaoSum = vaoSumList.LastOrDefault();
            var vaoSum = vaoList.TakeLastExt(length).Average();
            vaoSumList.AddRounded(vaoSum);

            var signal = GetCompareSignal(vaoSum, prevVaoSum);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vao", vaoSumList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vaoSumList;
        stockData.IndicatorName = IndicatorName.VolumeAccumulationOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Accumulation Percent
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeAccumulationPercent(this StockData stockData, int length = 10)
    {
        List<double> vapcList = new();
        List<double> tvaList = new();
        List<double> tempVolumeList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = inputList[i];

            var currentVolume = volumeList[i];
            tempVolumeList.AddRounded(currentVolume);

            var xt = currentHigh - currentLow != 0 ? ((2 * currentClose) - currentHigh - currentLow) / (currentHigh - currentLow) : 0;
            var tva = currentVolume * xt;
            tvaList.AddRounded(tva);

            var volumeSum = tempVolumeList.TakeLastExt(length).Sum();
            var tvaSum = tvaList.TakeLastExt(length).Sum();

            var prevVapc = vapcList.LastOrDefault();
            var vapc = volumeSum != 0 ? MinOrMax(100 * tvaSum / volumeSum, 100, 0) : 0;
            vapcList.AddRounded(vapc);

            var signal = GetCompareSignal(vapc, prevVapc);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vapc", vapcList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vapcList;
        stockData.IndicatorName = IndicatorName.VolumeAccumulationPercent;

        return stockData;
    }

    /// <summary>
    /// Calculates the Volume Flow Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="inputName"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="signalLength"></param>
    /// <param name="smoothLength"></param>
    /// <param name="coef"></param>
    /// <param name="vcoef"></param>
    /// <returns></returns>
    public static StockData CalculateVolumeFlowIndicator(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        InputName inputName = InputName.TypicalPrice, int length1 = 130, int length2 = 30, int signalLength = 5, int smoothLength = 3,
        double coef = 0.2, double vcoef = 2.5)
    {
        List<double> interList = new();
        List<double> tempList = new();
        List<double> vcpList = new();
        List<double> vcpVaveSumList = new();
        List<double> dList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        var smaVolumeList = GetMovingAverageList(stockData, maType, length1, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var inter = currentValue > 0 && prevValue > 0 ? Math.Log(currentValue) - Math.Log(prevValue) : 0;
            interList.AddRounded(inter);
        }

        stockData.CustomValuesList = interList;
        var vinterList = CalculateStandardDeviationVolatility(stockData, maType, length2).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var vinter = vinterList[i];
            var currentVolume = volumeList[i];
            var currentClose = closeList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevVave = tempList.LastOrDefault();
            var vave = smaVolumeList[i];
            tempList.AddRounded(vave);

            var cutoff = currentClose * vinter * coef;
            var vmax = prevVave * vcoef;
            var vc = Math.Min(currentVolume, vmax);
            var mf = MinPastValues(i, 1, currentValue - prevValue);

            var vcp = mf > cutoff ? vc : mf < cutoff * -1 ? vc * -1 : mf > 0 ? vc : mf < 0 ? vc * -1 : 0;
            vcpList.AddRounded(vcp);

            var vcpSum = vcpList.TakeLastExt(length1).Sum();
            var vcpVaveSum = vave != 0 ? vcpSum / vave : 0;
            vcpVaveSumList.AddRounded(vcpVaveSum);
        }

        var vfiList = GetMovingAverageList(stockData, maType, smoothLength, vcpVaveSumList);
        var vfiEmaList = GetMovingAverageList(stockData, MovingAvgType.ExponentialMovingAverage, signalLength, vfiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var vfi = vfiList[i];
            var vfima = vfiEmaList[i];

            var prevD = dList.LastOrDefault();
            var d = vfi - vfima;
            dList.AddRounded(d);

            var signal = GetCompareSignal(d, prevD);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Vfi", vfiList },
            { "Signal", vfiEmaList },
            { "Histogram", dList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = vfiList;
        stockData.IndicatorName = IndicatorName.VolumeFlowIndicator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Volume Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <returns></returns>
    public static StockData CalculatePriceVolumeOscillator(this StockData stockData, int length1 = 50, int length2 = 14)
    {
        List<double> aList = new();
        List<double> bList = new();
        List<double> absAList = new();
        List<double> absBList = new();
        List<double> oscAList = new();
        List<double> oscBList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= length1 ? inputList[i - length1] : 0;
            var prevVolume = i >= length2 ? volumeList[i - length2] : 0;

            var a = MinPastValues(i, length1, currentValue - prevValue);
            aList.AddRounded(a);

            var b = MinPastValues(i, length2, currentVolume - prevVolume);
            bList.AddRounded(b);

            var absA = Math.Abs(a);
            absAList.AddRounded(absA);

            var absB = Math.Abs(b);
            absBList.AddRounded(absB);

            var aSum = aList.TakeLastExt(length1).Sum();
            var bSum = bList.TakeLastExt(length2).Sum();
            var absASum = absAList.TakeLastExt(length1).Sum();
            var absBSum = absBList.TakeLastExt(length2).Sum();

            var oscA = absASum != 0 ? aSum / absASum : 0;
            oscAList.AddRounded(oscA);

            var oscB = absBSum != 0 ? bSum / absBSum : 0;
            oscBList.AddRounded(oscB);

            var signal = GetConditionSignal(oscA > 0 && oscB > 0, oscA < 0 && oscB > 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Po", oscAList },
            { "Vo", oscBList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = new List<double>();
        stockData.IndicatorName = IndicatorName.PriceVolumeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Price Volume Rank
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="fastLength"></param>
    /// <param name="slowLength"></param>
    /// <returns></returns>
    public static StockData CalculatePriceVolumeRank(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int fastLength = 5, int slowLength = 10)
    {
        List<double> pvrList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentVolume = volumeList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;

            double pvr = currentValue > prevValue && currentVolume > prevVolume ? 1 : currentValue > prevValue && currentVolume <= prevVolume ? 2 :
                currentValue <= prevValue && currentVolume <= prevVolume ? 3 : 4;
            pvrList.AddRounded(pvr);
        }

        var pvrFastSmaList = GetMovingAverageList(stockData, maType, fastLength, pvrList);
        var pvrSlowSmaList = GetMovingAverageList(stockData, maType, slowLength, pvrList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var fastSma = pvrFastSmaList[i];
            var slowSma = pvrSlowSmaList[i];
            var prevFastSma = i >= 1 ? pvrFastSmaList[i - 1] : 0;
            var prevSlowSma = i >= 1 ? pvrSlowSmaList[i - 1] : 0;

            var signal = GetCompareSignal(fastSma - slowSma, prevFastSma - prevSlowSma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Pvr", pvrList },
            { "SlowSignal", pvrSlowSmaList },
            { "FastSignal", pvrFastSmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = pvrList;
        stockData.IndicatorName = IndicatorName.PriceVolumeRank;

        return stockData;
    }

    /// <summary>
    /// Calculates the Twiggs Money Flow
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTwiggsMoneyFlow(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 21)
    {
        List<double> adList = new();
        List<double> tmfList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        var volumeEmaList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var trh = Math.Max(currentHigh, prevPrice);
            var trl = Math.Min(currentLow, prevPrice);

            var ad = trh - trl != 0 && currentVolume != 0 ? (currentPrice - trl - (trh - currentPrice)) / (trh - trl) * currentVolume : 0;
            adList.AddRounded(ad);
        }

        var smoothAdList = GetMovingAverageList(stockData, maType, length, adList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentEmaVolume = volumeEmaList[i];
            var smoothAd = smoothAdList[i];
            var prevTmf1 = i >= 1 ? tmfList[i - 1] : 0;
            var prevTmf2 = i >= 2 ? tmfList[i - 2] : 0;

            var tmf = currentEmaVolume != 0 ? MinOrMax(smoothAd / currentEmaVolume, 1, -1) : 0;
            tmfList.AddRounded(tmf);

            var signal = GetRsiSignal(tmf - prevTmf1, prevTmf1 - prevTmf2, tmf, prevTmf1, 0.2, -0.2);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tmf", tmfList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tmfList;
        stockData.IndicatorName = IndicatorName.TwiggsMoneyFlow;

        return stockData;
    }

    /// <summary>
    /// Calculates the Trade Volume Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="minTickValue"></param>
    /// <returns></returns>
    public static StockData CalculateTradeVolumeIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 14, double minTickValue = 0.5)
    {
        List<double> tviList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var currentVolume = volumeList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var priceChange = currentPrice - prevPrice;

            var prevTvi = tviList.LastOrDefault();
            var tvi = priceChange > minTickValue ? prevTvi + currentVolume : priceChange < -minTickValue ?
                prevTvi - currentVolume : prevTvi;
            tviList.AddRounded(tvi);
        }

        var tviSignalList = GetMovingAverageList(stockData, maType, length, tviList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var tvi = tviList[i];
            var tviSignal = tviSignalList[i];
            var prevTvi = i >= 1 ? tviList[i - 1] : 0;
            var prevTviSignal = i >= 1 ? tviSignalList[i - 1] : 0;

            var signal = GetCompareSignal(tvi - tviSignal, prevTvi - prevTviSignal);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tvi", tviList },
            { "Signal", tviSignalList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tviList;
        stockData.IndicatorName = IndicatorName.TradeVolumeIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the TFS Volume Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateTFSVolumeOscillator(this StockData stockData, int length = 7)
    {
        List<double> totvList = new();
        List<double> tfsvoList = new();
        List<Signal> signalsList = new();
        var (inputList, _, _, openList, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var open = openList[i];
            var close = inputList[i];
            var volume = volumeList[i];

            var totv = close > open ? volume : close < open ? -volume : 0;
            totvList.AddRounded(totv);

            var totvSum = totvList.TakeLastExt(length).Sum();
            var prevTfsvo = tfsvoList.LastOrDefault();
            var tfsvo = length != 0 ? totvSum / length : 0;
            tfsvoList.AddRounded(tfsvo);

            var signal = GetCompareSignal(tfsvo, prevTfsvo);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Tfsvo", tfsvoList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = tfsvoList;
        stockData.IndicatorName = IndicatorName.TFSVolumeOscillator;

        return stockData;
    }

    /// <summary>
    /// Calculates the Market Facilitation Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <returns></returns>
    public static StockData CalculateMarketFacilitationIndex(this StockData stockData)
    {
        List<double> mfiList = new();
        List<Signal> signalsList = new();
        var (_, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i];
            var prevVolume = i >= 1 ? volumeList[i - 1] : 0;

            var prevMfi = mfiList.LastOrDefault();
            var mfi = currentVolume != 0 ? (currentHigh - currentLow) / currentVolume : 0;
            mfiList.AddRounded(mfi);

            var mfiDiff = mfi - prevMfi;
            var volDiff = currentVolume - prevVolume;

            var signal = GetConditionSignal(mfiDiff > 0, volDiff > 0);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mi", mfiList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mfiList;
        stockData.IndicatorName = IndicatorName.MarketFacilitationIndex;

        return stockData;
    }

    /// <summary>
    /// Calculates the Multi Vote On Balance Volume
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateMultiVoteOnBalanceVolume(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14)
    {
        List<double> mvoList = new();
        List<Signal> signalsList = new();
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentHigh = highList[i];
            var currentClose = inputList[i];
            var currentLow = lowList[i];
            var currentVolume = volumeList[i] / 1000000;
            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            var prevHigh = i >= 1 ? highList[i - 1] : 0;
            var prevLow = i >= 1 ? lowList[i - 1] : 0;
            double highVote = currentHigh > prevHigh ? 1 : currentHigh < prevHigh ? -1 : 0;
            double lowVote = currentLow > prevLow ? 1 : currentLow < prevLow ? -1 : 0;
            double closeVote = currentClose > prevClose ? 1 : currentClose < prevClose ? -1 : 0;
            var totalVotes = highVote + lowVote + closeVote;

            var prevMvo = mvoList.LastOrDefault();
            var mvo = prevMvo + (currentVolume * totalVotes);
            mvoList.AddRounded(mvo);
        }

        var mvoEmaList = GetMovingAverageList(stockData, maType, length, mvoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var mvo = mvoList[i];
            var mvoEma = mvoEmaList[i];
            var prevMvo = i >= 1 ? mvoList[i - 1] : 0;
            var prevMvoEma = i >= 1 ? mvoEmaList[i - 1] : 0;

            var signal = GetCompareSignal(mvo - mvoEma, prevMvo - prevMvoEma);
            signalsList.Add(signal);
        }

        stockData.OutputValues = new Dictionary<string, List<double>>
        {
            { "Mvo", mvoList },
            { "Signal", mvoEmaList }
        };
        stockData.SignalsList = signalsList;
        stockData.CustomValuesList = mvoList;
        stockData.IndicatorName = IndicatorName.MultiVoteOnBalanceVolume;

        return stockData;
    }
}
