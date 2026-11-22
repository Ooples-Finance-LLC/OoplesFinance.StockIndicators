using OoplesFinance.StockIndicators.Helpers;
using Big = System.Numerics.BigInteger;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

/// <summary>
/// <see cref="ExactInteger"/> replaces <see cref="Big"/> under every exact window, so each operation must return the
/// integer <see cref="Big"/> returns. The operands mimic the windows': 2^-1074 fixed-point units of binary64 values
/// (huge, with long runs of trailing zeros), their products and sums, small counts, and negatives of each.
/// </summary>
/// <remarks>
/// Compiled into the net10.0 test project and, through tests/ExactIntegerFrameworks, for net8.0 and net48, which runs
/// the library's net461 build. The oracle side uses only APIs every target has (helpers below stand in for
/// <c>Random.NextInt64</c>, <c>BigInteger.TrailingZeroCount</c> and <c>BigInteger.GetBitLength</c> where missing).
/// </remarks>
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

    private static long NextLong(Random random) => ((long)random.Next() << 33) ^ ((long)random.Next() << 2) ^ random.Next(4)
        ^ (random.Next(2) == 0 ? 0 : long.MinValue);

    private static long NextLong(Random random, long min, long max) => min + (long)((ulong)NextLong(random) % (ulong)(max - min));

    private static long BitLength(Big value)
    {
#if NET5_0_OR_GREATER
        return value.GetBitLength();
#else
        // Bits of the minimal two's complement form, without the sign bit (BigInteger.GetBitLength's definition).
        var bytes = value.ToByteArray();
        var top = bytes[bytes.Length - 1];
        var sign = value.Sign < 0 ? 0xFF : 0x00;
        var length = (long)bytes.Length * 8;
        for (var bit = 7; bit >= 0 && ((top >> bit) & 1) == (sign & 1); bit--) length--;
        return length;
#endif
    }

    private static Big TrailingZeroCount(Big value)
    {
#if NET5_0_OR_GREATER
        return Big.TrailingZeroCount(value);
#else
        var count = 0;
        while (value.IsEven) { value >>= 1; count++; }
        return count;
#endif
    }

    private static List<Big> Operands(int seed)
    {
        var random = new Random(seed);
        var values = new List<Big>
        {
            Big.Zero, Big.One, Big.MinusOne, 2, -2, 3, 1L << 40, long.MaxValue, long.MinValue, (Big)long.MaxValue + 1,
            int.MaxValue, int.MinValue, (Big)ulong.MaxValue, Big.One << 1074, -(Big.One << 1074), (Big.One << 2000) - 1,
        };
        for (var i = 0; i < 40; i++)
        {
            var price = Units(100 * Math.Exp(random.NextDouble() - 0.5));
            var tiny = Units(random.NextDouble() * 1e-300);
            values.Add(price);
            values.Add(-price);
            values.Add(tiny);
            values.Add(price * Units(random.NextDouble()));
            values.Add(price + tiny);
            values.Add(NextLong(random, -1_000_000, 1_000_000));
            values.Add(((Big)NextLong(random) << random.Next(0, 200)) + random.Next(0, 3));
        }
        return values;
    }

    public static IEnumerable<object[]> Seeds() => Enumerable.Range(1, 8).Select(seed => new object[] { seed });

    [Theory]
    [MemberData(nameof(Seeds))]
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
            Assert.Equal(a.Sign > 0 && (a & (a - 1)).IsZero, x.IsPowerOfTwo);
            Assert.Equal(BitLength(a), x.GetBitLength());
            Assert.Equal((double)a, (double)x);
            Assert.Equal((float)a, (float)x);
            Assert.Equal(a.ToByteArray(), x.ToByteArray());
            Assert.Equal(a.ToString(), x.ToString());
            Assert.Equal(-a, (Big)(-x));
            Assert.Equal(~a, (Big)(~x));
            var increment = x; increment++;
            var decrement = x; decrement--;
            Assert.Equal(a + 1, (Big)increment);
            Assert.Equal(a - 1, (Big)decrement);
            Assert.Equal(Big.Abs(a), (Big)ExactInteger.Abs(x));
            Assert.Equal(Big.Pow(a, 3), (Big)ExactInteger.Pow(x, 3));
            Assert.Equal(x.GetHashCode(), ((ExactInteger)(Big)x).GetHashCode());
            if (a.Sign > 0)
            {
                Assert.Equal(Big.Log(a), ExactInteger.Log(x));
                Assert.Equal(Big.Log10(a), ExactInteger.Log10(x));
            }
#if NET5_0_OR_GREATER
            if (!a.IsZero) Assert.Equal(TrailingZeroCount(a), (Big)ExactInteger.TrailingZeroCount(x));
