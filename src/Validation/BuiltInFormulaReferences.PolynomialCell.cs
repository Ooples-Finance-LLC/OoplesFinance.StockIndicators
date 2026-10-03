using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PolynomialCellOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var length = Math.Max(1, Integer(indicator.CreateOptions(), "Length", 100));
        // The binary64 endpoint integrals define the coefficient grid; the dot
        // product below is independently evaluated as an exact rational sum.
        var endpoints = Enumerable.Range(0, Math.Min(length, bars.Count) + 1).Select(j =>
        {
            var x = j / (double)length;
            return x * x + Enumerable.Range(1, 3).Sum(k => Math.Sin(k * x * Math.PI) / k);
        }).ToArray();
        var weights = Enumerable.Range(0, endpoints.Length - 1).Select(j => length == 1 ? new ReferenceFraction(1) : length == 2 ? new ReferenceFraction(j == 0 ? 11 : 1) / new ReferenceFraction(12) : ReferenceFraction.FromDouble(endpoints[j + 1] - endpoints[j])).ToArray();
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var total = new ReferenceFraction(0);
            for (var lag = 0; lag < length && lag <= i; lag++) total += weights[lag] * ReferenceFraction.FromDouble(bars[i - lag].Close);
            output[i] = total.ToDouble();
        }
        return Outputs(("Plsma", output));
    }
}
