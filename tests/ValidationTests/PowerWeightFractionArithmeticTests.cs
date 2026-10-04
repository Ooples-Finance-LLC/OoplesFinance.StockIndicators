using System.Numerics;
using Fraction = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PowerWeightFractionArithmeticTests
{
    // The oracle deliberately multiplies first and reduces afterwards. It does
    // not call any Fraction arithmetic or reuse the cancellation paths.
    private static void Equal(Fraction actual, BigInteger numerator, BigInteger denominator)
    {
        if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
        var divisor = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Assert.Equal((numerator / divisor, denominator / divisor), (actual.Numerator, actual.Denominator));
    }
    private static Fraction Make(BigInteger n, BigInteger d)
    {
        var value = new Fraction(n, d);
        Equal(value, n, d); return value;
    }
    [Fact]
    public void OperatorsMatchMultiplyThenReduceAcrossSignsSharedFactorsAndExponents()
    {
        var inputs = new List<(BigInteger N, BigInteger D)> { (0, 1), (1, 1), (-1, 1), (2, 3), (9, 10), (-7, 45), (11, -75) };
        foreach (var shift in new[] { 53, 1074, 2048, 8192 })
        {
            var power = BigInteger.One << shift;
            inputs.Add((power + 1, 3 * power)); inputs.Add((-(power - 1), 5 * power));
            inputs.Add((15 * power, 7)); inputs.Add((7, 15 * power));
        }
        foreach (var x in inputs) foreach (var y in inputs)
        {
            var a = Make(x.N, x.D); var b = Make(y.N, y.D);
            Equal(a + b, x.N * y.D + y.N * x.D, x.D * y.D);
            Equal(a - b, x.N * y.D - y.N * x.D, x.D * y.D);
            Equal(a * b, x.N * y.N, x.D * y.D);
            if (!y.N.IsZero) Equal(a / b, x.N * y.D, x.D * y.N);
            Equal(a.Abs(), BigInteger.Abs(x.N), BigInteger.Abs(x.D));
        }
    }
    [Fact]
    public void RepeatedCancellationAndRollingExpiryKeepCanonicalFractions()
    {
        var actual = (Fraction)0; BigInteger n = 0, d = 1;
        var queue = new Queue<(BigInteger N, BigInteger D)>();
        for (var i = 1; i <= 80; i++)
        {
            BigInteger vn = i % 2 == 0 ? i : -i; BigInteger vd = 3 * i + 1;
            actual += Make(vn, vd); n = n * vd + vn * d; d *= vd;
            queue.Enqueue((vn, vd));
            if (queue.Count > 7) { var old = queue.Dequeue(); actual -= Make(old.N, old.D); n = n * old.D - old.N * d; d *= old.D; }
            var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(n), d); n /= gcd; d /= gcd;
            Equal(actual, n, d); Equal(actual - actual, 0, 1);
            if (!n.IsZero) Equal(actual / actual, 1, 1);
        }
    }
    [Fact]
    public void DefaultZeroAndIntegerPowersRetainReducedValues()
    {
        Fraction zero = default; var value = Make(-7, 15);
        Equal(zero + value, -7, 15); Equal(zero * value, 0, 1); Equal(zero / value, 0, 1);
        Equal(-value, 7, 15); Equal(zero.Abs(), 0, 1);
        for (var power = 0; power <= 12; power++)
            Equal(value.Pow(power), BigInteger.Pow(-7, power), BigInteger.Pow(15, power));
    }
    [Fact]
    public void DivisionByZeroStillRejectsEveryNumerator()
    {
        foreach (var n in new[] { -1, 0, 1 })
            Assert.Throws<DivideByZeroException>(() => ((Fraction)n) / (Fraction)0);
    }
}
