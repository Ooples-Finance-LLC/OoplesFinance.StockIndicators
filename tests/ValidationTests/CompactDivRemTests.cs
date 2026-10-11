using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactDivRemTests
{
    [Fact]
    public void QuotientWidthDivisorWidthAndZeroGuardsMatchIndependentIntegers()
    {
        UInt128[] denominators = [1, 3, (UInt128)1 << 32, ((UInt128)1 << 53) + 1,
            ulong.MaxValue, (UInt128)1 << 64, ((UInt128)1 << 127) + 1, UInt128.MaxValue];
        foreach (var denominator in denominators)
        {
            UInt128[] numerators = [0, 1, denominator - 1, denominator, UInt128.MaxValue];
            foreach (var numerator in numerators) Check(numerator, denominator);
            if (denominator <= ulong.MaxValue)
            {
                // The last hardware-safe quotient and the first too-wide one.
                Check(((denominator - 1) << 64) | ulong.MaxValue, denominator);
                Check(denominator << 64, denominator);
            }
        }
        Assert.Throws<DivideByZeroException>(() => ExactMeanAccumulator.CompactDivRem(0, 0));
        Assert.Throws<DivideByZeroException>(() => ExactMeanAccumulator.CompactDivRem(UInt128.MaxValue, 0));
    }

    [Fact]
    public void RandomHardwareEligibleAndWideDivisionsMatchIndependentIntegers()
    {
        var random = new Random(94381);
        var bytes = new byte[16];
        UInt128 Next()
        {
            random.NextBytes(bytes);
            return ((UInt128)BitConverter.ToUInt64(bytes, 0) << 64) | BitConverter.ToUInt64(bytes, 8);
        }
        for (int i = 0; i < 2000; i++)
        {
            UInt128 numerator = Next();
            UInt128 denominator = Next() | 1;
            Check(numerator, denominator);
            ulong narrow = unchecked((ulong)denominator);
            UInt128 upper = (ulong)(numerator >> 64) % narrow;
            Check((upper << 64) | unchecked((ulong)numerator), narrow);
        }
    }

    private static void Check(UInt128 numerator, UInt128 denominator)
    {
        var quotient = BigInteger.DivRem((BigInteger)numerator, (BigInteger)denominator, out var remainder);
        var actual = ExactMeanAccumulator.CompactDivRem(numerator, denominator);
        Assert.Equal((UInt128)quotient, actual.Quotient);
        Assert.Equal((UInt128)remainder, actual.Remainder);
    }
}
