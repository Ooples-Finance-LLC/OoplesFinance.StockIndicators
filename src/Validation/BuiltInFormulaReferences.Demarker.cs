using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> DemarkerOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return DemarkerOutputs(bars, Integer(o, "Length", 20), AverageKind(o, 1)); }
    internal static IReadOnlyDictionary<string, double[]> DemarkerOutputs(IReadOnlyList<Bar> bars, int length, int kind, double[]? externalUp = null, double[]? externalDown = null)
    {
        var zero = new ReferenceFraction(0); var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count]; var values = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var h = i == 0 ? zero : ReferenceFraction.FromDouble(bars[i].High) - ReferenceFraction.FromDouble(bars[i - 1].High); var l = i == 0 ? zero : ReferenceFraction.FromDouble(bars[i - 1].Low) - ReferenceFraction.FromDouble(bars[i].Low);
            up[i] = h.Sign > 0 ? h.RoundExtendedBinary64() : zero; down[i] = l.Sign > 0 ? l.RoundExtendedBinary64() : zero;
        }
        var u = externalUp is null ? SmoothRocBankStage(up, Math.Max(1, length), kind) : externalUp.Select(ReferenceFraction.FromDouble).ToArray(); var d = externalDown is null ? SmoothRocBankStage(down, Math.Max(1, length), kind) : externalDown.Select(ReferenceFraction.FromDouble).ToArray();
        for (var i = 0; i < bars.Count; i++) { var total = u[i] + d[i]; values[i] = total.Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (new ReferenceFraction(100) * u[i] / total).ToDouble())); }
        return Outputs(("Dm", values));
    }
}
