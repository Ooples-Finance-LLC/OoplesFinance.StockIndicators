
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Chande Composite Momentum Index
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length1"></param>
    /// <param name="length2"></param>
    /// <param name="length3"></param>
    /// <param name="smoothLength"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateChandeCompositeMomentumIndex(this StockData stockData,
        MovingAvgType maType = MovingAvgType.DoubleExponentialMovingAverage, int length1 = 5, int length2 = 10, int length3 = 20, int smoothLength = 3)
    {
        var (input, _, _, _, _) = GetInputValuesList(stockData); var external = Builder.Compute.ComponentAverage.HasOverrides || !ChandeCompositeWindow.Supports(maType);
        var components = external ? ChandeCompositeWindow.Components(stockData, input, maType, length1, length2, length3, smoothLength) : null;
        using var window = new ChandeCompositeWindow(maType, length1, length2, length3, smoothLength, external);
        var line = new List<double>(input.Count); var signalLine = new List<double>(input.Count); var signals = CreateSignalsList(stockData);
        for (var i = 0; i < input.Count; i++) { var point = window.Next(input[i], true, components?[0][i], components?[1][i], components?[2][i]); line.Add(point.Line); signalLine.Add(point.SignalLine); signals?.Add(point.Signal); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Ccmi", line }, { "Signal", signalLine } }); stockData.SetSignals(signals); stockData.SetCustomValues(line); stockData.IndicatorName = IndicatorName.ChandeCompositeMomentumIndex; return stockData;
    }

}

