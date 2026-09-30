
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Fisher Transform Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <param name="stochLength"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFisherTransformStochasticOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length = 2, int stochLength = 30, int smoothLength = 5)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData);
        var result = FisherStochasticWindow.Calculate(stockData, input, maType, length, stochLength, smoothLength, false);
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ftso", result.Line.ToList() } });
        stockData.SetSignals(result.Trades.ToList()); stockData.SetCustomValues(result.Line.ToList());
        stockData.IndicatorName = IndicatorName.FisherTransformStochasticOscillator; return stockData;
    }


    /// <summary>
    /// Calculates the Fast and Slow Stochastic Oscillator
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="length4"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateFastandSlowStochasticOscillator(this StockData stockData,
        MovingAvgType maType = MovingAvgType.WeightedMovingAverage, int length1 = 3, int length2 = 6, int length3 = 9, int length4 = 9)
    {
        var (input, high, low, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        var components = external ? FastSlowCompositeWindow.Components(stockData, input, high, low, false, maType, length1, length2, length3, length4) : null; using var window = new FastSlowCompositeWindow(false, maType, length1, length2, length3, length4, external);
        var line = new List<double>(input.Count); var signalLine = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(high[i], low[i], input[i], true, components?[0][i], components?[1][i], components?[2][i]); line.Add(point.Line); signalLine.Add(point.SignalLine); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Fsst", line }, { "Signal", signalLine } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.FastandSlowStochasticOscillator; return stockData;
    }

}

