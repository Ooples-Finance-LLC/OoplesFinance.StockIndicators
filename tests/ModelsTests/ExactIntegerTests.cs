using OoplesFinance.StockIndicators.Helpers;
using Big = System.Numerics.BigInteger;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// <see cref="ExactInteger"/> replaces <see cref="Big"/> under every exact window, so each operation must return the
/// integer <see cref="Big"/> returns. The operands mimic the windows': 2^-1074 fixed-point units of binary64 values
/// (huge, with long runs of trailing zeros), their products and sums, small counts, and negatives of each.
/// </summary>
public sealed class ExactIntegerTests
{
    private static Big Units(double value)
    {
        var bits = BitConverter.DoubleToInt64Bits(value);
        var exponent = (int)((bits >> 52) & 2047);
        var integer = new Big(bits & ((1L << 52) - 1));
        if (exponent != 0) integer = (integer + (Big.One << 52)) << (exponent - 1);
        return bits < 0 ? -integer : integer;
    }

    private static List<Big> Operands(int seed)
    {
        var random = new Random(seed);
        var values = new List<Big> { Big.Zero, Big.One, Big.MinusOne, 2, -2, 3, 1L << 40, long.MaxValue, long.MinValue, (Big)long.MaxValue + 1 };
        for (var i = 0; i < 40; i++)
        {
            var price = Units(100 * Math.Exp(random.NextDouble() - 0.5));
            var tiny = Units(random.NextDouble() * 1e-300);
            values.Add(price);
            values.Add(-price);
            values.Add(tiny);
            values.Add(price * Units(random.NextDouble()));
            values.Add(price + tiny);
            values.Add((Big)random.NextInt64(-1_000_000, 1_000_000));
            values.Add(((Big)random.NextInt64() << random.Next(0, 200)) + random.Next(0, 3));
        }
        return values;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void Every_operation_returns_the_BigInteger_result(int seed)
    {
        var values = Operands(seed);
        foreach (var a in values)
        {
            ExactInteger x = a;
            Assert.Equal(a, (Big)x);
            Assert.Equal(a.Sign, x.Sign);
            Assert.Equal(a.IsZero, x.IsZero);
            Assert.Equal(a.IsOne, x.IsOne);
            Assert.Equal(a.IsEven, x.IsEven);
            Assert.Equal(a.GetBitLength(), x.GetBitLength());
            Assert.Equal((double)a, (double)x);
            Assert.Equal(a.ToByteArray(), x.ToByteArray());
            Assert.Equal(a.ToString(), x.ToString());
            Assert.Equal(-a, (Big)(-x));
            Assert.Equal(Big.Abs(a), (Big)ExactInteger.Abs(x));
            Assert.Equal(Big.Pow(a, 3), (Big)ExactInteger.Pow(x, 3));
            Assert.Equal(a.GetHashCode(), x.GetHashCode());
            if (!a.IsZero) Assert.Equal(Big.TrailingZeroCount(a), (Big)ExactInteger.TrailingZeroCount(x));
            foreach (var shift in new[] { 0, 1, 7, 52, 64, 1074, 1100 })
            {
                Assert.Equal(a << shift, (Big)(x << shift));
                Assert.Equal(a >> shift, (Big)(x >> shift));
                Assert.Equal(a << -shift, (Big)(x << -shift));
            }
            if (a >= long.MinValue && a <= long.MaxValue) Assert.Equal((long)a, (long)x);
            else Assert.Throws<OverflowException>(() => (long)x);

            foreach (var b in values)
            {
                ExactInteger y = b;
                Assert.Equal(a + b, (Big)(x + y));
                Assert.Equal(a - b, (Big)(x - y));
                Assert.Equal(a * b, (Big)(x * y));
                Assert.Equal(a.CompareTo(b), Math.Sign(x.CompareTo(y)));
                Assert.Equal(a == b, x == y);
                Assert.Equal(a < b, x < y);
                Assert.Equal(a >= b, x >= y);
                Assert.Equal(Big.Max(a, b), (Big)ExactInteger.Max(x, y));
                Assert.Equal(Big.Min(a, b), (Big)ExactInteger.Min(x, y));
                Assert.Equal(Big.GreatestCommonDivisor(a, b), (Big)ExactInteger.GreatestCommonDivisor(x, y));
                Assert.Equal(a & b, (Big)(x & y));
                if (b.IsZero)
                {
                    Assert.Throws<DivideByZeroException>(() => x / y);
                    continue;
                }
                Assert.Equal(a / b, (Big)(x / y));
                Assert.Equal(a % b, (Big)(x % y));
                var quotient = Big.DivRem(a, b, out var remainder);
                Assert.Equal(quotient, (Big)ExactInteger.DivRem(x, y, out var exactRemainder));
                Assert.Equal(remainder, (Big)exactRemainder);
            }
        }
    }

    [Fact]
    public void Equal_values_have_one_representation()
    {
        ExactInteger shifted = (Big)3 << 1074;
        ExactInteger built = Units(3) >> 1;
        Assert.Equal((Big)3 << 1074, (Big)shifted);
        Assert.True(((ExactInteger)((Big)6 << 1073)).Equals(shifted));
        Assert.Equal(built * 2, ExactInteger.Parse((Units(3)).ToString()));
        Assert.Equal(default(ExactInteger), (ExactInteger)Big.One - (ExactInteger)Big.One);
    }
}
