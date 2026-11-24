using System.Numerics;
using System.Reflection;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RandomWalkArithmeticTests
{
    private static readonly Type Fraction = typeof(RandomWalkWindow).GetNestedType("Fraction", BindingFlags.NonPublic)!;
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Create(BigInteger n, BigInteger d) => Fraction.GetConstructor(Instance, null,
        new[] { typeof(BigInteger), typeof(BigInteger) }, null)!.Invoke(new object[] { n, d });
    private static (BigInteger N, BigInteger D) Parts(object value)
    {
        var n = (BigInteger)Fraction.GetField("_numerator", Instance)!.GetValue(value)!;
        var d = (BigInteger)Fraction.GetField("_denominator", Instance)!.GetValue(value)!;
        return (n, d.IsZero ? BigInteger.One : d);
    }
    private static (BigInteger N, BigInteger D) Reduce(BigInteger n, BigInteger d)
    {
        if (d.Sign < 0) { n = -n; d = -d; }
        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d);
        return (n / gcd, d / gcd);
    }
    private static object[] Values()
    {
        BigInteger[] values = { 0, 1, -1, 2, -3, 15, long.MinValue, long.MaxValue,
            BigInteger.One << 1024, (BigInteger.One << 997) - 1 };
        BigInteger[] denominators = { 1, -1, 2, 15, (BigInteger.One << 1024) - 1 };
        return values.SelectMany(n => denominators.Select(d => Create(n, d)))
            .Append(Activator.CreateInstance(Fraction)!).ToArray();
    }

    [Fact]
    public void BinaryOperationsMatchIndependentMultiplyFirstRationalArithmetic()
    {
        var values = Values();
        foreach (var a in values)
        foreach (var b in values)
        {
            var (an, ad) = Parts(a); var (bn, bd) = Parts(b);
            foreach (var subtract in new[] { false, true })
            {
                var operation = Fraction.GetMethod(subtract ? "op_Subtraction" : "op_Addition", BindingFlags.Public | BindingFlags.Static)!;
                var actual = operation.Invoke(null, new[] { a, b })!;
                Assert.Equal(Reduce(an * bd + (subtract ? -bn : bn) * ad, ad * bd), Parts(actual));
            }
            var divide = Fraction.GetMethod("op_Division", BindingFlags.Public | BindingFlags.Static)!;
            if (bn.IsZero)
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => divide.Invoke(null, new[] { a, b })).InnerException);
            else
                Assert.Equal(Reduce(an * bd, ad * bn), Parts(divide.Invoke(null, new[] { a, b })!));
        }
    }

    [Fact]
    public void ScalarOperationsPreserveReducedRepresentationAndZeroDivision()
    {
        foreach (var value in Values())
        foreach (var scalar in new[] { long.MinValue, long.MaxValue, -15L, -1L, 0L, 1L, 2L, 15L })
        {
            var (n, d) = Parts(value);
            var times = Fraction.GetMethod("Times", Instance)!;
            Assert.Equal(Reduce(n * scalar, d), Parts(times.Invoke(value, new object[] { scalar })!));
            var divide = Fraction.GetMethod("Divide", Instance)!;
            if (scalar == 0)
                Assert.IsType<DivideByZeroException>(Assert.Throws<TargetInvocationException>(() => divide.Invoke(value, new object[] { scalar })).InnerException);
            else
                Assert.Equal(Reduce(n, d * scalar), Parts(divide.Invoke(value, new object[] { scalar })!));
        }
    }
}
