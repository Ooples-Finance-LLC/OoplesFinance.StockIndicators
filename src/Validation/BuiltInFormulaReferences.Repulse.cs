using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> RepulseOutputs(IReadOnlyList<Bar> bars, int length = 5, int kind = 3)
    {
        length = Math.Max(1, length); var powerPeriod = (int)Math.Min(int.MaxValue, (long)length * 5);
        var bull = new ReferenceFraction[bars.Count]; var bear = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var close = ReferenceFraction.FromDouble(bars[i].Close); var triple = RoundRocBankStage(close * new ReferenceFraction(3));
            var window = bars.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(i + 1, length)).ToArray();
            var high = RoundRocBankStage(ReferenceFraction.FromDouble(window.Max(b => b.High)) * new ReferenceFraction(2));
            var low = RoundRocBankStage(ReferenceFraction.FromDouble(window.Min(b => b.Low)) * new ReferenceFraction(2));
            var open = i == 0 ? zero : ReferenceFraction.FromDouble(bars[i - 1].Open);
            bull[i] = close.Sign == 0 ? zero : RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(triple - low) - open) * new ReferenceFraction(100)) / close);
            bear[i] = close.Sign == 0 ? zero : RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(RoundRocBankStage(open + high) - triple) * new ReferenceFraction(100)) / close);
        }
        var bulls = SmoothRocBankStage(bull, powerPeriod, kind); var bears = SmoothRocBankStage(bear, powerPeriod, kind);
        var line = bulls.Select((v, i) => RoundRocBankStage(v - bears[i])).ToArray(); var signal = SmoothRocBankStage(line, length, kind);
        return new() { { "Repulse", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
