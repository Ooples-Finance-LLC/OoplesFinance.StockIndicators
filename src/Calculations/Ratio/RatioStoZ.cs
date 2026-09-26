
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateUpsidePotentialRatio(this StockData stockData, int length = 30, double bmk = 0.05)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new TargetReturnWindow(length, bmk, true);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value - 5, previous - 5));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Upr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
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
            // For TrueRange on first bar, use current close to avoid inflated TR
            var prevValue = i >= 1 ? inputList[i - 1] : inputList[i];
            var prevEma = i >= 1 ? emaList[i - 1] : 0;
            var currentHigh = highList[i];
            var currentLow = lowList[i];
            var tr = CalculationsHelper.CalculateTrueRange(currentHigh, currentLow, prevValue);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateTreynorRatio(this StockData stockData, int length = 30, double beta = 1, double bmk = 0.02)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new TargetReturnWindow(length, bmk, false, beta);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value - 2, previous - 2));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Tr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
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
            var currentDeviation = Math.Min(ret, 0);

            var deviationSquared = Pow(currentDeviation, 2);
            deviationSquaredList.Add(deviationSquared);
        }

        // The downside deviation is exactly 0 when no return in the window falls below the target. A running
        // SMA left a residue near 1e-19 there, and the ratio divided by its root came out near 1e7.
        var divisionOfSumList = maType == MovingAvgType.SimpleMovingAverage
            ? GetExactWindowAverageList(deviationSquaredList, length)
            : GetMovingAverageList(stockData, maType, length, deviationSquaredList);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
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
        var stdDevList = GetStandardDeviationList(retList, length);
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
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateShinoharaIntensityRatio(this StockData stockData, int length = 14)
    {
        List<double> ratioA = new(stockData.Count), ratioB = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new ShinoharaWindow(length);
        for (var i = 0; i < stockData.Count; i++)
        {
            var (a, b) = window.Next(stockData.OpenPrices[i], stockData.HighPrices[i], stockData.LowPrices[i], input[i], true);
            var previousA = i == 0 ? 0 : ratioA[i - 1]; var previousB = i == 0 ? 0 : ratioB[i - 1];
            ratioA.Add(a); ratioB.Add(b); signals?.Add(GetCompareSignal(a - b, previousA - previousB));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "ARatio", ratioA }, { "BRatio", ratioB } });
        stockData.SetSignals(signals); stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ShinoharaIntensityRatio;
        return stockData;
    }
}

