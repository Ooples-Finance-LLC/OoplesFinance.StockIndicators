using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> ReallySimpleOutputs(IReadOnlyList<Bar> bars, int length = 21, int smoothLength = 10, int kind = 3)
    {
        length = Math.Max(1, length); smoothLength = Math.Max(1, smoothLength);
        var averages = SmoothRocBankStage(bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(), length, kind);
        var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var delta = RoundRocBankStage(ReferenceFraction.FromDouble(b.Low) - averages[i]);
            line[i] = b.Close == 0 ? new ReferenceFraction(0) : RoundRocBankStage(RoundRocBankStage(delta / ReferenceFraction.FromDouble(b.Close)) * new ReferenceFraction(100));
        }
        var signal = SmoothRocBankStage(line, smoothLength, kind);
        return new() { { "Rsi", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
