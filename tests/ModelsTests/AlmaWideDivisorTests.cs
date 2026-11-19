using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AlmaWideDivisorTests
{
    private static readonly UInt128 ActualMass = UInt128.Parse("4164322647515385545693");

    [Fact]
    public void CachedQuotientAndRemainderMatchIndependentBigIntegerBoundaries()
    {
        UInt128[] divisors = [(UInt128)1 << 64, ((UInt128)1 << 64) + 1,
            (UInt128)1 << 72, ActualMass, ((UInt128)1 << 78) - 1,
            (UInt128)1 << 127, UInt128.MaxValue - 1, UInt128.MaxValue];
        foreach (var denominator in divisors)
        {
            var divisor = new ValuesBarExecution.AlmaWideDivisor(denominator);
            UInt128[] numerators = [0, 1, denominator - 1, denominator,
                UInt128.MaxValue - 1, UInt128.MaxValue];
            foreach (var numerator in numerators) CheckDivision(divisor, numerator, denominator);
        }
        var random = new Random(72128);
        for (int i = 0; i < 5000; i++)
        {
            UInt128 denominator = Next128(random) | ((UInt128)1 << 64);
            CheckDivision(new(denominator), Next128(random), denominator);
        }
    }

    [Fact]
    public void ScaledRoundingMatchesIndependentFractionsAndKeepsOriginalFallback()
    {
        UInt128[] denominators = [(UInt128)1 << 64, ((UInt128)1 << 64) + 1,
            (UInt128)1 << 72, (UInt128)1 << 75, ActualMass, ((UInt128)1 << 78) - 1];
        Int128[] numerators = [0, 1, -1, ((Int128)1 << 53) - 1,
            ((Int128)1 << 89) - 1, -(((Int128)1 << 89) - 1), Int128.MaxValue, Int128.MinValue];
        int accepted = 0, fallback = 0;
        foreach (var denominator in denominators)
        foreach (var numerator in numerators)
        foreach (int scale in new[] { -1400, -1100, -1075, -1074, -1073, -1022, -53, -3, 0, 971, 1023, 1100 })
        {
            var divisor = new ValuesBarExecution.AlmaWideDivisor(denominator);
            double expected = Reference(numerator, denominator, scale);
            if (divisor.TryRatio(numerator, scale, out double actual)) accepted++;
            else
            {
                fallback++;
                // The cached helper may decline any rounding-grid miss. Do
                // not extend the original product factory's <=106-bit contract
                // merely to test the private helper's larger input domain.
                if (BigInteger.Abs((BigInteger)numerator).GetBitLength() > 106) continue;
                actual = JurikDyadic.FromBinaryProduct(numerator, scale)
                    .Ratio(JurikDyadic.FromBinaryProduct((Int128)denominator, 0));
            }
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.True(accepted > 0 && fallback > 0);
    }

    [Fact]
    public void NormalMidpointsAndSubnormalDoubleRoundingRetainEveryBit()
    {
        UInt128 denominator = (UInt128)1 << 72;
        var divisor = new ValuesBarExecution.AlmaWideDivisor(denominator);
        foreach (Int128 midpoint in new[] { ((Int128)1 << 72) + ((Int128)1 << 19),
            ((Int128)1 << 72) - ((Int128)1 << 18) })
        foreach (int delta in new[] { -1, 0, 1 })
        foreach (int sign in new[] { -1, 1 })
        {
            Int128 numerator = sign * (midpoint + delta);
            Assert.True(divisor.TryRatio(numerator, 0, out double actual));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Reference(numerator, denominator, 0)),
                BitConverter.DoubleToInt64Bits(actual));
        }
        denominator = (UInt128)1 << 74;
        divisor = new(denominator);
        Int128 subnormalNumerator = (3 * ((Int128)1 << 53) - 1) << 20;
        Assert.True(divisor.TryRatio(subnormalNumerator, -1074, out double exact));
        Assert.Equal(double.Epsilon, exact);
        Assert.NotEqual(exact, Math.ScaleB((double)subnormalNumerator / (double)denominator, -1074));
        Assert.True(divisor.TryRatio(-1, -1074, out double negativeZero));
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(negativeZero));
        Assert.True(divisor.TryRatio(0, -1074, out double canonicalZero));
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(canonicalZero));
    }

    [Fact]
    public void WordAndUninitializedDivisorsCannotEnterTheCachedWideRoute()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ValuesBarExecution.AlmaWideDivisor(ulong.MaxValue));
        var empty = default(ValuesBarExecution.AlmaWideDivisor);
        Assert.False(empty.TryRatio(1, 0, out _));
        Assert.Throws<DivideByZeroException>(() => empty.Divide(1));
    }

    private static void CheckDivision(ValuesBarExecution.AlmaWideDivisor divisor, UInt128 numerator, UInt128 denominator)
    {
        var (quotient, remainder) = divisor.Divide(numerator);
        BigInteger expected = BigInteger.DivRem((BigInteger)numerator, (BigInteger)denominator, out var rest);
        Assert.Equal(expected, (BigInteger)quotient);
        Assert.Equal(rest, (BigInteger)remainder);
    }

    private static UInt128 Next128(Random random)
    {
        var bytes = new byte[16]; random.NextBytes(bytes);
        return ((UInt128)BitConverter.ToUInt64(bytes, 8) << 64) | BitConverter.ToUInt64(bytes, 0);
    }

    private static double Reference(Int128 numerator, UInt128 denominator, int scale)
    {
        var value = new ReferenceFraction((BigInteger)numerator) / new ReferenceFraction((BigInteger)denominator);
        var power = new ReferenceFraction(BigInteger.One << Math.Abs(scale));
        return (scale < 0 ? value / power : value * power).ToDouble();
    }
}
