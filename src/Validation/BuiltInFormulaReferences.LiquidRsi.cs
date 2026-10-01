using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget LiquidRsiBudget = new(0, 4e-15, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> LiquidRsiOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => Outputs(("Lrsi", LiquidRsiValues(bars, Integer(indicator.CreateOptions(), "Length", 14))));
    internal static double[] LiquidRsiValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var values = new double[bars.Count];
        // Exact raw observation products on a common binary grid, with a shared
        // rational denominator for the two weight sums. No production rounding.
        BigInteger Units(double value)
        {
            var bits = BitConverter.DoubleToInt64Bits(value); var exponent = (int)((bits >> 52) & 2047);
            var mantissa = new BigInteger(bits & ((1L << 52) - 1));
            if (exponent != 0) mantissa = (mantissa + (BigInteger.One << 52)) << (exponent - 1);
            return bits < 0 ? -mantissa : mantissa;
        }
        var upward = BigInteger.Zero; var total = BigInteger.Zero; var divisor = BigInteger.One; var deferred = 0;
        for (var i = 1; i < bars.Count; i++)
        {
            var price = Units(bars[i].Close) - Units(bars[i - 1].Close); var volume = Units(bars[i].Volume) - Units(bars[i - 1].Volume);
            var mass = BigInteger.Abs(price * volume); var rises = price.Sign > 0 && volume.Sign > 0;
            if (length == 1) { values[i] = rises ? 100 : 0; continue; }
            if (mass.IsZero)
            {
                if (!total.IsZero) deferred++;
                values[i] = values[i - 1]; continue;
            }
            if (deferred > 0)
            {
                var retained = BigInteger.Pow(new BigInteger(length - 1), deferred);
                upward *= retained; total *= retained; divisor *= BigInteger.Pow(new BigInteger(length), deferred); deferred = 0;
            }
            upward = upward * (length - 1) + (rises ? mass * divisor : BigInteger.Zero);
            total = total * (length - 1) + mass * divisor; divisor *= length;
            var common = BigInteger.GreatestCommonDivisor(BigInteger.GreatestCommonDivisor(upward, total), divisor);
            if (common > BigInteger.One) { upward /= common; total /= common; divisor /= common; }
            values[i] = upward.IsZero ? 0 : upward == total ? 100 : (new ReferenceFraction(100 * upward) / new ReferenceFraction(total)).ToDouble();
        }
        return values;
    }
}
