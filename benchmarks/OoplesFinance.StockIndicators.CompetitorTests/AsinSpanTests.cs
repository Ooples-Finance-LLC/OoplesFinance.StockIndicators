using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class AsinSpanTests
{
    [Fact]
    public void CloseOnlyMatchesValidatedBarRouteAndPreservesTail()
    {
        var data = CircularComparison.BoundaryFixture("Asin");
        var expected = new double[data.Count];
        IndicatorKernels.Asin().Process(data.IndicatorBars, expected);
        var actual = Enumerable.Repeat(123d, data.Count + 3).ToArray();
        IndicatorKernels.Asin(data.Closes, actual);
        Assert.Equal(expected, actual.Take(data.Count));
        Assert.All(actual.Skip(data.Count), v => Assert.Equal(123, v));
        IndicatorKernels.Asin(data.Closes, data.Closes);
        Assert.Equal(expected, data.Closes);
    }
    [Fact]
    public void RejectedBuffersLeaveOutputUntouched()
    {
        var output = new[] { 42d, 43d, 44d };
        Assert.Throws<ArgumentException>(() => IndicatorKernels.Asin(new double[4], output));
        Assert.Equal(new[] { 42d, 43d, 44d }, output);
        var overlap = new[] { .1, .2, .3 };
        Assert.Throws<ArgumentException>(() => IndicatorKernels.Asin(overlap.AsSpan(0, 2), overlap.AsSpan(1, 2)));
        Assert.Throws<ArgumentException>(() => IndicatorKernels.Asin(overlap.AsSpan(1, 2), overlap.AsSpan(0, 2)));
        Assert.Equal(new[] { .1, .2, .3 }, overlap);
    }
    [Fact]
    public void NonfiniteAndSignedZeroMatchNativeIeeeBehavior()
    {
        var input = new[] { double.NegativeInfinity, double.NaN, double.PositiveInfinity, -0d, 0d };
        var ours = new double[input.Length]; var native = new double[input.Length];
        IndicatorKernels.Asin(input, ours);
        var code = TALib.Functions.Asin<double>(input, System.Range.All, native, out var range);
        Assert.Equal(TALib.Core.RetCode.Success, code);
        Assert.Equal((0, input.Length), range.GetOffsetAndLength(input.Length));
        Assert.Equal(native, ours);
        Assert.All(ours.Take(3), value => Assert.True(double.IsNaN(value)));
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0d), BitConverter.DoubleToInt64Bits(ours[3]));
    }

    [Fact]
    public void EmptyDomainAndAllocationContract()
    {
        var output = new[] { 42d, 43d, 44d };
        IndicatorKernels.Asin(ReadOnlySpan<double>.Empty, output);
        Assert.Equal(42, output[0]);
        var input = new[] { -2d, 0d, 2d };
        IndicatorKernels.Asin(input, output);
        Assert.True(double.IsNaN(output[0])); Assert.Equal(0, output[1]); Assert.True(double.IsNaN(output[2]));
        input = new CpuNativeWorkload("TaLib.Functions.Asin", 10_000).Data.Closes;
        output = new double[input.Length];
        for (var i = 0; i < 10; i++) IndicatorKernels.Asin(input, output);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++) IndicatorKernels.Asin(input, output);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }
}
