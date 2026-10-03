using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> SigmaSpikesOutputs(IReadOnlyList<Bar> bars, int length = 20, int kind = 3)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0);
        var returns = bars.Select((b, i) => i == 0 || bars[i - 1].Close == 0 ? zero : RoundRocBankStage(RoundRocBankStage(ReferenceFraction.FromDouble(b.Close) / ReferenceFraction.FromDouble(bars[i - 1].Close)) - new ReferenceFraction(1))).ToArray();
        var deviation = new ReferenceFraction[bars.Count]; var line = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            deviation[i] = zero;
            if (i + 1L >= length)
            {
                var window = Window(returns, i, length).ToArray(); var n = new ReferenceFraction(length); var mean = window.Aggregate(zero, (a, b) => a + b) / n;
                var variance = window.Aggregate(zero, (sum, v) => sum + (v - mean) * (v - mean)) / n;
                for (var shift = 0; ; shift += 32)
                {
                    var root = (variance / new ReferenceFraction(BigInteger.One << (2 * shift))).SqrtToDouble();
                    if (double.IsInfinity(root)) continue; deviation[i] = ReferenceFraction.FromDouble(root) * new ReferenceFraction(BigInteger.One << shift); break;
                }
            }
            line[i] = i == 0 || deviation[i - 1].Sign == 0 ? zero : RoundRocBankStage(returns[i] / deviation[i - 1]);
        }
        var signal = SmoothRocBankStage(line, length, kind);
        return new() { { "Ss", line.Select(v => v.ToDouble()).ToArray() }, { "Signal", signal.Select(v => v.ToDouble()).ToArray() } };
    }
}
