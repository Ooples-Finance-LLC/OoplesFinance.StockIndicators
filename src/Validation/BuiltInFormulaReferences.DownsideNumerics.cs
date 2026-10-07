using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedDownside(IReadOnlyList<Bar> bars, int length, double target = 0) => bars.Select((_, i) =>
    {
        if (i < length) return 0d;
        var threshold = ReferenceFraction.FromDouble(target);
        var differences = Enumerable.Range(i - length + 1, length).Select(j =>
            (bars[j - 1].Close > 0
                ? ReferenceFraction.FromDouble(bars[j].Close) / ReferenceFraction.FromDouble(bars[j - 1].Close) - new ReferenceFraction(1)
                : new ReferenceFraction(0)) - threshold).Where(v => v.Sign < 0).ToArray();
        return differences.Length == 0 ? 0 :
            (differences.Aggregate(new ReferenceFraction(0), (sum, v) => sum + v * v) /
                new ReferenceFraction(differences.Length)).SqrtToDouble();
    }).ToArray();
}
