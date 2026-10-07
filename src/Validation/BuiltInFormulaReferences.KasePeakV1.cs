using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> KasePeakV1Outputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return KasePeakV1Outputs(bars, Math.Max(1, Integer(options, "Length", 30)),
            Math.Max(1, Integer(options, "SmoothLength", 3)), (indicator as IIndicator)?.Source is not null);
    }
    internal static IReadOnlyDictionary<string, double[]> KasePeakV1Outputs(IReadOnlyList<Bar> bars, int length, int smoothLength, bool selected = false)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var peak = KasePeakStages(bars, length, smoothLength, selected);
        var mean = SmoothRocBankStage(peak, length, 1, Round);
        var levels = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var first = Math.Max(0, i - length + 1); var count = i - first + 1;
            var center = R(0);
            for (var j = first; j <= i; j++) center += peak[j];
            center /= new ReferenceFraction(count);
            var variance = R(0);
            for (var j = first; j <= i; j++) { var delta = peak[j] - center; variance += delta * delta; }
            variance /= new ReferenceFraction(count);
            var factor = R(1); var scale = R(Math.Pow(2, 256));
            while (double.IsInfinity(variance.SqrtToDouble())) { variance /= scale * scale; factor *= scale; }
            var deviation = R(variance.SqrtToDouble()) * factor;
            var radius = Round(R(1.33) * deviation);
            var upper = Round(mean[i] + radius); if (upper.CompareTo(R(2.08)) < 0) upper = R(2.08);
            var lower = Round(mean[i] - radius); if (lower.CompareTo(R(-1.92)) > 0) lower = R(-1.92);
            var previous = i == 0 ? R(0) : peak[i - 1];
            levels[i] = previous.Sign >= 0 && peak[i].Sign > 0 ? upper.ToDouble()
                : previous.Sign <= 0 && peak[i].Sign < 0 ? lower.ToDouble() : 0;
        }
        return Outputs(("Kpo", levels), ("Pk", peak.Select(v => v.ToDouble()).ToArray()));
    }
}
