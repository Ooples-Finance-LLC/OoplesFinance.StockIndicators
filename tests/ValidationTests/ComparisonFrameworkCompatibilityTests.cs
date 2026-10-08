using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ComparisonFrameworkCompatibilityTests
{
    [Fact]
    public void PortableBitLengthMatchesRuntimeAcrossSignedPowerBoundaries()
    {
        for (var exponent = 0; exponent <= 2048; exponent++)
        foreach (var delta in new[] { -1, 0, 1 })
        foreach (var sign in new[] { -1, 1 })
        {
            var value = sign * ((BigInteger.One << exponent) + delta);
            Assert.Equal(value.GetBitLength(), FrameworkCompatibility.GetBitLengthPortable(value));
        }
    }

    [Fact]
    public void PortablePredicatesPreserveSpecialValues()
    {
        foreach (var value in new[] { double.NaN, double.NegativeInfinity, double.PositiveInfinity,
                     -double.MaxValue, double.MaxValue, -0d, 0d, double.Epsilon, -double.Epsilon })
        {
            Assert.Equal(double.IsFinite(value), FrameworkCompatibility.IsFinite(value));
            var expected = Math.Clamp(value, -1d, 1d);
            var actual = FrameworkCompatibility.Clamp(value, -1d, 1d);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
        }
        Assert.Throws<ArgumentException>(() => FrameworkCompatibility.Clamp(0, 1, -1));
    }

    [Fact]
    public void EveryDiscoveredLibraryConfigurationCanBeConstructed()
    {
        var failures = new List<string>();
        foreach (var item in IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }))
        {
            try { Assert.IsAssignableFrom<IIndicator>(item.Factory()); }
            catch (Exception error) { failures.Add(item + ": " + error.GetBaseException().Message); }
        }
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
