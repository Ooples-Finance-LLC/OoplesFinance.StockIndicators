using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> KurtosisOutputs(IReadOnlyList<Bar> bars, int first = 3, int second = 1, int fast = 3, int slow = 65)
    {
        first = Math.Max(1, first); second = Math.Max(1, second);
        var differences = bars.Select((b, i) => i < first ? new ReferenceFraction(0) : RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - first].Close))).ToArray();
        var changes = differences.Select((v, i) => i < second ? new ReferenceFraction(0) : RoundRocBankStage(v - differences[i - second])).ToArray();
        var line = SmoothRocBankStage(changes, Math.Max(1, slow), 3); var signal = SmoothRocBankStage(line, Math.Max(1, fast), 2);
        return new() { { "Fk", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
