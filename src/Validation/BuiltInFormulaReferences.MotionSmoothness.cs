using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] MotionSmoothnessOutputs(IReadOnlyList<Bar> bars, int length = 50)
    {
        length = Math.Max(1, length); var zero = new ReferenceFraction(0); var n = new ReferenceFraction(length);
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var changes = prices.Select((v, i) => i == 0 ? zero : RoundRocBankStage(v - prices[i - 1])).ToArray();
        ReferenceFraction Deviation(ReferenceFraction[] values, int i)
        {
            var window = Window(values, i, length).ToArray(); var mean = window.Aggregate(zero, (a, b) => a + b) / n;
            var variance = window.Aggregate(zero, (sum, v) => sum + (v - mean) * (v - mean)) / n;
            for (var shift = 0; ; shift += 32)
            {
                var root = (variance / new ReferenceFraction(BigInteger.One << (2 * shift))).SqrtToDouble();
                if (double.IsInfinity(root)) continue; return ReferenceFraction.FromDouble(root) * new ReferenceFraction(BigInteger.One << shift);
            }
        }
        return prices.Select((v, i) => {
            if (i + 1L < length) return 0d; var denominator = Deviation(prices, i);
            return denominator.Sign == 0 ? 0d : (Deviation(changes, i) / denominator).ToDouble();
        }).ToArray();
    }
}
