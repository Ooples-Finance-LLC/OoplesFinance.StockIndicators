using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> SmiErgodicOutputs(IReadOnlyList<Bar> bars, int fast, int slow, int signal, int kind = 3)
    {
        var zero = new ReferenceFraction(0);
        var changes = bars.Select((b, i) => i == 0 ? zero : RoundStrengthStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - 1].Close))).ToArray();
        ReferenceFraction[] Smooth(ReferenceFraction[] input) => SmoothStrengthStage(SmoothStrengthStage(input, Math.Max(1, fast), kind), Math.Max(1, slow), kind);
        var signed = Smooth(changes); var absolute = Smooth(changes.Select(v => v.Abs()).ToArray());
        var line = signed.Select((v, i) => absolute[i].Sign == 0 ? 0 : Math.Max(-100, Math.Min(100, (v * new ReferenceFraction(100) / absolute[i]).ToDouble()))).ToArray();
        var signalLine = SmoothStrengthStage(line.Select(ReferenceFraction.FromDouble).ToArray(), Math.Max(1, signal), kind).Select(v => v.ToDouble()).ToArray();
        return Outputs(("Smi", line), ("Signal", signalLine));
    }
}
