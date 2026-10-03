using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> ErgodicCandleOutputs(IReadOnlyList<Bar> bars, int first, int second, int kind = 3)
    {
        ReferenceFraction[] Smooth(ReferenceFraction[] input) => SmoothRocBankStage(SmoothRocBankStage(input, Math.Max(1, first), kind), Math.Max(1, second), kind);
        var body = Smooth(bars.Select(b => RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open))).ToArray());
        var range = Smooth(bars.Select(b => RoundRocBankStage(ReferenceFraction.FromDouble(b.High) - ReferenceFraction.FromDouble(b.Low))).ToArray());
        var line = body.Select((v, i) => range[i].Sign == 0 ? new ReferenceFraction(0) : RoundRocBankStage(v * new ReferenceFraction(100) / range[i])).ToArray();
        var signal = SmoothRocBankStage(line, Math.Max(1, second), kind);
        return Outputs(("Eco", line.Select(v => v.ToDouble()).ToArray()), ("Signal", signal.Select(v => v.ToDouble()).ToArray()));
    }
}
