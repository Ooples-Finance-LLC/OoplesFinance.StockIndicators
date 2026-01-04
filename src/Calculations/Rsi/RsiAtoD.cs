using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Core;

namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the index of the connors relative strength.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length1">Length of the streak.</param>
    /// <param name="length2">Length of the rsi.</param>
    /// <param name="length3">Length of the roc.</param>
    /// <returns></returns>
    public static StockData CalculateConnorsRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod,
        int length1 = 2, int length2 = 3, int length3 = 100)
    {
        List<double> streakList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<double> pctRankList = new(stockData.Count);
        List<double> connorsRsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        using var rocOrder = new RollingOrderStatistic(length3);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length2, length2).CustomValuesList;
        var rocList = CalculateRateOfChange(stockData, length3).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var roc = rocList[i];
            tempList.Add(roc);
            rocOrder.Add(roc);

            var count = Math.Max(0, rocOrder.CountLessThanOrEqual(roc) - 1);
            var pctRank = MinOrMax((double)count / length3 * 100, 100, 0);
            pctRankList.Add(pctRank);

            var prevStreak = GetLastOrDefault(streakList);
            var streak = currentValue > prevValue ? prevStreak >= 0 ? prevStreak + 1 : 1 : currentValue < prevValue ? prevStreak <= 0 ?
                prevStreak - 1 : -1 : 0;
            streakList.Add(streak);
        }

        stockData.SetCustomValues(streakList);
        var rsiStreakList = CalculateRelativeStrengthIndex(stockData, maType, length1, length1).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRsi = rsiList[i];
            var percentRank = pctRankList[i];
            var streakRsi = rsiStreakList[i];
            var prevConnorsRsi1 = i >= 1 ? connorsRsiList[i - 1] : 0;
            var prevConnorsRsi2 = i >= 2 ? connorsRsiList[i - 2] : 0;

            var connorsRsi = MinOrMax((currentRsi + percentRank + streakRsi) / 3, 100, 0);
            connorsRsiList.Add(connorsRsi);

            var signal = GetRsiSignal(connorsRsi - prevConnorsRsi1, prevConnorsRsi1 - prevConnorsRsi2, connorsRsi, prevConnorsRsi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Rsi", rsiList },
            { "PctRank", pctRankList },
            { "StreakRsi", rsiStreakList },
            { "ConnorsRsi", connorsRsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(connorsRsiList);
        stockData.IndicatorName = IndicatorName.ConnorsRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the index of the asymmetrical relative strength.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAsymmetricalRelativeStrengthIndex(this StockData stockData, int length = 14)
    {
        List<double> rocList = new(stockData.Count);
        List<double> upSumList = new(stockData.Count);
        List<double> downSumList = new(stockData.Count);
        List<double> arsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum upCountSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var prevArsi1 = i >= 1 ? arsiList[i - 1] : 0;
            var prevArsi2 = i >= 2 ? arsiList[i - 2] : 0;
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var roc = prevValue != 0 ? MinPastValues(i, 1, currentValue - prevValue) / prevValue * 100 : 0;
            rocList.Add(roc);

            var upFlag = roc >= 0 ? 1 : 0;
            upCountSum.Add(upFlag);
            double upCount = upCountSum.Sum(length);
            var upAlpha = upCount != 0 ? 1 / upCount : 0;
            var posRoc = roc > 0 ? roc : 0;
            var negRoc = roc < 0 ? Math.Abs(roc) : 0;

            var prevUpSum = GetLastOrDefault(upSumList);
            var upSum = (upAlpha * posRoc) + ((1 - upAlpha) * prevUpSum);
            upSumList.Add(upSum);

            var downCount = length - upCount;
            var downAlpha = downCount != 0 ? 1 / downCount : 0;

            var prevDownSum = GetLastOrDefault(downSumList);
            var downSum = (downAlpha * negRoc) + ((1 - downAlpha) * prevDownSum);
            downSumList.Add(downSum);

            var ars = downSum != 0 ? upSum / downSum : 0;
            var arsi = downSum == 0 ? 100 : upSum == 0 ? 0 : MinOrMax(100 - (100 / (1 + ars)), 100, 0);
            arsiList.Add(arsi);

            var signal = GetRsiSignal(arsi - prevArsi1, prevArsi1 - prevArsi2, arsi, prevArsi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Arsi", arsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(arsiList);
        stockData.IndicatorName = IndicatorName.AsymmetricalRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Adaptive Relative Strength Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAdaptiveRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14)
    {
        List<double> arsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var rsiList = CalculateRelativeStrengthIndex(stockData, maType, length, length).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var alpha = 2 * Math.Abs((rsi / 100) - 0.5);

            var prevArsi = GetLastOrDefault(arsiList);
            var arsi = (alpha * currentValue) + ((1 - alpha) * prevArsi);
            arsiList.Add(arsi);

            var signal = GetCompareSignal(currentValue - arsi, prevValue - prevArsi);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Arsi", arsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(arsiList);
        stockData.IndicatorName = IndicatorName.AdaptiveRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the average absolute error normalization.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <returns></returns>
    public static StockData CalculateAverageAbsoluteErrorNormalization(this StockData stockData, int length = 14)
    {
        List<double> yList = new(stockData.Count);
        List<double> eList = new(stockData.Count);
        List<double> eAbsList = new(stockData.Count);
        List<double> aList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum eAbsSum = new();
        RollingSum eSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevY = i >= 1 ? yList[i - 1] : currentValue;
            var prevA1 = i >= 1 ? aList[i - 1] : 0;
            var prevA2 = i >= 2 ? aList[i - 2] : 0;

            var e = currentValue - prevY;
            eList.Add(e);
            eSum.Add(e);

            var eAbs = Math.Abs(e);
            eAbsList.Add(eAbs);
            eAbsSum.Add(eAbs);

            var eAbsSma = eAbsSum.Average(length);
            var eSma = eSum.Average(length);

            var a = eAbsSma != 0 ? MinOrMax(eSma / eAbsSma, 1, -1) : 0;
            aList.Add(a);

            var y = currentValue + (a * eAbsSma);
            yList.Add(y);

            var signal = GetRsiSignal(a - prevA1, prevA1 - prevA2, a, prevA1, 0.8, -0.8);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Aaen", aList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(aList);
        stockData.IndicatorName = IndicatorName.AverageAbsoluteErrorNormalization;

        return stockData;
    }


    /// <summary>
    /// Calculates the Apirine Slow Relative Strength Index
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="smoothLength">Length of the smooth.</param>
    /// <returns></returns>
    public static StockData CalculateApirineSlowRelativeStrengthIndex(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, 
        int length = 14, int smoothLength = 6)
    {
        List<double> r2List = new(stockData.Count);
        List<double> r3List = new(stockData.Count);
        List<double> rrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var emaList = GetMovingAverageList(stockData, maType, smoothLength, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var r1 = emaList[i];

            var r2 = currentValue > r1 ? currentValue - r1 : 0;
            r2List.Add(r2);

            var r3 = currentValue < r1 ? r1 - currentValue : 0;
            r3List.Add(r3);
        }

        var r4List = GetMovingAverageList(stockData, maType, length, r2List);
        var r5List = GetMovingAverageList(stockData, maType, length, r3List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var r4 = r4List[i];
            var r5 = r5List[i];
            var prevRr1 = i >= 1 ? rrList[i - 1] : 0;
            var prevRr2 = i >= 2 ? rrList[i - 2] : 0;
            var rs = r5 != 0 ? r4 / r5 : 0;

            var rr = r5 == 0 ? 100 : r4 == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rrList.Add(rr);

            var signal = GetRsiSignal(rr - prevRr1, prevRr1 - prevRr2, rr, prevRr1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Asrsi", rrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rrList);
        stockData.IndicatorName = IndicatorName.ApirineSlowRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Breakout Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="inputName"></param>
    /// <param name="length"></param>
    /// <param name="lbLength"></param>
    /// <returns></returns>
    public static StockData CalculateBreakoutRelativeStrengthIndex(this StockData stockData, InputName inputName = InputName.FullTypicalPrice,
        int length = 14, int lbLength = 2)
    {
        List<double> brsiList = new(stockData.Count);
        List<double> posPowerList = new(stockData.Count);
        List<double> boPowerList = new(stockData.Count);
        List<double> negPowerList = new(stockData.Count);
        List<double> tempList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum volumeSumWindow = new();
        RollingSum posPowerSumWindow = new();
        RollingSum negPowerSumWindow = new();
        var (inputList, highList, lowList, openList, closeList, volumeList) = GetInputValuesList(inputName, stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var currentClose = closeList[i];
            var currentOpen = openList[i];
            var prevBrsi1 = i >= 1 ? brsiList[i - 1] : 0;
            var prevBrsi2 = i >= 2 ? brsiList[i - 2] : 0;

            var currentVolume = volumeList[i];
            tempList.Add(currentVolume);
            volumeSumWindow.Add(currentVolume);

            var boVolume = volumeSumWindow.Sum(lbLength);
            var boStrength = currentHigh - currentLow != 0 ? (currentClose - currentOpen) / (currentHigh - currentLow) : 0;

            var prevBoPower = GetLastOrDefault(boPowerList);
            var boPower = currentValue * boStrength * boVolume;
            boPowerList.Add(boPower);

            var posPower = boPower > prevBoPower ? Math.Abs(boPower) : 0;
            posPowerList.Add(posPower);
            posPowerSumWindow.Add(posPower);

            var negPower = boPower < prevBoPower ? Math.Abs(boPower) : 0;
            negPowerList.Add(negPower);
            negPowerSumWindow.Add(negPower);

            var posPowerSum = posPowerSumWindow.Sum(length);
            var negPowerSum = negPowerSumWindow.Sum(length);
            var boRatio = negPowerSum != 0 ? posPowerSum / negPowerSum : 0;

            var brsi = negPowerSum == 0 ? 100 : posPowerSum == 0 ? 0 : MinOrMax(100 - (100 / (1 + boRatio)), 100, 0);
            brsiList.Add(brsi);

            var signal = GetRsiSignal(brsi - prevBrsi1, prevBrsi1 - prevBrsi2, brsi, prevBrsi1, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Brsi", brsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(brsiList);
        stockData.IndicatorName = IndicatorName.BreakoutRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Double Smoothed Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateDoubleSmoothedRelativeStrengthIndex(this StockData stockData, 
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 2, int length2 = 5, int length3 = 25)
    {
        List<double> rsiList = new(stockData.Count);
        List<double> srcLcList = new(stockData.Count);
        List<double> hcSrcList = new(stockData.Count);
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

            var hcSrc = hc - currentValue;
            hcSrcList.Add(hcSrc);
        }

        var topEma1List = GetMovingAverageList(stockData, maType, length2, srcLcList);
        var topEma2List = GetMovingAverageList(stockData, maType, length3, topEma1List);
        var botEma1List = GetMovingAverageList(stockData, maType, length2, hcSrcList);
        var botEma2List = GetMovingAverageList(stockData, maType, length3, botEma1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var top = topEma2List[i];
            var bot = botEma2List[i];
            var rs = bot != 0 ? MinOrMax(top / bot, 1, 0) : 0;

            var rsi = bot == 0 ? 100 : top == 0 ? 0 : MinOrMax(100 - (100 / (1 + rs)), 100, 0);
            rsiList.Add(rsi);
        }

        var rsiEmaList = GetMovingAverageList(stockData, maType, length3, rsiList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = rsiList[i];
            var rsiEma = rsiEmaList[i];
            var prevRsi = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsiEma = i >= 1 ? rsiEmaList[i - 1] : 0;

            var signal = GetRsiSignal(rsi - rsiEma, prevRsi - prevRsiEma, rsi, prevRsi, 80, 20);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Dsrsi", rsiList },
            { "Signal", rsiEmaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.DoubleSmoothedRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Dominant Cycle Tuned Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateDominantCycleTunedRelativeStrengthIndex(this StockData stockData, int length = 5)
    {
        List<double> aList = new(stockData.Count);
        List<double> bList = new(stockData.Count);
        List<double> rsiList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var v1List = CalculateEhlersAdaptiveCyberCycle(stockData, length).OutputValues["Period"];

        for (var i = 0; i < stockData.Count; i++)
        {
            var v1 = v1List[i];
            var p = v1 != 0 ? 1 / v1 : 0.07;
            var price = inputList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var aChg = price > prevPrice ? Math.Abs(price - prevPrice) : 0;
            var bChg = price < prevPrice ? Math.Abs(price - prevPrice) : 0;
            var prevRsi1 = i >= 1 ? rsiList[i - 1] : 0;
            var prevRsi2 = i >= 2 ? rsiList[i - 2] : 0;

            var prevA = i >= 1 ? aList[i - 1] : aChg;
            var a = (p * aChg) + ((1 - p) * prevA);
            aList.Add(a);

            var prevB = i >= 1 ? bList[i - 1] : bChg;
            var b = (p * bChg) + ((1 - p) * prevB);
            bList.Add(b);

            var r = b != 0 ? a / b : 0;
            var rsi = b == 0 ? 100 : a == 0 ? 0 : MinOrMax(100 - (100 / (1 + r)), 100, 0);
            rsiList.Add(rsi);

            var signal = GetRsiSignal(rsi - prevRsi1, prevRsi1 - prevRsi2, rsi, prevRsi1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "DctRsi", rsiList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(rsiList);
        stockData.IndicatorName = IndicatorName.DominantCycleTunedRelativeStrengthIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the CCT Stochastic Relative Strength Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <param name="length5"></param>
    /// <param name="smoothLength1"></param>
    /// <param name="smoothLength2"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateCCTStochRSI(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length1 = 5, int length2 = 8, int length3 = 13, int length4 = 14, int length5 = 21, int smoothLength1 = 3, int smoothLength2 = 8,
        int signalLength = 9)
    {
        List<double> type1List = new(stockData.Count);
        List<double> type2List = new(stockData.Count);
        List<double> type3List = new(stockData.Count);
        List<double> type4List = new(stockData.Count);
        List<double> type5List = new(stockData.Count);
        List<double> type6List = new(stockData.Count);
        List<double> tempRsi21List = new(stockData.Count);
        List<double> tempRsi14List = new(stockData.Count);
        List<double> tempRsi13List = new(stockData.Count);
        List<double> tempRsi5List = new(stockData.Count);
        List<double> tempRsi8List = new(stockData.Count);
        List<double> typeCustomList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingMinMax rsi21Len2Window = new(length2);
        RollingMinMax rsi21Len3Window = new(length3);
        RollingMinMax rsi21Len5Window = new(length5);
        RollingMinMax rsi14Len4Window = new(length4);
        RollingMinMax rsi5Len1Window = new(length1);
        RollingMinMax rsi13Len3Window = new(length3);
        RollingMinMax rsi8Len2Window = new(length2);

        var rsi5List = CalculateRelativeStrengthIndex(stockData, maType, length: length1).CustomValuesList;
        var rsi8List = CalculateRelativeStrengthIndex(stockData, maType, length: length2).CustomValuesList;
        var rsi13List = CalculateRelativeStrengthIndex(stockData, maType, length: length3).CustomValuesList;
        var rsi14List = CalculateRelativeStrengthIndex(stockData, maType, length: length4).CustomValuesList;
        var rsi21List = CalculateRelativeStrengthIndex(stockData, maType, length: length5).CustomValuesList;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentRSI5 = rsi5List[i];
            tempRsi5List.Add(currentRSI5);
            rsi5Len1Window.Add(currentRSI5);

            var currentRSI8 = rsi8List[i];
            tempRsi8List.Add(currentRSI8);
            rsi8Len2Window.Add(currentRSI8);

            var currentRSI13 = rsi13List[i];
            tempRsi13List.Add(currentRSI13);
            rsi13Len3Window.Add(currentRSI13);

            var currentRSI14 = rsi14List[i];
            tempRsi14List.Add(currentRSI14);
            rsi14Len4Window.Add(currentRSI14);

            var currentRSI21 = rsi21List[i];
            tempRsi21List.Add(currentRSI21);
            rsi21Len2Window.Add(currentRSI21);
            rsi21Len3Window.Add(currentRSI21);
            rsi21Len5Window.Add(currentRSI21);

            var lowestX1 = rsi21Len2Window.Min;
            var lowestZ1 = rsi21Len3Window.Min;
            var highestY1 = rsi21Len3Window.Max;
            var lowestX2 = rsi21Len5Window.Min;
            var lowestZ2 = rsi21Len5Window.Min;
            var highestY2 = rsi21Len5Window.Max;
            var lowestX3 = rsi14Len4Window.Min;
            var lowestZ3 = rsi14Len4Window.Min;
            var highestY3 = rsi14Len4Window.Max;
            var lowestX4 = rsi21Len3Window.Min;
            var lowestZ4 = rsi21Len3Window.Min;
            var highestY4 = rsi21Len2Window.Max;
            var lowestX5 = rsi5Len1Window.Min;
            var lowestZ5 = rsi5Len1Window.Min;
            var highestY5 = rsi5Len1Window.Max;
            var lowestX6 = rsi13Len3Window.Min;
            var lowestZ6 = rsi13Len3Window.Min;
            var highestY6 = rsi13Len3Window.Max;
            var lowestCustom = rsi8Len2Window.Min;
            var highestCustom = rsi8Len2Window.Max;

            var stochRSI1 = highestY1 - lowestZ1 != 0 ? (currentRSI21 - lowestX1) / (highestY1 - lowestZ1) * 100 : 0;
            type1List.Add(stochRSI1);

            var stochRSI2 = highestY2 - lowestZ2 != 0 ? (currentRSI21 - lowestX2) / (highestY2 - lowestZ2) * 100 : 0;
            type2List.Add(stochRSI2);

            var stochRSI3 = highestY3 - lowestZ3 != 0 ? (currentRSI14 - lowestX3) / (highestY3 - lowestZ3) * 100 : 0;
            type3List.Add(stochRSI3);

            var stochRSI4 = highestY4 - lowestZ4 != 0 ? (currentRSI21 - lowestX4) / (highestY4 - lowestZ4) * 100 : 0;
            type4List.Add(stochRSI4);

            var stochRSI5 = highestY5 - lowestZ5 != 0 ? (currentRSI5 - lowestX5) / (highestY5 - lowestZ5) * 100 : 0;
            type5List.Add(stochRSI5);

            var stochRSI6 = highestY6 - lowestZ6 != 0 ? (currentRSI13 - lowestX6) / (highestY6 - lowestZ6) * 100 : 0;
            type6List.Add(stochRSI6);

            var stochCustom = highestCustom - lowestCustom != 0 ? (currentRSI8 - lowestCustom) / (highestCustom - lowestCustom) * 100 : 0;
            typeCustomList.Add(stochCustom);
        }

        var rsiEma4List = GetMovingAverageList(stockData, maType, smoothLength2, type4List);
        var rsiEma5List = GetMovingAverageList(stockData, maType, smoothLength1, type5List);
        var rsiEma6List = GetMovingAverageList(stockData, maType, smoothLength1, type6List);
        var rsiEmaCustomList = GetMovingAverageList(stockData, maType, smoothLength1, typeCustomList);
        var rsiSignalList = GetMovingAverageList(stockData, maType, signalLength, type1List);
        for (var i = 0; i < stockData.Count; i++)
        {
            var rsi = type1List[i];
            var prevRsi = i >= 1 ? type1List[i - 1] : 0;
            var rsiSignal = rsiSignalList[i];
            var prevRsiSignal = i >= 1 ? rsiSignalList[i - 1] : 0;

            var signal = GetRsiSignal(rsi - rsiSignal, prevRsi - prevRsiSignal, rsi, prevRsi, 90, 10);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Type1", type1List },
            { "Type2", type2List },
            { "Type3", type3List },
            { "Type4", rsiEma4List },
            { "Type5", rsiEma5List },
            { "Type6", rsiEma6List },
            { "TypeCustom", rsiEmaCustomList },
            { "Signal", rsiSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(type1List);
        stockData.IndicatorName = IndicatorName.CCTStochRelativeStrengthIndex;

        return stockData;
    }

}

