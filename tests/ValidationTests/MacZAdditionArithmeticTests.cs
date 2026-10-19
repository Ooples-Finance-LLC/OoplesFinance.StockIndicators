using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MacZAdditionArithmeticTests
{
    private static (BigInteger N, int E, BigInteger D) Parts(MacZWindow.Number value)
    {
        var type = typeof(MacZWindow.Number);
        object Read(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
        var denominator = (BigInteger)Read("_denominator");
        return ((BigInteger)Read("_coefficient"), (int)Read("_exponent"), denominator.IsZero ? BigInteger.One : denominator);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdditionAndSubtractionPreserveTheLegacyExactRepresentation(bool subtract)
    {
        var values = new List<MacZWindow.Number> { default };
        foreach (var value in new[] { 0d, 1, -1, 0.1, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue })
            values.Add(MacZWindow.Number.Of(value));
        foreach (var bits in new[] { 0, 53, 1074, 4096 })
        foreach (var sign in new[] { -1, 0, 1 })
        {
            var power = BigInteger.One << bits;
            var value = MacZWindow.Number.Integer(sign * (power + 15))
                .Divide(MacZWindow.Number.Integer(3 * power + 7));
            values.Add(value);
            values.Add(value.Divide(-15));
            // Mixed exponents can cancel only part or all of an even
            // denominator, and must not cancel extra numerator powers of two.
            foreach (var shift in new[] { 1, 63, 1074, 1100 })
                values.Add(MacZWindow.Number.Integer(sign * (power + 15))
                    .Divide(MacZWindow.Number.Integer((3 * power + 7) << shift)));
            values.Add(MacZWindow.Number.Integer(sign * (power + 16))
                .Divide(MacZWindow.Number.Integer(3 * power + 7)));
        }
        foreach (var a in values)
        foreach (var b in values)
        {
            var x = Parts(a); var y = Parts(b);
            if (subtract) y.N = -y.N;
            (BigInteger N, int E, BigInteger D) expected;
            if (x.N.IsZero) expected = y;
            else if (y.N.IsZero) expected = x;
            else
            {
                // Independent multiply-first oracle deliberately uses the full
                // denominator product, including when exponents differ.
                var exponent = Math.Min(x.E, y.E);
                var numerator = (x.N << (x.E - exponent)) * y.D + (y.N << (y.E - exponent)) * x.D;
                var denominator = x.D * y.D;
                var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
                expected = (numerator / divisor, exponent, denominator / divisor);
            }
            Assert.Equal(expected, Parts(subtract ? a - b : a + b));
        }
    }
}
