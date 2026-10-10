using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CertifiedSmaBatchTests
{
    [Theory]
    [InlineData(2, -512)]
    [InlineData(3, -20)]
    [InlineData(20, 0)]
    [InlineData(31, 100)]
    [InlineData(32, 480)]
    public void CertifiedSignedWindowsMatchIndependentRationalRounding(int period, int exponent)
    {
        var random = new Random(921);
        var input = Enumerable.Range(0, 257).Select(_ => random.Next(-1_000_000, 1_000_000) * Math.Pow(2, exponent)).ToArray();
        input[30] = -input[29];
        input[31] = 0;
        var output = new double[input.Length];
        Assert.True(MovingAverageCore.TryExactGridSimpleMovingAverage(input, output, period));
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            if (i >= period - 1)
                for (var j = i - period + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(input[j]);
            var expected = (sum / new ReferenceFraction(period)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(output[i]));
        }
    }

    [Fact]
    public void LateUncertifiedValuesLeaveOutputsUntouchedBeforeGuardedFallback()
    {
        foreach (var outlier in new[] { Math.BitIncrement(1d), double.Epsilon, double.MaxValue,
            double.NaN, double.PositiveInfinity, Math.Pow(2, -513), Math.Pow(2, 501) })
        {
            var input = Enumerable.Repeat(1d, 80).Append(outlier).ToArray();
            var output = Enumerable.Repeat(123d, input.Length).ToArray();
            Assert.False(MovingAverageCore.TryExactGridSimpleMovingAverage(input, output, 20));
            Assert.All(output, value => Assert.Equal(123d, value));
        }
    }

    [Fact]
    public void CertificateRejectsWindowsWhoseIntermediateExceedsItsIntegerBudget()
    {
        var input = new[] { Math.Pow(2, 48), 1d, -Math.Pow(2, 48) };
        Assert.True(MovingAverageCore.TryExactGridSimpleMovingAverage(input, new double[3], 15));
        Assert.False(MovingAverageCore.TryExactGridSimpleMovingAverage(input, new double[3], 16));
    }

    [Fact]
    public void EmptyHugePeriodAndSignedZeroKeepWarmupAndBitPatterns()
    {
        Assert.True(MovingAverageCore.TryExactGridSimpleMovingAverage([], [], int.MaxValue));
        var output = new double[4];
        Assert.True(MovingAverageCore.TryExactGridSimpleMovingAverage(new[] { -0d, 0d, -0d, 0d }, output, 2));
        Assert.All(output, value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
        Assert.True(MovingAverageCore.TryExactGridSimpleMovingAverage(new[] { 1d, 2d, 3d, 4d }, output, int.MaxValue));
        Assert.All(output, value => Assert.Equal(0d, value));
    }
}
