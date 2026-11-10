using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ShortLogReturnCertificateTests
{
    [Theory]
    [InlineData(1.1, 1d)]
    [InlineData(.9, 1d)]
    [InlineData(2d, 1d)]
    [InlineData(.5, 1d)]
    [InlineData(1.5, 1d)]
    [InlineData(double.MaxValue, double.Epsilon)]
    [InlineData(double.Epsilon, double.MaxValue)]
    [InlineData(1d, 1d)]
    public void CertifiedInitialCellsMatchIndependentLogarithms(double current, double previous)
    {
        double expected = (ReferenceFraction.FromDouble(current)
            / ReferenceFraction.FromDouble(previous)).LogToDouble();
        Assert.True(CertifiedLogReturn.TryShort(new(current), new(previous), out double actual));
        Bits(expected, actual);
        Bits(expected, CertifiedLogReturn.Of(current, previous));
        Bits(expected, CertifiedLogReturn.Of(-current, -previous));
    }

    [Fact]
    public void UnresolvedInitialCellRetainsTheExistingFixedCertificate()
    {
        double current = 1 + Math.ScaleB(1d, -30);
        Assert.False(CertifiedLogReturn.TryShort(new(current), new(1d), out _));
        Assert.True(CertifiedLogReturn.TryFixed(new(current), new(1d), out double existing));
        double expected = ReferenceFraction.FromDouble(current).LogToDouble();
        Bits(expected, existing);
        Bits(expected, CertifiedLogReturn.Of(current, 1d));
    }

    [Fact]
    public void AdjacentPricesKeepIndependentRoundingWhenFixedCertificatesDoNotResolve()
    {
        double current = Math.BitIncrement(1d);
        Assert.False(CertifiedLogReturn.TryShort(new(current), new(1d), out _));
        Assert.False(CertifiedLogReturn.TryFixed(new(current), new(1d), out _));
        Bits(ReferenceFraction.FromDouble(current).LogToDouble(), CertifiedLogReturn.Of(current, 1d));
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
