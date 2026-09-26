
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the adaptive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveTrailingStop(this StockData stockData, int length = 100, double factor = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new PoweredKaufmanWindow(length, factor);
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true).Stop; line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, (i > 0 ? input[i - 1] : 0) - (i > 0 ? line[i - 1] : 0)));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ts", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AdaptiveTrailingStop;
        return stockData;
    }


    /// <summary>
    /// Calculates the adaptive autonomous recursive trailing stop.
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="length">The length.</param>
    /// <param name="gamma">The gamma.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAdaptiveAutonomousRecursiveTrailingStop(this StockData stockData, int length = 14, double gamma = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new AdaptiveAutonomousWindow(length, gamma);
        List<double> line = new(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(input[i], true).Stop; line.Add(value);
            signals?.Add(GetCompareSignal(input[i] - value, (i > 0 ? input[i - 1] : 0) - (i > 0 ? line[i - 1] : 0)));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ts", line } });
        stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.AdaptiveAutonomousRecursiveTrailingStop;
        return stockData;
    }


    /// <summary>
    /// Calculates the Chandelier Exit
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length">The length.</param>
    /// <param name="mult">The mult.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandelierExit(this StockData stockData, MovingAvgType maType = MovingAvgType.WildersSmoothingMethod, int length = 22, 
        double mult = 3)
    {
        List<double> chandelierExitLongList = new(stockData.Count);
        List<double> chandelierExitShortList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, highList, lowList, _, _) = GetInputValuesList(stockData);
        var (highestList, lowestList) = GetMaxAndMinValuesList(highList, lowList, length);

        var atrList = CalculateAverageTrueRange(stockData, maType, length).ChainedValues;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentAvgTrueRange = atrList[i];
            var highestHigh = highestList[i];
            var lowestLow = lowestList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;

            var prevExitLong = GetLastOrDefault(chandelierExitLongList);
            var chandelierExitLong = highestHigh - (currentAvgTrueRange * mult);
            chandelierExitLongList.Add(chandelierExitLong);

            var prevExitShort = GetLastOrDefault(chandelierExitShortList);
            var chandelierExitShort = lowestLow + (currentAvgTrueRange * mult);
            chandelierExitShortList.Add(chandelierExitShort);

            var signal = GetBullishBearishSignal(currentValue - chandelierExitLong, prevValue - prevExitLong, currentValue - chandelierExitShort, prevValue - prevExitShort);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "ExitLong", chandelierExitLongList },
            { "ExitShort", chandelierExitShortList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = IndicatorName.ChandelierExit;

        return stockData;
    }


    /// <summary>
    /// Calculates the Average True Range Trailing Stops
    /// </summary>
    /// <param name="stockData">The stock data.</param>
    /// <param name="maType">Type of the ma.</param>
    /// <param name="length1">The length1.</param>
    /// <param name="length2">The length2.</param>
    /// <param name="factor">The factor.</param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateAverageTrueRangeTrailingStops(this StockData stockData, MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, 
        int length1 = 63, int length2 = 21, double factor = 3)
    {
        List<double> atrtsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var atrList = CalculateAverageTrueRange(stockData, maType, length2).ChainedValues;
        var emaList = GetMovingAverageList(stockData, maType, length1, inputList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var currentEma = emaList[i];
            var currentAtr = atrList[i];
            var prevAtrts = i >= 1 ? GetLastOrDefault(atrtsList) : currentValue;
            var upTrend = currentValue > currentEma;
            var dnTrend = currentValue <= currentEma;

            var atrts = upTrend ? Math.Max(currentValue - (factor * currentAtr), prevAtrts) : dnTrend ?
                Math.Min(currentValue + (factor * currentAtr), prevAtrts) : prevAtrts;
            atrtsList.Add(atrts);

            var signal = GetCompareSignal(currentValue - atrts, prevValue - prevAtrts);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Atrts", atrtsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(atrtsList);
        stockData.IndicatorName = IndicatorName.AverageTrueRangeTrailingStops;

        return stockData;
    }

}

