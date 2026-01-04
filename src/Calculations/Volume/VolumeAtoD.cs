
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chaikin Money Flow
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateChaikinMoneyFlow(this StockData stockData, int length = 20)
    {
        List<double> chaikinMoneyFlowList = new(stockData.Count);
        List<double> tempVolumeList = new(stockData.Count);
        List<double> moneyFlowVolumeList = new(stockData.Count);
        var volumeSumWindow = new RollingSum();
        var mfVolumeSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
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
            tempVolumeList.Add(currentVolume);
            volumeSumWindow.Add(currentVolume);

            var moneyFlowVolume = moneyFlowMultiplier * currentVolume;
            moneyFlowVolumeList.Add(moneyFlowVolume);
            mfVolumeSumWindow.Add(moneyFlowVolume);

            var volumeSum = volumeSumWindow.Sum(length);
            var mfVolumeSum = mfVolumeSumWindow.Sum(length);

            var cmf = volumeSum != 0 ? mfVolumeSum / volumeSum : 0;
            chaikinMoneyFlowList.Add(cmf);

            var signal = GetCompareSignal(cmf - prevCmf1, prevCmf1 - prevCmf2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmf", chaikinMoneyFlowList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(chaikinMoneyFlowList);
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
        var count = stockData.Count;
        List<double> adlList = new(count);
        List<Signal>? signalsList = CreateSignalsList(stockData, count);
        var (inputList, highList, lowList, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < count; i++)
        {
            var currentLow = lowList[i];
            var currentHigh = highList[i];
            var currentClose = inputList[i];
            var currentVolume = volumeList[i];
            var moneyFlowMultiplier = currentHigh - currentLow != 0 ?
                (currentClose - currentLow - (currentHigh - currentClose)) / (currentHigh - currentLow) : 0;
            var moneyFlowVolume = moneyFlowMultiplier * currentVolume;

            var prevAdl = i >= 1 ? adlList[i - 1] : 0;
            var adl = prevAdl + moneyFlowVolume;
            adlList.Add(adl);
        }

        var adlSignalList = GetMovingAverageList(stockData, maType, length, adlList);
        for (var i = 0; i < count; i++)
        {
            var adl = adlList[i];
            var prevAdl = i >= 1 ? adlList[i - 1] : 0;
            var adlSignal = adlSignalList[i];
            var prevAdlSignal = i >= 1 ? adlSignalList[i - 1] : 0;

            var signal = GetCompareSignal(adl - adlSignal, prevAdl - prevAdlSignal);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Adl", adlList },
            { "AdlSignal", adlSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(adlList);
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
        List<double> chgList = new(stockData.Count);
        List<double> rList = new(stockData.Count);
        List<double> kList = new(stockData.Count);
        var rWindow = new RollingMinMax(length);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        var avgvList = GetMovingAverageList(stockData, maType, length, volumeList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var chg = MinPastValues(i, 1, currentValue - prevValue);
            chgList.Add(chg);
        }

        var avgcList = GetMovingAverageList(stockData, maType, length, chgList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var avgv = avgvList[i];
            var avgc = avgcList[i];

            var r = Math.Abs(avgv * avgc) > 0 ? Math.Log(Math.Abs(avgv * avgc)) * Math.Sign(avgc) : 0;
            rList.Add(r);
            rWindow.Add(r);

            var rh = rWindow.Max;
            var rl = rWindow.Min;
            var rs = rh != rl ? (r - rl) / (rh - rl) * 100 : 0;

            var k = (rs * 2) - 100;
            kList.Add(k);
        }

        var ksList = GetMovingAverageList(stockData, maType, smoothLength, kList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ks = ksList[i];
            var prevKs = i >= 1 ? ksList[i - 1] : 0;

            var signal = GetCompareSignal(ks, prevKs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Amfo", ksList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(ksList);
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
        List<double> v1List = new(stockData.Count);
        List<double> v2List = new(stockData.Count);
        List<double> v3List = new(stockData.Count);
        List<double> v4List = new(stockData.Count);
        List<double> v5List = new(stockData.Count);
        List<double> v6List = new(stockData.Count);
        List<double> v7List = new(stockData.Count);
        List<double> v8List = new(stockData.Count);
        List<double> v9List = new(stockData.Count);
        List<double> v10List = new(stockData.Count);
        List<double> v11List = new(stockData.Count);
        List<double> v12List = new(stockData.Count);
        List<double> v13List = new(stockData.Count);
        List<double> v14List = new(stockData.Count);
        List<double> v15List = new(stockData.Count);
        List<double> v16List = new(stockData.Count);
        List<double> v17List = new(stockData.Count);
        List<double> v18List = new(stockData.Count);
        List<double> v19List = new(stockData.Count);
        List<double> v20List = new(stockData.Count);
        List<double> v21List = new(stockData.Count);
        List<double> v22List = new(stockData.Count);
        var v3Window = new RollingMinMax(length);
        var v4Window = new RollingMinMax(length);
        var v5Window = new RollingMinMax(length);
        var v6Window = new RollingMinMax(length);
        var v7Window = new RollingMinMax(length);
        var v8Window = new RollingMinMax(length);
        var v9Window = new RollingMinMax(length);
        var v10Window = new RollingMinMax(length);
        var v11Window = new RollingMinMax(length);
        var v12Window = new RollingMinMax(length);
        var v13Window = new RollingMinMax(length);
        var v14Window = new RollingMinMax(length);
        var v15Window = new RollingMinMax(length);
        var v16Window = new RollingMinMax(length);
        var v17Window = new RollingMinMax(length);
        var v18Window = new RollingMinMax(length);
        var v19Window = new RollingMinMax(length);
        var v20Window = new RollingMinMax(length);
        var v21Window = new RollingMinMax(length);
        var v22Window = new RollingMinMax(length);
        List<Signal>? signalsList = CreateSignalsList(stockData);
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

            var prevV1 = i >= 1 ? v1List[i - 1] : 0;
            var v1 = currentClose > currentOpen ? range / ((2 * range) + currentOpen - currentClose) * currentVolume :
                currentClose < currentOpen ? (range + currentClose - currentOpen) / ((2 * range) + currentClose - currentOpen) * currentVolume :
                0.5 * currentVolume;
            v1List.Add(v1);

            var prevV2 = i >= 1 ? v2List[i - 1] : 0;
            var v2 = currentVolume - v1;
            v2List.Add(v2);

            var prevV3 = i >= 1 ? v3List[i - 1] : 0;
            var v3 = v1 + v2;
            v3List.Add(v3);
            v3Window.Add(v3);

            var v4 = v1 * range;
            v4List.Add(v4);
            v4Window.Add(v4);

            var v5 = (v1 - v2) * range;
            v5List.Add(v5);
            v5Window.Add(v5);

            var v6 = v2 * range;
            v6List.Add(v6);
            v6Window.Add(v6);

            var v7 = (v2 - v1) * range;
            v7List.Add(v7);
            v7Window.Add(v7);

            var v8 = range != 0 ? v1 / range : 0;
            v8List.Add(v8);
            v8Window.Add(v8);

            var v9 = range != 0 ? (v1 - v2) / range : 0;
            v9List.Add(v9);
            v9Window.Add(v9);

            var v10 = range != 0 ? v2 / range : 0;
            v10List.Add(v10);
            v10Window.Add(v10);

            var v11 = range != 0 ? (v2 - v1) / range : 0;
            v11List.Add(v11);
            v11Window.Add(v11);

            var v12 = range != 0 ? v3 / range : 0;
            v12List.Add(v12);
            v12Window.Add(v12);

            var v13 = v3 + prevV3;
            v13List.Add(v13);
            v13Window.Add(v13);

            var v14 = (v1 + prevV1) * highLowRange;
            v14List.Add(v14);
            v14Window.Add(v14);

            var v15 = (v1 + prevV1 - v2 - prevV2) * highLowRange;
            v15List.Add(v15);
            v15Window.Add(v15);

            var v16 = (v2 + prevV2) * highLowRange;
            v16List.Add(v16);
            v16Window.Add(v16);

            var v17 = (v2 + prevV2 - v1 - prevV1) * highLowRange;
            v17List.Add(v17);
            v17Window.Add(v17);

            var v18 = highLowRange != 0 ? (v1 + prevV1) / highLowRange : 0;
            v18List.Add(v18);
            v18Window.Add(v18);

            var v19 = highLowRange != 0 ? (v1 + prevV1 - v2 - prevV2) / highLowRange : 0;
            v19List.Add(v19);
            v19Window.Add(v19);

            var v20 = highLowRange != 0 ? (v2 + prevV2) / highLowRange : 0;
            v20List.Add(v20);
            v20Window.Add(v20);

            var v21 = highLowRange != 0 ? (v2 + prevV2 - v1 - prevV1) / highLowRange : 0;
            v21List.Add(v21);
            v21Window.Add(v21);

            var v22 = highLowRange != 0 ? v13 / highLowRange : 0;
            v22List.Add(v22);
            v22Window.Add(v22);

            var c1 = v3 == v3Window.Min;
            var c2 = v4 == v4Window.Max && currentClose > currentOpen;
            var c3 = v5 == v5Window.Max && currentClose > currentOpen;
            var c4 = v6 == v6Window.Max && currentClose < currentOpen;
            var c5 = v7 == v7Window.Max && currentClose < currentOpen;
            var c6 = v8 == v8Window.Min && currentClose < currentOpen;
            var c7 = v9 == v9Window.Min && currentClose < currentOpen;
            var c8 = v10 == v10Window.Min && currentClose > currentOpen;
            var c9 = v11 == v11Window.Min && currentClose > currentOpen;
            var c10 = v12 == v12Window.Max;
            var c11 = v13 == v13Window.Min && currentClose > currentOpen && prevClose > prevOpen;
            var c12 = v14 == v14Window.Max && currentClose > currentOpen && prevClose > prevOpen;
            var c13 = v15 == v15Window.Max && currentClose > currentOpen && prevClose < prevOpen;
            var c14 = v16 == v16Window.Min && currentClose < currentOpen && prevClose < prevOpen;
            var c15 = v17 == v17Window.Min && currentClose < currentOpen && prevClose < prevOpen;
            var c16 = v18 == v18Window.Min && currentClose < currentOpen && prevClose < prevOpen;
            var c17 = v19 == v19Window.Min && currentClose > currentOpen && prevClose < prevOpen;
            var c18 = v20 == v20Window.Min && currentClose > currentOpen && prevClose > prevOpen;
            var c19 = v21 == v21Window.Min && currentClose > currentOpen && prevClose > prevOpen;
            var c20 = v22 == v22Window.Min;
            var climaxUp = c2 || c3 || c8 || c9 || c12 || c13 || c18 || c19;
            var climaxDown = c4 || c5 || c6 || c7 || c14 || c15 || c16 || c17;
            var churn = c10 || c20;
            var lowVolue = c1 || c11;

            var signal = GetConditionSignal(climaxUp, climaxDown);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Bvi", v1List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(v1List);
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
        List<double> priceVolList = new(stockData.Count);
        List<double> fastBuffList = new(stockData.Count);
        List<double> slowBuffList = new(stockData.Count);
        List<double> tempVolumeList = new(stockData.Count);
        var priceVolSumWindow = new RollingSum();
        var volumeSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, volumeList) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var currentVolume = volumeList[i];
            tempVolumeList.Add(currentVolume);
            volumeSumWindow.Add(currentVolume);

            var priceVol = currentValue * currentVolume;
            priceVolList.Add(priceVol);
            priceVolSumWindow.Add(priceVol);

            var fastBuffNum = priceVolSumWindow.Sum(fastLength);
            var fastBuffDenom = volumeSumWindow.Sum(fastLength);

            var prevFastBuff = i >= 1 ? fastBuffList[i - 1] : 0;
            var fastBuff = fastBuffDenom != 0 ? fastBuffNum / fastBuffDenom : 0;
            fastBuffList.Add(fastBuff);

            var slowBuffNum = priceVolSumWindow.Sum(slowLength);
            var slowBuffDenom = volumeSumWindow.Sum(slowLength);

            var prevSlowBuff = i >= 1 ? slowBuffList[i - 1] : 0;
            var slowBuff = slowBuffDenom != 0 ? slowBuffNum / slowBuffDenom : 0;
            slowBuffList.Add(slowBuff);

            var signal = GetCompareSignal(fastBuff - slowBuff, prevFastBuff - prevSlowBuff);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "FastBuff", fastBuffList },
            { "SlowBuff", slowBuffList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.BuffAverage;

        return stockData;
    }

}

