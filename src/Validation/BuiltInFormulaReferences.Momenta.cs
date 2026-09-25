using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> MomentaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 3);
        var lookback = Math.Max(2, Integer(options, "MomentumLength", 2));
        var first = Integer(options, "FirstSmooth", 5);
        var second = Integer(options, "SecondSmooth", 25);
        var numerator = new ReferenceFraction[bars.Count];
        var denominator = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var window = bars.Skip(Math.Max(0, i - lookback + 1)).Take(Math.Min(i + 1, lookback)).Select(b => b.Close).ToArray();
            var low = ReferenceFraction.FromDouble(window.Min());
            numerator[i] = RoundStrengthStage(ReferenceFraction.FromDouble(bars[i].Close) - low);
            denominator[i] = RoundStrengthStage(ReferenceFraction.FromDouble(window.Max()) - low);
        }
        foreach (var period in new[] { first, second })
        {
            numerator = SmoothStrengthStage(numerator, period, kind);
            denominator = SmoothStrengthStage(denominator, period, kind);
        }
        var line = numerator.Select((v, i) => denominator[i].Sign == 0 ? 0 :
            Math.Max(0, Math.Min(100, (v * new ReferenceFraction(100) / denominator[i]).ToDouble()))).ToArray();
        var signal = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), second, kind)
            .Select(v => v.ToDouble()).ToArray();
        return Outputs(("Dsm", line), ("Signal", signal));
    }
}
