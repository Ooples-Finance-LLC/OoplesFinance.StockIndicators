using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> ModifiedObvOutputs(IReadOnlyList<Bar> bars, int length1 = 7, int length2 = 10, int kind = 3)
    {
        var total = new ReferenceFraction(0); var raw = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? 0 : bars[i - 1].Close;
            var direction = bars[i].Close > previous ? 1 : bars[i].Close < previous ? -1 : 0;
            total += new ReferenceFraction(direction) * ReferenceFraction.FromDouble(bars[i].Volume);
            raw[i] = RoundRocBankStage(total);
        }
        var line = SmoothRocBankStage(raw, Math.Max(1, length1), kind); var signal = SmoothRocBankStage(line, Math.Max(1, length2), kind);
        return new() { { "Obvm", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
