using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> TimeSeriesForecastOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => TimeSeriesForecastOutputs(bars, Integer(indicator.CreateOptions(), "Length", 500));
    internal static IReadOnlyDictionary<string, double[]> TimeSeriesForecastOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var sum = new ReferenceFraction[bars.Count + 1]; var weighted = new ReferenceFraction[bars.Count + 1]; sum[0] = weighted[0] = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++) { sum[i + 1] = sum[i] + prices[i]; weighted[i + 1] = weighted[i] + new ReferenceFraction(i) * prices[i]; }
        var upper = new double[bars.Count]; var middle = new double[bars.Count]; var lower = new double[bars.Count]; var errorSum = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var start = Math.Max(0, i + 1 - length); var n = new ReferenceFraction(i + 1 - start); var center = new ReferenceFraction((long)start + i) / new ReferenceFraction(2); var total = sum[i + 1] - sum[start]; var covariance = weighted[i + 1] - weighted[start] - center * total; var spread = n * (n * n - new ReferenceFraction(1)) / new ReferenceFraction(12);
            var endpoint = i == start ? prices[i] : total / n + covariance / spread * (new ReferenceFraction(i) - center); var fit = endpoint.RoundExtendedBinary64();
            errorSum += (prices[i] - fit).Abs().RoundExtendedBinary64(); var error = (errorSum / new ReferenceFraction(i + 1)).RoundExtendedBinary64();
            upper[i] = (fit + error).ToDouble(); middle[i] = fit.ToDouble(); lower[i] = (fit - error).ToDouble();
        }
        return Outputs(("UpperBand", upper), ("MiddleBand", middle), ("LowerBand", lower));
    }
}
