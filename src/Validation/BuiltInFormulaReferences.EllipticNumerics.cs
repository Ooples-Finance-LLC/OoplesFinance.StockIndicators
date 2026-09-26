using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> EllipticNumericalOutputs(IReadOnlyList<Bar> bars, bool modified)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var result = new ReferenceFraction[bars.Count];
        var b = new[] { .13785, .0007, .13785 }.Select(ReferenceFraction.FromDouble).ToArray();
        var a = new[] { 1.2103, -.4867 }.Select(ReferenceFraction.FromDouble).ToArray();
        var zero = new ReferenceFraction(0); var seed = modified && bars.Count > 0 ? prices[0] : zero;
        for (var i = 0; i < bars.Count; i++)
        {
            ReferenceFraction Price(int index) => index >= 0 ? prices[index] : seed;
            var sum = zero;
            for (var j = 0; j < b.Length; j++)
            {
                var drive = modified ? (new ReferenceFraction(2) * Price(i - j) - Price(i - j - 1)).RoundExtendedBinary64() : Price(i - j);
                sum += b[j] * drive;
            }
            for (var j = 0; j < a.Length; j++) sum += a[j] * (i > j ? result[i - j - 1] : modified && i > 0 ? result[0] : seed);
            result[i] = sum.RoundExtendedBinary64();
        }
        return Outputs(("Emoef", result.Select(v => v.ToDouble()).ToArray()));
    }
}
