using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget MultiDepthBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> MultiDepthOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => MultiDepthValues(Closes(bars), Integer(indicator.CreateOptions(), "Length", 50)).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MultiDepthValues(double[] prices, int length)
    {
        length = Math.Max(1, length);
        // Unit DC gain and current-price alpha seeding make an entirely flat
        // trajectory exact: alpha=price and beta=0 at every depth. Production
        // still executes every bar, including the 4,000-bar settled fixtures.
        if (prices.Length == 0 || !double.IsNaN(prices[0]) && !double.IsInfinity(prices[0]) && prices.All(value => value == prices[0])) // NOSONAR: S1244 - Only an exactly constant trajectory has this closed form.
            return (new Dictionary<string, double[]> { ["Md1Pole"] = prices.ToArray(), ["Md2Pole"] = prices.ToArray(), ["Md3Pole"] = prices.ToArray() }, new Signal[prices.Length]);
        // Coefficients alone are reduced fractions. The trajectory uses integer
        // numerators on known powers of a common denominator, avoiding a GCD
        // after every addition while retaining the exact uncentered recurrence.
        (BigInteger N, BigInteger D) Reduce(BigInteger n, BigInteger d)
        { var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d); return (n / gcd, d / gcd); }
        (BigInteger N, BigInteger D) Add((BigInteger N, BigInteger D) a, (BigInteger N, BigInteger D) b)
            => Reduce(a.N * b.D + b.N * a.D, a.D * b.D);
        (BigInteger N, BigInteger D) Sub((BigInteger N, BigInteger D) a, (BigInteger N, BigInteger D) b)
            => Reduce(a.N * b.D - b.N * a.D, a.D * b.D);
        (BigInteger N, BigInteger D) Mul((BigInteger N, BigInteger D) a, (BigInteger N, BigInteger D) b)
            => Reduce(a.N * b.N, a.D * b.D);
        BigInteger Units(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) throw new ArgumentOutOfRangeException(nameof(value));
            var bits = BitConverter.DoubleToInt64Bits(value); var exponent = (int)((bits >> 52) & 2047);
            var significand = new BigInteger(bits & ((1L << 52) - 1));
            if (exponent != 0) significand += BigInteger.One << 52;
            return (bits < 0 ? -significand : significand) << Math.Max(0, exponent - 1);
        }
        var grid = BigInteger.One << 1074;
        (BigInteger N, BigInteger D) R(double value) => Reduce(Units(value), grid);
        var one = R(1); var two = R(2); var zero = R(0);
        (BigInteger N, BigInteger D) Radius(double angle) => Sub(one, R(2 * Math.Exp(-angle / 2) * Math.Sinh(angle / 2)));
        var angle2 = Math.Sqrt(2) * Math.PI / length;
        var r2 = Radius(angle2); var r3 = Radius(Math.PI / length);
        var sin2 = R(Math.Sin(angle2 / 2)); var sin3 = R(Math.Sin(Math.Sqrt(3) * Math.PI / length / 2));
        var b2 = Mul(Mul(two, r2), Sub(one, Mul(two, Mul(sin2, sin2))));
        var b3 = Mul(Mul(two, r3), Sub(one, Mul(two, Mul(sin3, sin3)))); var c = Mul(r3, r3);
        var feedback = new[]
        {
            new[] { Sub(one, Reduce(2, length + 1L)) },
            new[] { b2, Sub(zero, Mul(r2, r2)) },
            new[] { Add(b3, c), Sub(zero, Mul(c, Add(one, b3))), Mul(c, c) }
        };
        var inputs = prices.Select(Units).ToArray();
        var outputs = new Dictionary<string, double[]>(); var signals = new Signal[prices.Length];
        foreach (var coefficients in feedback)
        {
            var depth = coefficients.Length; var q = BigInteger.One;
            foreach (var coefficient in coefficients) q = q / BigInteger.GreatestCommonDivisor(q, coefficient.D) * coefficient.D;
            var weights = coefficients.Select(v => v.N * (q / v.D)).ToArray();
            var gain = q - weights.Aggregate(BigInteger.Zero, (sum, value) => sum + value);
            var powers = new BigInteger[prices.Length + 2]; powers[0] = BigInteger.One;
            for (var i = 1; i < powers.Length; i++) powers[i] = powers[i - 1] * q;
            var alpha = new BigInteger[prices.Length]; var beta = new BigInteger[prices.Length];
            var line = new double[prices.Length]; var previousMargin = BigInteger.Zero;
            for (var i = 0; i < prices.Length; i++)
            {
                // alpha[i] has denominator q^(i+1), beta[i] q^(i+2).
                // Both numerators express price in exact units of 2^-1074.
                var forcing = inputs[i] * powers[i]; var a = gain * forcing;
                for (var lag = 1; lag <= depth; lag++)
                    a += weights[lag - 1] * (i >= lag ? alpha[i - lag] * powers[lag - 1] : forcing);
                var b = gain * (inputs[i] * powers[i + 1] - a);
                for (var lag = 1; lag <= Math.Min(i, depth); lag++) b += weights[lag - 1] * beta[i - lag] * powers[lag - 1];
                alpha[i] = a; beta[i] = b;
                var numerator = depth * q * a + b; var denominator = depth * powers[i + 2];
                line[i] = (new ReferenceFraction(numerator) / new ReferenceFraction(denominator * grid)).ToDouble();
                if (depth != 2) continue;
                var margin = inputs[i] * denominator - numerator; var change = margin - previousMargin * q;
                signals[i] = margin.Sign > 0 && change.Sign > 0 ? Signal.StrongBuy : margin.Sign < 0 && change.Sign < 0 ? Signal.StrongSell
                    : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
                previousMargin = margin;
            }
            outputs["Md" + depth + "Pole"] = line;
        }
        return (outputs, signals);
    }
}
