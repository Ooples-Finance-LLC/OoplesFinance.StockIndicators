using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static int TwoPoleVariant(IndicatorName name) => name switch
    {
        IndicatorName.Ehlers2PoleButterworthFilterV1 => 0, IndicatorName.Ehlers2PoleButterworthFilterV2 => 1,
        IndicatorName.Ehlers2PoleSuperSmootherFilterV1 => 2, IndicatorName.Ehlers2PoleSuperSmootherFilterV2 => 3,
        _ => -1
    };
    internal static IReadOnlyDictionary<string, double[]> TwoPoleOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var variant = TwoPoleVariant(indicator.BatchName); var length = Math.Max(2, Integer(indicator.CreateOptions(), "Length", 10));
        var angle = Math.Sqrt(2) * Math.PI / length;
        var radius = ReferenceFraction.FromDouble(Math.Exp(-angle));
        var cosine = ReferenceFraction.FromDouble(Math.Cos(variant == 0 ? Math.Sqrt(2) * 1.25 * Math.PI / length : angle));
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0);
        // Expand the conjugate-pole denominator, retaining the exact dyadic coefficients.
        var denominator1 = zero - two * radius * cosine; var denominator2 = radius * radius;
        var gain = (one - radius) * (one - radius) + two * radius * (one - cosine);
        var taps = variant == 1 ? new[] { 1, 2, 1 } : variant == 3 ? new[] { 1, 1 } : new[] { 1 };
        var mass = new ReferenceFraction(taps.Sum()); var startup = variant is 1 or 2 ? 3 : 0;
        var states = new ReferenceFraction[bars.Count]; var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var drive = zero;
            for (var lag = 0; lag < taps.Length && lag <= i; lag++) drive += ReferenceFraction.FromDouble(bars[i - lag].Close) * new ReferenceFraction(taps[lag]);
            var next = gain * drive / mass - denominator1 * (i > 0 ? states[i - 1] : zero) - denominator2 * (i > 1 ? states[i - 2] : zero);
            states[i] = i < startup ? ReferenceFraction.FromDouble(bars[i].Close) : next.RoundExtendedBinary64();
            output[i] = states[i].ToDouble();
        }
        return Outputs((variant < 2 ? "E2bf" : variant == 2 ? "Essf" : "E2ssf", output));
    }
}
