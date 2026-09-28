using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> AmDetectorOutputs(IReadOnlyList<Bar> bars, int length1, int length2, int kind)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        var difference = bars.Select(b => (ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open)).Abs()).ToArray();
        var envelope = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var maximum = new ReferenceFraction(0);
            for (var j = Math.Max(0, i - length1 + 1); j <= i; j++) if ((difference[j] - maximum).Sign > 0) maximum = difference[j];
            envelope[i] = RoundRocBankStage(maximum);
        }
        var line = SmoothRocBankStage(envelope, length2, kind); var signal = SmoothRocBankStage(line, length2, kind);
        return new() { { "Eamd", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
