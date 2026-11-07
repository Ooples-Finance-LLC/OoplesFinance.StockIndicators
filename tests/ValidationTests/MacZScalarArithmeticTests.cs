using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MacZScalarArithmeticTests
{
    private static (BigInteger N, int E, BigInteger D) Parts(MacZWindow.Number value)
    {
        var type = typeof(MacZWindow.Number);
        object Read(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
        var denominator = (BigInteger)Read("_denominator");
        return ((BigInteger)Read("_coefficient"), (int)Read("_exponent"), denominator.IsZero ? BigInteger.One : denominator);
    }

    // Independent legacy multiply-first reduction also verifies the internal
    // sign convention and zero-divisor behavior, which a fraction normalizes.
    private static void EqualLegacy(MacZWindow.Number value, long scalar, bool divide)
    {
        var (n, e, d) = Parts(value);
        if (divide) d *= scalar; else n *= scalar;
        if (d.IsZero) d = BigInteger.One;
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        var actual = divide ? value.Divide(scalar) : value.Times(scalar);
        Assert.Equal((n / gcd, e, d / gcd), Parts(actual));
        var expected = new ReferenceFraction(n) / new ReferenceFraction(d);
        expected = e >= 0 ? expected * new ReferenceFraction(BigInteger.One << e)
            : expected / new ReferenceFraction(BigInteger.One << -e);
        var fraction = actual.ToFraction();
        Assert.Equal(expected.Components, (fraction.Numerator, fraction.Denominator));
    }

    [Fact]
    public void ScalarsPreserveExactValuesAcrossSignsExtremesAndLargeFractions()
    {
        long[] scalars = [0, 1, -1, 2, -3, 15, -75, long.MinValue, long.MaxValue];
        foreach (var bits in new[] { 0, 53, 1074, 8192 })
        foreach (var sign in new[] { -1, 0, 1 })
        {
            var value = MacZWindow.Number.Integer(sign * ((BigInteger.One << bits) + 15))
                .Divide(MacZWindow.Number.Integer(3 * (BigInteger.One << bits) + 7));
            foreach (var scalar in scalars)
            {
                EqualLegacy(value, scalar, false);
                EqualLegacy(value, scalar, true);
                EqualLegacy(value.Divide(-15), scalar, false);
                EqualLegacy(value.Divide(-15), scalar, true);
            }
        }
        foreach (var value in new[] { default(MacZWindow.Number), MacZWindow.Number.Of(double.Epsilon), MacZWindow.Number.Of(-double.MaxValue) })
        foreach (var scalar in scalars)
        {
            EqualLegacy(value, scalar, false);
            EqualLegacy(value, scalar, true);
        }
    }

    [Fact]
    public void RecursiveScalarCancellationPreservesUnroundedState()
    {
        var actual = MacZWindow.Number.Of(0.1);
        var expected = ReferenceFraction.FromDouble(0.1);
        for (var i = 1; i <= 256; i++)
        {
            EqualLegacy(actual, 19, false);
            actual = actual.Times(19) + MacZWindow.Number.Integer(i % 7 - 3);
            EqualLegacy(actual, 23, true);
            actual = actual.Divide(23);
            expected = (expected * new ReferenceFraction(19) + new ReferenceFraction(i % 7 - 3)) / new ReferenceFraction(23);
            var fraction = actual.ToFraction();
            Assert.Equal(expected.Components, (fraction.Numerator, fraction.Denominator));
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected.ToDouble()), BitConverter.DoubleToInt64Bits(actual.Publish()));
        }
    }
}
