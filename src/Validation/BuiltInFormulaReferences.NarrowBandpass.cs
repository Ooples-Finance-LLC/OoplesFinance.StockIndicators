using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> NarrowBandpassOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        NarrowBandpassOutputs(bars, Math.Max(2, Integer(indicator.CreateOptions(), "Length", 50)));
    internal static IReadOnlyDictionary<string, double[]> NarrowBandpassOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(2, length);
        var zero = new ReferenceFraction(0);
        var weights = Enumerable.Range(0, length).Select(j =>
        {
            if (j == 0 || j == length - 1 || 2L * j == length) return zero;
            var x = (double)j / (length - 1);
            var c1 = ReferenceFraction.FromDouble(Math.Cos(2 * Math.PI * x));
            var c2 = ReferenceFraction.FromDouble(Math.Cos(4 * Math.PI * x));
            var window = ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(.42) - c1 / new ReferenceFraction(2) + ReferenceFraction.FromDouble(.08) * c2).ToDouble());
            return ReferenceFraction.FromDouble((window * ReferenceFraction.FromDouble(Math.Sin(2 * Math.PI * j / length))).ToDouble());
        }).ToArray();
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = zero;
            for (var j = 0; j < Math.Min(i + 1, length); j++) sum += weights[j] * ReferenceFraction.FromDouble(bars[i - j].Close);
            values[i] = sum.ToDouble();
        }
        return Outputs(("Nbpf", values));
    }
}
