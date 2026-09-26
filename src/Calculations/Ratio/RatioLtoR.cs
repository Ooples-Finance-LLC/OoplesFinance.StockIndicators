
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
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new MartinWindow(maType, length, bmk);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            signals?.Add(GetCompareSignal(value - 2, previous - 2)); output.Add(value);
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Mr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
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

