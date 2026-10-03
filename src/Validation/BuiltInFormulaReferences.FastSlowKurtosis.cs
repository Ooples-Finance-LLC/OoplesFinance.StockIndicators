using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> FastSlowKurtosisOutputs(IReadOnlyList<Bar> bars, int length = 3, double ratio = .03, int kind = 2)
    {
        length = Math.Max(1, length); var changes = bars.Select((b, i) => i < length ? new ReferenceFraction(0) : RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(bars[i - length].Close))).ToArray();
        var line = new ReferenceFraction[bars.Count]; var previous = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var change = RoundRocBankStage(changes[i] - (i == 0 ? new ReferenceFraction(0) : changes[i - 1]));
            previous = RoundRocBankStage(RoundRocBankStage(change * ReferenceFraction.FromDouble(ratio)) + RoundRocBankStage(previous * ReferenceFraction.FromDouble(1 - ratio)));
            line[i] = previous;
        }
        var signal = SmoothRocBankStage(line, length, kind);
        return new() { { "Fsk", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
