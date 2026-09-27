
namespace OoplesFinance.StockIndicators;

public static partial class Calculations
{
    /// <summary>
    /// Calculates the Demarker
    /// </summary>
    /// <param name="stockData"></param>
    /// <param name="maType"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    [Obsolete("Use the v2.0 Builder API (StockIndicatorBuilder) instead. See MIGRATION.md for details.")]
    public static StockData CalculateDemarker(this StockData stockData, MovingAvgType maType = MovingAvgType.SimpleMovingAverage, int length = 20)
    {
        length = Math.Max(1, length); var (_, high, low, _, _) = GetInputValuesList(stockData); List<double> values = new(high.Count); var signals = CreateSignalsList(stockData);
        if (Builder.Compute.ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType))
        {
            var ups = new List<double>(high.Count); var downs = new List<double>(high.Count);
            for (var i = 0; i < high.Count; i++) { var up = DemarkerWindow.PositiveDifference(high[i], i > 0 ? high[i - 1] : high[i]); var down = DemarkerWindow.PositiveDifference(i > 0 ? low[i - 1] : low[i], low[i]); ups.Add(up.Mantissa * (up.Doubled ? 2 : 1)); downs.Add(down.Mantissa * (down.Doubled ? 2 : 1)); }
            var upMean = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(ups), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, ups);
            var downMean = Builder.Compute.ComponentAverage.Take(Compatibility.SpanCompat.AsReadOnlySpan(downs), length)?.ToList() ?? GetMovingAverageList(stockData, maType, length, downs);
            for (var i = 0; i < high.Count; i++) values.Add(DemarkerWindow.Ratio(new StrengthValue(upMean[i]), new StrengthValue(downMean[i])));
        }
        else { using var window = new DemarkerWindow(maType, length, high.Count); for (var i = 0; i < high.Count; i++) values.Add(window.Next(high[i], low[i], true)); }
        for (var i = 0; i < high.Count; i++) { var previous = i > 0 ? values[i - 1] : 0; var older = i > 1 ? values[i - 2] : 0; signals?.Add(GetRsiSignal(values[i] - previous, previous - older, values[i], previous, 70, 30)); }
        stockData.SetOutputValues(() => new Dictionary<string, List<double>> { { "Dm", values } }); stockData.SetSignals(signals); stockData.SetCustomValues(values); stockData.IndicatorName = IndicatorName.Demarker; return stockData;
    }
}

