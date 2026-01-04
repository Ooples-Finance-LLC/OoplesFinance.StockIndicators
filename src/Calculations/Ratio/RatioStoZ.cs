
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Upside Potential Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateUpsidePotentialRatio(this StockData stockData, int length = 30, double bmk = 0.05)
    {
        List<double> retList = new(stockData.Count);
        List<double> upsidePotentialList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;
        var ratio = (double)1 / length;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);

            double downSide = 0, upSide = 0;
            for (var j = 0; j < length; j++)
            {
                var iValue = i >= j ? retList[i - j] : 0;
                downSide += iValue < bench ? Pow(iValue - bench, 2) * ratio : 0;
                upSide += iValue > bench ? (iValue - bench) * ratio : 0;
            }

            var prevUpsidePotential = GetLastOrDefault(upsidePotentialList);
            var upsidePotential = downSide >= 0 ? upSide / Sqrt(downSide) : 0;
            upsidePotentialList.Add(upsidePotential);

            var signal = GetCompareSignal(upsidePotential - 5, prevUpsidePotential - 5);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Upr", upsidePotentialList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(upsidePotentialList);
        stockData.IndicatorName = IndicatorName.UpsidePotentialRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Volatility Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="breakoutLevel"></param>
    /// <returns></returns>
    public static StockData CalculateVolatilityRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage,
        int length = 14, double breakoutLevel = 0.5)
    {
        List<double> vrList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length - 1);

        var emaList = GetMovingAverageList(stockData, maType, length, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentEma = emaList[i];
            var prevHighest = i >= 1 ? highestList[i - 1] : 0;
            var prevLowest = i >= 1 ? lowestList[i - 1] : 0;
            var priorValue = i >= length + 1 ? inputList[i - (length + 1)] : 0;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculateTrueRange(currentHigh, currentLow, prevValue);
            var max = priorValue != 0 ? Math.Max(prevHighest, priorValue) : prevHighest;
            var min = priorValue != 0 ? Math.Min(prevLowest, priorValue) : prevLowest;

            var vr = max - min != 0 ? tr / (max - min) : 0;
            vrList.Add(vr);

            var signal = GetVolatilitySignal(currentValue - currentEma, prevValue - prevEma, vr, breakoutLevel);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Vr", vrList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(vrList);
        stockData.IndicatorName = IndicatorName.VolatilityRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Treynor Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="beta"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateTreynorRatio(this StockData stockData, int length = 30, double beta = 1, double bmk = 0.02)
    {
        List<double> treynorList = new(stockData.Count);
        List<double> retList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum retSum = new();
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24, minPerYr = 60 * 24 * 30 * 12, barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 : 0;
            retList.Add(ret);
            retSum.Add(ret);

            var retSma = retSum.Average(length);
            var prevTreynor = GetLastOrDefault(treynorList);
            var treynor = beta != 0 ? (retSma - bench) / beta : 0;
            treynorList.Add(treynor);

            var signal = GetCompareSignal(treynor - 2, prevTreynor - 2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Tr", treynorList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(treynorList);
        stockData.IndicatorName = IndicatorName.TreynorRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Sortino Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateSortinoRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, 
        double bmk = 0.02)
    {
        List<double> sortinoList = new(stockData.Count);
        List<double> retList = new(stockData.Count);
        List<double> deviationSquaredList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double minPerYr = 60 * 24 * 30 * 12, barMin = 60 * 24, barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 - bench : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var ret = retList[i];
            var retSma = retSmaList[i];
            var currentDeviation = Math.Min(ret - retSma, 0);

            var deviationSquared = Pow(currentDeviation, 2);
            deviationSquaredList.Add(deviationSquared);
        }

        var divisionOfSumList = GetMovingAverageList(stockData, maType, length, deviationSquaredList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var divisionOfSum = divisionOfSumList[i];
            var stdDeviation = Sqrt(divisionOfSum);
            var retSma = retSmaList[i];

            var prevSortino = GetLastOrDefault(sortinoList);
            var sortino = stdDeviation != 0 ? retSma / stdDeviation : 0;
            sortinoList.Add(sortino);

            var signal = GetCompareSignal(sortino - 2, prevSortino - 2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sr", sortinoList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sortinoList);
        stockData.IndicatorName = IndicatorName.SortinoRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Sharpe Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    public static StockData CalculateSharpeRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 30, 
        double bmk = 0.02)
    {
        List<double> sharpeList = new(stockData.Count);
        List<double> retList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double minPerYr = 60 * 24 * 30 * 12, barMin = 60 * 24, barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;
            var bench = Pow(1 + bmk, length / barsPerYr) - 1;

            var ret = prevValue != 0 ? (currentValue / prevValue) - 1 - bench : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        stockData.SetCustomValues(retList);
        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).CustomValuesList;
        for (var i = 0; i < stockData.Count; i++)
        {
            var stdDeviation = stdDevList[i];
            var retSma = retSmaList[i];

            var prevSharpe = GetLastOrDefault(sharpeList);
            var sharpe = stdDeviation != 0 ? retSma / stdDeviation : 0;
            sharpeList.Add(sharpe);

            var signal = GetCompareSignal(sharpe - 2, prevSharpe - 2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Sr", sharpeList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(sharpeList);
        stockData.IndicatorName = IndicatorName.SharpeRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Shinohara Intensity Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    public static StockData CalculateShinoharaIntensityRatio(this StockData stockData, int length = 14)
    {
        List<double> tempOpenList = new(stockData.Count);
        List<double> tempLowList = new(stockData.Count);
        List<double> tempHighList = new(stockData.Count);
        List<double> prevCloseList = new(stockData.Count);
        List<double> ratioAList = new(stockData.Count);
        List<double> ratioBList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        RollingSum highSumWindow = new();
        RollingSum lowSumWindow = new();
        RollingSum openSumWindow = new();
        RollingSum prevCloseSumWindow = new();
        var (inputList, highList, lowList, openList, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var high = highList[i];
            tempHighList.Add(high);
            highSumWindow.Add(high);

            var low = lowList[i];
            tempLowList.Add(low);
            lowSumWindow.Add(low);

            var open = openList[i];
            tempOpenList.Add(open);
            openSumWindow.Add(open);

            var prevClose = i >= 1 ? inputList[i - 1] : 0;
            prevCloseList.Add(prevClose);
            prevCloseSumWindow.Add(prevClose);

            var highSum = highSumWindow.Sum(length);
            var lowSum = lowSumWindow.Sum(length);
            var openSum = openSumWindow.Sum(length);
            var prevCloseSum = prevCloseSumWindow.Sum(length);
            var bullA = highSum - openSum;
            var bearA = openSum - lowSum;
            var bullB = highSum - prevCloseSum;
            var bearB = prevCloseSum - lowSum;

            var prevRatioA = GetLastOrDefault(ratioAList);
            var ratioA = bearA != 0 ? bullA / bearA * 100 : 0;
            ratioAList.Add(ratioA);

            var prevRatioB = GetLastOrDefault(ratioBList);
            var ratioB = bearB != 0 ? bullB / bearB * 100 : 0;
            ratioBList.Add(ratioB);

            var signal = GetCompareSignal(ratioA - ratioB, prevRatioA - prevRatioB);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "ARatio", ratioAList },
            { "BRatio", ratioBList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ShinoharaIntensityRatio;

        return stockData;
    }
}