#endif
            foreach (var shift in new[] { 0, 1, 7, 52, 64, 1074, 1100 })
            {
                Assert.Equal(a << shift, (Big)(x << shift));
                Assert.Equal(a >> shift, (Big)(x >> shift));
                Assert.Equal(a << -shift, (Big)(x << -shift));
            }
            Assert.Equal(a << int.MinValue, (Big)(x << int.MinValue));
            // a >> int.MinValue is a << 2^31: BigInteger builds that 2^31-bit integer; see the dedicated test below.
            if (a.IsZero) Assert.Equal(a >> int.MinValue, (Big)(x >> int.MinValue));
            else Assert.Throws<OverflowException>(() => x >> int.MinValue);
            if (a >= long.MinValue && a <= long.MaxValue) Assert.Equal((long)a, (long)x);
            else Assert.Throws<OverflowException>(() => (long)x);
            if (a >= int.MinValue && a <= int.MaxValue) Assert.Equal((int)a, (int)x);
            else Assert.Throws<OverflowException>(() => (int)x);
            if (Big.Abs(a) <= new Big(decimal.MaxValue)) Assert.Equal((decimal)a, (decimal)x);
            else Assert.Throws<OverflowException>(() => (decimal)x);

            Assert.Equal(0, x.CompareTo((object)a));
            Assert.Equal(0, x.CompareTo((object)x));
            Assert.Equal(1, x.CompareTo(null));
            Assert.True(x.Equals((object)x));
            Assert.Equal(x.Equals((object)a), x.CompareTo((object)a) == 0);

            foreach (var b in values)
            {
                ExactInteger y = b;
                Assert.Equal(a + b, (Big)(x + y));
                Assert.Equal(a - b, (Big)(x - y));
                Assert.Equal(a * b, (Big)(x * y));
                Assert.Equal(a.CompareTo(b), Math.Sign(x.CompareTo(y)));
                Assert.Equal(a.CompareTo(b), Math.Sign(x.CompareTo((object)y)));
                Assert.Equal(a == b, x == y);
                Assert.Equal(a == b, x.Equals((object)y));
                Assert.Equal(a < b, x < y);
                Assert.Equal(a >= b, x >= y);
                Assert.Equal(Big.Max(a, b), (Big)ExactInteger.Max(x, y));
                Assert.Equal(Big.Min(a, b), (Big)ExactInteger.Min(x, y));
                Assert.Equal(Big.GreatestCommonDivisor(a, b), (Big)ExactInteger.GreatestCommonDivisor(x, y));
                Assert.Equal(a & b, (Big)(x & y));
                Assert.Equal(a | b, (Big)(x | y));
                Assert.Equal(a ^ b, (Big)(x ^ y));
                if (a == b) Assert.Equal(x.GetHashCode(), y.GetHashCode());
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
    public void Floating_conversions_into_ExactInteger_truncate_as_BigInteger_does()
    {
        foreach (var d in new[] { 0d, -0d, 1.5, -1.5, 1e300, -1e300, double.Epsilon, 4503599627370497d, Math.PI * (1L << 60) })
            Assert.Equal(new Big(d), (Big)(ExactInteger)d);
        foreach (var f in new[] { 0f, 1.5f, -1.5f, 1e38f, float.Epsilon })
            Assert.Equal(new Big(f), (Big)(ExactInteger)f);
        foreach (var m in new[] { 0m, 1.5m, -1.5m, decimal.MaxValue, decimal.MinValue })
            Assert.Equal(new Big(m), (Big)(ExactInteger)m);
    }

    [Fact]
    public void Shifting_left_by_2_to_the_31_overflows_where_BigInteger_allocates_it()
    {
        // The one place ExactInteger is not BigInteger: a result of 2^31 or more bits. BigInteger allocates it (256 MB
        // here); ExactInteger's exponent is an int, so it throws OverflowException instead. No window produces a
        // count past 2^31 bits, and a throw is preferable to a silent wrap.
#if NET5_0_OR_GREATER
        Assert.Equal(1L << 31, (Big.One >> int.MinValue).GetBitLength() - 1);
#endif
        Assert.Throws<OverflowException>(() => (ExactInteger)Big.One >> int.MinValue);
        Assert.Throws<OverflowException>(() => (ExactInteger)Big.One << int.MaxValue << 1);
    }

    [Fact]
    public void Window_units_match_the_independent_binary64_decoding()
    {
        var random = new Random(11);
        var values = new List<double>
        {
            0d, -0d, double.Epsilon, -double.Epsilon, 2.2250738585072009e-308, 2.2250738585072014e-308,
            -2.2250738585072014e-308, 4.4501477170144028e-308, 1d, -1d, 0.1, 100.25, double.MaxValue, double.MinValue,
        };
        for (var i = 0; i < 2000; i++)
            values.Add(BitConverter.Int64BitsToDouble(NextLong(random) & ~(2047L << 52) | ((long)random.Next(0, 2047) << 52)));
        foreach (var value in values)
            Assert.Equal(Units(value), (Big)ExactVarianceWindow.Units(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactVarianceWindow.Units(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => ExactVarianceWindow.Units(double.PositiveInfinity));
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
