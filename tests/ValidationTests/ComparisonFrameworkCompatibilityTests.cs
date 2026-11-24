using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ComparisonFrameworkCompatibilityTests
{
    [Fact]
    public void PortableAdjacentValuesMatchRuntimeBitsAcrossBinary64Boundaries()
    {
        void Check(double value)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(Math.BitIncrement(value)), BitConverter.DoubleToInt64Bits(FrameworkCompatibility.BitIncrementPortable(value)));
            Assert.Equal(BitConverter.DoubleToInt64Bits(Math.BitDecrement(value)), BitConverter.DoubleToInt64Bits(FrameworkCompatibility.BitDecrementPortable(value)));
        }
        foreach (var value in new[] { 0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            double.PositiveInfinity, double.NegativeInfinity, double.NaN }) Check(value);
        for (var exponent = 1; exponent < 2047; exponent++)
        foreach (var offset in new[] { -1L, 0L, 1L })
        {
            var value = BitConverter.Int64BitsToDouble(((long)exponent << 52) + offset);
            Check(value); Check(-value);
        }
        var random = new Random(271828); var bytes = new byte[8];
        for (var i = 0; i < 10000; i++) { random.NextBytes(bytes); Check(BitConverter.ToDouble(bytes, 0)); }
    }

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
