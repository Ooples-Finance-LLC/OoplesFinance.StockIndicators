using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FrameworkCircularMathTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ReducedKernelMatchesIndependentReferenceAcrossBinary64Range(int operation)
    {
        var values = new List<double> { 0, double.Epsilon, .5, Math.BitIncrement(.5),
            1e9, 1e100, 1.0148715737776067e100, double.MaxValue };
        for (var exponent = -1022; exponent <= 1023; exponent += 37)
            values.Add(Math.ScaleB(1.371, exponent));
        for (var quadrant = 1; quadrant <= 16; quadrant++)
        {
            var angle = quadrant * Math.PI / 2;
            values.Add(Math.BitDecrement(angle));
            values.Add(angle);
            values.Add(Math.BitIncrement(angle));
        }
        foreach (var magnitude in values)
        foreach (var sign in new[] { -1d, 1d })
        {
            var value = sign * magnitude;
            var expected = CircularReference.Value(value, (PriceCircularOperation)operation);
            var actual = FrameworkCircularMath.Value(value, operation);
            Assert.True(Math.Abs(expected - actual) <= Math.Abs(expected) * 4e-15,
                $"operation {operation}, input {value:R}: expected {expected:R}, got {actual:R}");
            Assert.Equal(Math.Sign(expected), Math.Sign(actual));
        }
    }

    [Fact]
    public void OddFunctionsPreserveNegativeZero()
    {
        foreach (var operation in new[] { 0, 2 })
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(FrameworkCircularMath.Value(-0d, operation)));
    }
}
