
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
        length = Math.Max(1, length); List<double> vrList = new(stockData.Count); List<Signal>? signalsList = CreateSignalsList(stockData);
        var (inputList, _, _, _, _) = GetInputValuesList(stockData); var window = new VolatilityRatioWindow(length);
        var emaList = Builder.Compute.ComponentAverage.Take(inputList.ToArray(), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, inputList);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(stockData.HighPrices[i], stockData.LowPrices[i], inputList[i], true); vrList.Add(value);
            signalsList?.Add(GetVolatilitySignal(inputList[i] - emaList[i], (i == 0 ? inputList[i] : inputList[i - 1]) - (i == 0 ? 0 : emaList[i - 1]), value, breakoutLevel));
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
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new SortinoWindow(maType, length, bmk);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value - 2, previous - 2));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Sr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
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
        List<double> output = new(stockData.Count);
        List<Signal>? signals = CreateSignalsList(stockData);
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        using var window = new ReturnScoreWindow(maType, length, bmk, false);
        for (var i = 0; i < stockData.Count; i++)
        {
            var value = window.Next(input[i], true);
            var previous = i == 0 ? 0 : output[i - 1];
            output.Add(value); signals?.Add(GetCompareSignal(value - 2, previous - 2));
        }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Sr", output } });
        stockData.SetSignals(signals); stockData.SetCustomValues(output);
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

