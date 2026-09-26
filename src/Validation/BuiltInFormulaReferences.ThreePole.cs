using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static int ThreePoleVariant(IndicatorName name) => name switch
    {
        IndicatorName.Ehlers3PoleButterworthFilterV1 => 0, IndicatorName.Ehlers3PoleButterworthFilterV2 => 1,
        IndicatorName.Ehlers3PoleSuperSmootherFilter => 2,
        _ => -1
    };
    internal static IReadOnlyDictionary<string, double[]> ThreePoleOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var variant = ThreePoleVariant(indicator.BatchName); var length = Math.Max(2, Integer(indicator.CreateOptions(), "Length", 10));
        var radius = ReferenceFraction.FromDouble(Math.Exp(-Math.PI / length));
        var cosine = ReferenceFraction.FromDouble(Math.Cos(1.738 * Math.PI / length));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0);
        // Multiply the conjugate pair and real-pole denominator polynomials.
        var pair = new[] { one, zero - two * radius * cosine, radius * radius };
        var real = new[] { one, zero - radius * radius };
        var denominator = Enumerable.Range(0, 4).Select(k => Enumerable.Range(0, 3)
            .Where(j => k - j >= 0 && k - j < 2).Aggregate(zero, (sum, j) => sum + pair[j] * real[k - j])).ToArray();
        var gain = pair.Aggregate(zero, (sum, term) => sum + term) * (one - radius * radius);
        var taps = variant == 1 ? new[] { 1, 3, 3, 1 } : new[] { 1 };
        var mass = new ReferenceFraction(taps.Sum()); var startup = variant == 0 ? 0 : 4;
        var states = new ReferenceFraction[bars.Count]; var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var drive = zero;
            for (var lag = 0; lag < taps.Length && lag <= i; lag++) drive += ReferenceFraction.FromDouble(bars[i - lag].Close) * new ReferenceFraction(taps[lag]);
            var next = gain * drive / mass;
            for (var lag = 1; lag <= 3 && lag <= i; lag++) next -= denominator[lag] * states[i - lag];
            states[i] = i < startup ? ReferenceFraction.FromDouble(bars[i].Close) : next.RoundExtendedBinary64();
            output[i] = states[i].ToDouble();
        }
        return Outputs((variant < 2 ? "E3bf" : "E3ssf", output));
    }
}
