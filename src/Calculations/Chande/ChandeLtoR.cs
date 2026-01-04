
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Quick Stick
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateChandeQuickStick(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 14)
    {
        List<double> openCloseList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentOpen = openList[i];
            var currentClose = inputList[i];

            var openClose = currentClose - currentOpen;
            openCloseList.Add(openClose);
        }

        var smaList = GetMovingAverageList(stockData, maType, length, openCloseList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var sma = smaList[i];
            var prevSma = i >= 1 ? smaList[i - 1] : 0;

            var signal = GetCompareSignal(sma, prevSma);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cqs", smaList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(smaList);
        stockData.IndicatorName = IndicatorName.ChandeQuickStick;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Filter
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="filter"></param>
    /// <returns></returns>
    public static StockData CalculateChandeMomentumOscillatorFilter(this StockData stockData, MovingAvgType maType = MovingAvgType.WeightedMovingAverage,
        int length = 9, double filter = 3)
    {
        List<double> cmoList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        var diffSumWindow = new RollingSum();
        var absDiffSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var diff = MinPastValues(i, 1, currentValue - prevValue);
            var absDiff = Math.Abs(diff);
            if (absDiff > filter)
            {
                diff = 0; absDiff = 0;
            }
            diffList.Add(diff);
            absDiffList.Add(absDiff);
            diffSumWindow.Add(diff);
            absDiffSumWindow.Add(absDiff);

            var diffSum = diffSumWindow.Sum(length);
            var absDiffSum = absDiffSumWindow.Sum(length);

            var cmo = absDiffSum != 0 ? MinOrMax(100 * diffSum / absDiffSum, 100, -100) : 0;
            cmoList.Add(cmo);
        }

        var cmoSignalList = GetMovingAverageList(stockData, maType, length, cmoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var cmo = cmoList[i];
            var cmoSignal = cmoSignalList[i];
            var prevCmo = i >= 1 ? cmoList[i - 1] : 0;
            var prevCmoSignal = i >= 1 ? cmoSignalList[i - 1] : 0;

            var signal = GetRsiSignal(cmo - cmoSignal, prevCmo - prevCmoSignal, cmo, prevCmo, 70, -70);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmof", cmoList },
            { "Signal", cmoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorFilter;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Absolute
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateChandeMomentumOscillatorAbsolute(this StockData stockData, int length = 9)
    {
        List<double> cmoAbsList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        var absDiffSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var priorValue = i >= length ? inputList[i - length] : 0;
            var prevCmoAbs1 = i >= 1 ? cmoAbsList[i - 1] : 0;
            var prevCmoAbs2 = i >= 2 ? cmoAbsList[i - 2] : 0;

            var absDiff = Math.Abs(MinPastValues(i, 1, currentValue - prevValue));
            absDiffList.Add(absDiff);
            absDiffSumWindow.Add(absDiff);

            var num = Math.Abs(100 * MinPastValues(i, length, currentValue - priorValue));
            var denom = absDiffSumWindow.Sum(length);

            var cmoAbs = denom != 0 ? MinOrMax(num / denom, 100, 0) : 0;
            cmoAbsList.Add(cmoAbs);

            var signal = GetRsiSignal(cmoAbs - prevCmoAbs1, prevCmoAbs1 - prevCmoAbs2, cmoAbs, prevCmoAbs1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoa", cmoAbsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoAbsList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAbsolute;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateChandeMomentumOscillatorAverage(this StockData stockData, int length1 = 5, int length2 = 10, int length3 = 20)
    {
        List<double> cmoAvgList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        var diffSumWindow = new RollingSum();
        var absDiffSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var prevCmoAvg1 = i >= 1 ? cmoAvgList[i - 1] : 0;
            var prevCmoAvg2 = i >= 2 ? cmoAvgList[i - 2] : 0;

            var diff = currentPrice - prevPrice;
            diffList.Add(diff);
            diffSumWindow.Add(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.Add(absDiff);
            absDiffSumWindow.Add(absDiff);

            var diffSum1 = diffSumWindow.Sum(length1);
            var absSum1 = absDiffSumWindow.Sum(length1);
            var diffSum2 = diffSumWindow.Sum(length2);
            var absSum2 = absDiffSumWindow.Sum(length2);
            var diffSum3 = diffSumWindow.Sum(length3);
            var absSum3 = absDiffSumWindow.Sum(length3);
            var temp1 = absSum1 != 0 ? MinOrMax(diffSum1 / absSum1, 1, -1) : 0;
            var temp2 = absSum2 != 0 ? MinOrMax(diffSum2 / absSum2, 1, -1) : 0;
            var temp3 = absSum3 != 0 ? MinOrMax(diffSum3 / absSum3, 1, -1) : 0;

            var cmoAvg = 100 * ((temp1 + temp2 + temp3) / 3);
            cmoAvgList.Add(cmoAvg);

            var signal = GetRsiSignal(cmoAvg - prevCmoAvg1, prevCmoAvg1 - prevCmoAvg2, cmoAvg, prevCmoAvg1, 50, -50);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoa", cmoAvgList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoAvgList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Absolute Average
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateChandeMomentumOscillatorAbsoluteAverage(this StockData stockData, int length1 = 5, int length2 = 10, int length3 = 20)
    {
        List<double> cmoAbsAvgList = new(stockData.Count);
        List<double> diffList = new(stockData.Count);
        List<double> absDiffList = new(stockData.Count);
        var diffSumWindow = new RollingSum();
        var absDiffSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentPrice = inputList[i];
            var prevPrice = i >= 1 ? inputList[i - 1] : 0;
            var prevCmoAbsAvg1 = i >= 1 ? cmoAbsAvgList[i - 1] : 0;
            var prevCmoAbsAvg2 = i >= 2 ? cmoAbsAvgList[i - 2] : 0;

            var diff = currentPrice - prevPrice;
            diffList.Add(diff);
            diffSumWindow.Add(diff);

            var absDiff = Math.Abs(diff);
            absDiffList.Add(absDiff);
            absDiffSumWindow.Add(absDiff);

            var diffSum1 = diffSumWindow.Sum(length1);
            var absSum1 = absDiffSumWindow.Sum(length1);
            var diffSum2 = diffSumWindow.Sum(length2);
            var absSum2 = absDiffSumWindow.Sum(length2);
            var diffSum3 = diffSumWindow.Sum(length3);
            var absSum3 = absDiffSumWindow.Sum(length3);
            var temp1 = absSum1 != 0 ? MinOrMax(diffSum1 / absSum1, 1, -1) : 0;
            var temp2 = absSum2 != 0 ? MinOrMax(diffSum2 / absSum2, 1, -1) : 0;
            var temp3 = absSum3 != 0 ? MinOrMax(diffSum3 / absSum3, 1, -1) : 0;

            var cmoAbsAvg = Math.Abs(100 * ((temp1 + temp2 + temp3) / 3));
            cmoAbsAvgList.Add(cmoAbsAvg);

            var signal = GetRsiSignal(cmoAbsAvg - prevCmoAbsAvg1, prevCmoAbsAvg1 - prevCmoAbsAvg2, cmoAbsAvg, prevCmoAbsAvg1, 70, 30);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoaa", cmoAbsAvgList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoAbsAvgList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAbsoluteAverage;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator Average Disparity Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <returns></returns>
    public static StockData CalculateChandeMomentumOscillatorAverageDisparityIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length1 = 200, int length2 = 50, int length3 = 20)
    {
        List<double> avgDisparityIndexList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var firstEmaList = GetMovingAverageList(stockData, maType, length1, inputList);
        var secondEmaList = GetMovingAverageList(stockData, maType, length2, inputList);
        var thirdEmaList = GetMovingAverageList(stockData, maType, length3, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var firstEma = firstEmaList[i];
            var secondEma = secondEmaList[i];
            var thirdEma = thirdEmaList[i];
            var firstDisparityIndex = currentValue != 0 ? (currentValue - firstEma) / currentValue * 100 : 0;
            var secondDisparityIndex = currentValue != 0 ? (currentValue - secondEma) / currentValue * 100 : 0;
            var thirdDisparityIndex = currentValue != 0 ? (currentValue - thirdEma) / currentValue * 100 : 0;

            var prevAvgDisparityIndex = GetLastOrDefault(avgDisparityIndexList);
            var avgDisparityIndex = (firstDisparityIndex + secondDisparityIndex + thirdDisparityIndex) / 3;
            avgDisparityIndexList.Add(avgDisparityIndex);

            var signal = GetCompareSignal(avgDisparityIndex, prevAvgDisparityIndex);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmoadi", avgDisparityIndexList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(avgDisparityIndexList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillatorAverageDisparityIndex;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Momentum Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="signalLength"></param>
    /// <returns></returns>
    public static StockData CalculateChandeMomentumOscillator(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, int signalLength = 3)
    {
        List<double> cmoList = new(stockData.Count);
        List<double> cmoPosChgList = new(stockData.Count);
        List<double> cmoNegChgList = new(stockData.Count);
        var cmoPosSumWindow = new RollingSum();
        var cmoNegSumWindow = new RollingSum();
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var diff = MinPastValues(i, 1, currentValue - prevValue);

            var negChg = i >= 1 && diff < 0 ? Math.Abs(diff) : 0;
            cmoNegChgList.Add(negChg);
            cmoNegSumWindow.Add(negChg);

            var posChg = i >= 1 && diff > 0 ? diff : 0;
            cmoPosChgList.Add(posChg);
            cmoPosSumWindow.Add(posChg);

            var negSum = cmoNegSumWindow.Sum(length);
            var posSum = cmoPosSumWindow.Sum(length);

            var cmo = posSum + negSum != 0 ? MinOrMax((posSum - negSum) / (posSum + negSum) * 100, 100, -100) : 0;
            cmoList.Add(cmo);
        }

        var cmoSignalList = GetMovingAverageList(stockData, maType, signalLength, cmoList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var cmo = cmoList[i];
            var cmoSignal = cmoSignalList[i];
            var prevCmo = i >= 1 ? cmoList[i - 1] : 0;
            var prevCmoSignal = i >= 1 ? cmoSignalList[i - 1] : 0;

            var signal = GetRsiSignal(cmo - cmoSignal, prevCmo - prevCmoSignal, cmo, prevCmo, 50, -50);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cmo", cmoList },
            { "Signal", cmoSignalList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(cmoList);
        stockData.IndicatorName = IndicatorName.ChandeMomentumOscillator;

        return stockData;
    }
}

