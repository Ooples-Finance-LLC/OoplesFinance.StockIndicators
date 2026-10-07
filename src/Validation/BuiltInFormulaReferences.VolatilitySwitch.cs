using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] VolatilitySwitchOutputs(IReadOnlyList<Bar> bars, int length = 14, int kind = 2)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0);
        var returns = bars.Select((b, i) => {
            if (i == 0) return zero; var current = ReferenceFraction.FromDouble(b.Close); var previous = ReferenceFraction.FromDouble(bars[i - 1].Close);
            var midpoint = ReferenceFraction.FromDouble(((current + previous) / new ReferenceFraction(2)).ToDouble());
            return midpoint.Sign == 0 ? zero : RoundRocBankStage(RoundRocBankStage(current - previous) / midpoint);
        }).ToArray();
        var deviation = new ReferenceFraction[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            deviation[i] = zero; if (i + 1L < length) continue;
            var window = Window(returns, i, length).ToArray(); var n = new ReferenceFraction(length); var mean = window.Aggregate(zero, (a, b) => a + b) / n;
            var variance = window.Aggregate(zero, (sum, v) => sum + (v - mean) * (v - mean)) / n;
            for (var shift = 0; ; shift += 32)
            {
                var root = (variance / new ReferenceFraction(BigInteger.One << (2 * shift))).SqrtToDouble();
                if (double.IsInfinity(root)) continue; deviation[i] = ReferenceFraction.FromDouble(root) * new ReferenceFraction(BigInteger.One << shift); break;
            }
        }
        return SmoothRocBankStage(deviation, length, kind).Select(v => v.ToDouble()).ToArray();
    }
}
