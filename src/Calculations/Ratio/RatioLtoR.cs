
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Martin Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateMartinRatio(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage,
        int length = 30, double bmk = 0.02)
    {
        List<double> martinList = new(stockData.Count);
        List<double> benchList = new(stockData.Count);
        List<double> retList = new(stockData.Count);

        List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData);

        double barMin = 60 * 24;
        double minPerYr = 60 * 24 * 30 * 12;
        var barsPerYr = minPerYr / barMin;

        for (var i = 0; i < stockData.Count; i++)
        {
            var currentValue = inputList[i];
            var prevValue = i >= length ? inputList[i - length] : 0;

            var bench = Pow(1 + bmk, length / barsPerYr) - 1;
            benchList.Add(bench);

            var ret = prevValue != 0 ? 100 * ((currentValue / prevValue) - 1 - bench) : 0;
            retList.Add(ret);
        }

        var retSmaList = GetMovingAverageList(stockData, maType, length, retList);
        stockData.SetCustomValues(inputList);
        var ulcerIndexList = CalculateUlcerIndex(stockData, length).ChainedValues;
        for (var i = 0; i < stockData.Count; i++)
        {
            var ulcerIndex = ulcerIndexList[i];
            var retSma = retSmaList[i];

            var prevMartin = GetLastOrDefault(martinList);
            var martin = ulcerIndex != 0 ? retSma / ulcerIndex : 0;
            martinList.Add(martin);

            var signal = GetCompareSignal(martin - 2, prevMartin - 2);
            signalsList?.Add(signal);
        }

        stockData.SetOutputValues(() => new Dictionary<string, List<double>>{
            { "Mr", martinList }
        });
        stockData.SetSignals(signalsList);
        stockData.SetCustomValues(martinList);
        stockData.IndicatorName = IndicatorName.MartinRatio;

        return stockData;
    }


    /// <summary>
    /// Calculates the Omega Ratio
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="length"></param>
    /// <param name="bmk"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateOmegaRatio(this StockData stockData, int length = 30, double bmk = 0.05)
    {
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new TargetReturnWindow(length, bmk, false);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value - 5, previous - 5));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Or", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
        stockData.IndicatorName = IndicatorName.OmegaRatio;
        return stockData;
    }

}

