using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MacZProductArithmeticTests
{
    private static (BigInteger N, int E, BigInteger D) Parts(MacZWindow.Number value)
    {
        var type = typeof(MacZWindow.Number);
        object Read(string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value)!;
        var denominator = (BigInteger)Read("_denominator");
        return ((BigInteger)Read("_coefficient"), (int)Read("_exponent"), denominator.IsZero ? BigInteger.One : denominator);
    }

    private static MacZWindow.Number Create(BigInteger numerator, int exponent, BigInteger denominator) =>
        (MacZWindow.Number)Activator.CreateInstance(typeof(MacZWindow.Number),
            BindingFlags.Instance | BindingFlags.NonPublic, null,
            new object[] { numerator, exponent, denominator, false }, null)!;

    private static void EqualLegacy(MacZWindow.Number a, MacZWindow.Number b)
    {
        var left = Parts(a);
        var right = Parts(b);
        var numerator = left.N * right.N;
        var denominator = left.D * right.D;
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Assert.Equal((numerator / gcd, checked(left.E + right.E), denominator / gcd), Parts(a * b));
    }

    [Fact]
    public void EqualDenominatorsMatchMultiplyThenReduceAcrossSignsAndScales()
    {
        foreach (var denominator in new[] { new BigInteger(3), new BigInteger(-3),
            new BigInteger(5), BigInteger.Pow(21, 64), -BigInteger.Pow(21, 64),
            BigInteger.Pow(21, 512), BigInteger.One << 256 })
        {
            var values = (from numerator in new[] { -BigInteger.One, BigInteger.Zero, new BigInteger(2), denominator + 1 }
                          from exponent in new[] { -1074, -7, 0, 17 }
                          select Create(numerator, exponent, denominator)).ToArray();
            var equalDenominatorPairs = 0;
            foreach (var a in values)
            foreach (var b in values)
            {
                if (Parts(a).D == Parts(b).D) equalDenominatorPairs++;
                EqualLegacy(a, b);
            }
            Assert.True(equalDenominatorPairs > 0);
        }
    }

    [Fact]
    public void RecursiveSquaresKeepTheCompleteUnroundedRepresentation()
    {
        var value = MacZWindow.Number.Of(.1);
        for (var i = 0; i < 512; i++)
        {
            value = (value.Times(19) + MacZWindow.Number.Of(i % 13 - 6d).Times(2)).Divide(21);
            EqualLegacy(value, value);
            EqualLegacy(value, value.Times(-1));
        }
    }

    [Fact]
    public void UnequalDenominatorsStillCancelCrossFactors()
    {
        var values = new[] { default(MacZWindow.Number), MacZWindow.Number.Of(double.Epsilon),
            MacZWindow.Number.Of(-double.MaxValue), Create(3, -17, 5), Create(5, 19, 3),
            Create(21, 0, 41), Create(41, 0, -21) };
        foreach (var a in values)
        foreach (var b in values)
            EqualLegacy(a, b);
    }

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, -1)]
    public void EqualDenominatorExponentOverflowStillThrows(int first, int second)
    {
        var a = Create(1, first, 3);
        var b = Create(2, second, 3);
        Assert.Throws<OverflowException>(() => { _ = a * b; });
    }
}
