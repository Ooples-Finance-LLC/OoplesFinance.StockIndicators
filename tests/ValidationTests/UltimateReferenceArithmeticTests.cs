using System.Numerics;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UltimateReferenceArithmeticTests
{
    [Fact]
    public void FractionalEnclosuresContainExactRationalRoots()
    {
        var half = new ReferenceFraction(1) / new ReferenceFraction(2);
        var squareRoot = UltimateReferenceArithmetic.Weight(4, 9, half, 192);
        var exact = new ReferenceFraction(2) / new ReferenceFraction(3);
        Assert.True(squareRoot.Low.CompareTo(exact) <= 0 && squareRoot.High.CompareTo(exact) >= 0);
        var fifthRoot = UltimateReferenceArithmetic.Weight(4, 9, half * new ReferenceFraction(5), 192);
        exact = new ReferenceFraction(32) / new ReferenceFraction(243);
        Assert.True(fifthRoot.Low.CompareTo(exact) <= 0 && fifthRoot.High.CompareTo(exact) >= 0);
        var negative = UltimateReferenceArithmetic.Weight(4, 9, half * new ReferenceFraction(-3), 192);
        exact = new ReferenceFraction(1) / new ReferenceFraction(8);
        Assert.True(negative.Low.CompareTo(exact) <= 0 && negative.High.CompareTo(exact) >= 0);
    }

    [Fact]
    public void NearOneEnclosureIsVerifiedByExactSquaring()
    {
        var p = new ReferenceFraction(5) / new ReferenceFraction(2);
        var actual = UltimateReferenceArithmetic.Weight(int.MaxValue - 1, int.MaxValue, p, 192);
        var square = new ReferenceFraction(BigInteger.Pow((BigInteger)int.MaxValue - 1, 5))
            / new ReferenceFraction(BigInteger.Pow(int.MaxValue, 5));
        Assert.True((actual.Low * actual.Low).CompareTo(square) <= 0);
        Assert.True((actual.High * actual.High).CompareTo(square) >= 0);
        Assert.True((actual.High - actual.Low).CompareTo(new ReferenceFraction(1) / new ReferenceFraction(BigInteger.One << 160)) < 0);
    }

    [Theory]
    [InlineData(1000, "233.8336071428446110146485796518701124773078213245407218815680296649561059844427166779834312735180973719")]
    [InlineData(int.MaxValue, "501079518.1333333334608358287269682491110665728178911702370116165474936458876038717094177337503206635176")]
    public void LargeFractionalSumEnclosesIndependentDecimalEvaluation(int length, string decimalValue)
    {
        // Decimal180 Euler-Maclaurin with 48 derivatives; its remainder bounds
        // are below 3.2e-101 and 5.1e-122 respectively. The length1000 value
        // was also compared with a direct180-digit sum of all1000 powers.
        var parts = decimalValue.Split('.');
        var expected = new ReferenceFraction(BigInteger.Parse(parts[0] + parts[1]))
            / new ReferenceFraction(BigInteger.Pow(10, parts[1].Length));
        var actual = UltimateReferenceArithmetic.PublicWeightSum(length, new ReferenceFraction(23) / new ReferenceFraction(7), 192);
        Assert.True(actual.Low.CompareTo(expected) <= 0 && actual.High.CompareTo(expected) >= 0);
        Assert.True((actual.High - actual.Low).CompareTo(new ReferenceFraction(1) / new ReferenceFraction(BigInteger.One << 192)) < 0);
    }
}
