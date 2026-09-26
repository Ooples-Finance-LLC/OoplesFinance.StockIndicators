using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> HendersonOutputs(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var m = Math.Max(2, Math.Min(530, (length - 1) / 2));
        var weights = Enumerable.Range(0, length).Select(j => {
            var n = new ReferenceFraction(j - m); var square = n * n;
            var second = new ReferenceFraction((long)(m + 2) * (m + 2));
            return (new ReferenceFraction((long)(m + 1) * (m + 1)) - square) * (second - square)
                * (new ReferenceFraction((long)(m + 3) * (m + 3)) - square)
                * (new ReferenceFraction(3) * second - new ReferenceFraction(11) * square - new ReferenceFraction(16));
        }).ToArray();
        var denominator = weights.Aggregate(new ReferenceFraction(0), (sum, weight) => sum + weight);
        var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var j = 0; j < length && j <= i; j++) sum += weights[j] * ReferenceFraction.FromDouble(bars[i - j].Close);
            values[i] = denominator.Sign == 0 ? 0 : (sum / denominator).ToDouble();
        }
        return Outputs(("Hwma", values));
    }
}
