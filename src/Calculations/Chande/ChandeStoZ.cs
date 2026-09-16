
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Volatility Index Dynamic Average Indicator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="alpha1"></param>
    /// <param name="alpha2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeVolatilityIndexDynamicAverageIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, double alpha1 = 0.2, double alpha2 = 0.04) =>
        CalculateVolatilityIndexDynamicAverage(stockData, maType, length, alpha1, alpha2,
            IndicatorName.ChandeVolatilityIndexDynamicAverageIndicator, "Cvida1", "Cvida2");

    /// <summary>
    /// Calculates the Volatility Index Dynamic Average Indicator
    /// </summary>
    /// <remarks>
    /// The same indicator as <see cref="CalculateChandeVolatilityIndexDynamicAverageIndicator"/>, under its own
    /// name and output keys. Streaming has offered this name all along; without a batch method of the same name
    /// the two engines could not be held to the same numbers.
    /// </remarks>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="alpha1"></param>
    /// <param name="alpha2"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateVolatilityIndexDynamicAverageIndicator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.ExponentialMovingAverage, int length = 20, double alpha1 = 0.2, double alpha2 = 0.04) =>
        CalculateVolatilityIndexDynamicAverage(stockData, maType, length, alpha1, alpha2,
            IndicatorName.VolatilityIndexDynamicAverageIndicator, "Vida1", "Vida2");

    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    private static StockData CalculateVolatilityIndexDynamicAverage(StockData stockData, MovingAvgType maType, int length,
        double alpha1, double alpha2, IndicatorName indicatorName, string vidya1Key, string vidya2Key)
    {
        List<double> vidya1List = new(stockData.Count);
        List<double> vidya2List = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        var stdDevList = CalculateStandardDeviationVolatility(stockData, maType, length).ChainedValues;
        var stdDevEmaList = GetMovingAverageList(stockData, maType, length, stdDevList);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var currentStdDev = stdDevList[i];
            var currentStdDevEma = stdDevEmaList[i];
            var prevVidya1 = i >= 1 ? GetLastOrDefault(vidya1List) : currentValue;
            var prevVidya2 = i >= 1 ? GetLastOrDefault(vidya2List) : currentValue;
            var prevValue = i >= 1 ? inputList[i - 1] : 0;
            var ratio = currentStdDevEma != 0 ? currentStdDev / currentStdDevEma : 0;

            var vidya1 = (alpha1 * ratio * currentValue) + ((1 - (alpha1 * ratio)) * prevVidya1);
            vidya1List.Add(vidya1);

            var vidya2 = (alpha2 * ratio * currentValue) + ((1 - (alpha2 * ratio)) * prevVidya2);
            vidya2List.Add(vidya2);

            var signal = GetBullishBearishSignal(currentValue - Math.Max(vidya1, vidya2), prevValue - Math.Max(prevVidya1, prevVidya2),
                currentValue - Math.Min(vidya1, vidya2), prevValue - Math.Min(prevVidya1, prevVidya2));
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { vidya1Key, vidya1List },
            { vidya2Key, vidya2List }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(new List<double>());
        stockData.IndicatorName = indicatorName;

        return stockData;
    }


    /// <summary>
    /// Calculates the Chande Trend Score
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="startLength"></param>
    /// <param name="endLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeTrendScore(this StockData stockData, int startLength = 11, int endLength = 20)
    {
        List<double> tsList = new(stockData.Count);
        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];

            var prevTs = GetLastOrDefault(tsList);
            double ts = 0;
            for (var j = startLength; j <= endLength; j++)
            {
                var prevValue = i >= j ? inputList[i - j] : 0;
                ts += currentValue >= prevValue ? 1 : -1;
            }
            tsList.Add(ts);

            var signal = GetCompareSignal(ts, prevTs);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Cts", tsList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(tsList);
        stockData.IndicatorName = IndicatorName.ChandeTrendScore;

        return stockData;
    }

}

