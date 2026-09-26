using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DirectionalStrengthOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        var kind = AverageKind(options, 3);
        var signed = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var up = ReferenceFraction.FromDouble(bars[i].High) - ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].High);
            var down = ReferenceFraction.FromDouble(i == 0 ? 0 : bars[i - 1].Low) - ReferenceFraction.FromDouble(bars[i].Low);
            up = up.Sign > 0 ? RoundStrengthStage(up) : new ReferenceFraction(0);
            down = down.Sign > 0 ? RoundStrengthStage(down) : new ReferenceFraction(0);
            signed[i] = RoundStrengthStage(up - down);
        }
        var absolute = signed.Select(v => v.Sign < 0 ? new ReferenceFraction(0) - v : v).ToArray();
        foreach (var period in new[] { Integer(options, "Length", 14), 10, 5 })
        {
            signed = SmoothStrengthStage(signed, period, kind);
            absolute = SmoothStrengthStage(absolute, period, kind);
        }
        return Outputs(("Dti", signed.Select((value, i) => absolute[i].Sign == 0 ? 0 :
            Math.Max(-100, Math.Min(100, (value * new ReferenceFraction(100) / absolute[i]).ToDouble()))).ToArray()));
    }
}
