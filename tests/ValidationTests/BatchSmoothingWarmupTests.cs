using System.Reflection;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class BatchSmoothingWarmupTests
{
    private delegate void Smooth(StockData data, MovingAvgType kind, int length, ReadOnlySpan<double> input, Span<double> output);
    private static readonly Smooth Run = typeof(IndicatorCompute).GetMethod("StochasticSmooth", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate<Smooth>();
    private static StockData Data(double[] input) => new(input, input, input, input, input, input.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));

    [Theory]
    [InlineData(0, int.MaxValue)]
    [InlineData(7, int.MaxValue)]
    [InlineData(7, 8)]
    [InlineData(7, 7)]
    [InlineData(7, 6)]
    [InlineData(7, int.MinValue)]
    public void BatchBoundaryPreservesOutputBitsAndUnusedTail(int count, int length)
    {
        double[] prices = [double.MaxValue, -double.MaxValue, double.Epsilon, -0d, 3, -2, 1];
        var input = prices.Take(count).ToArray();
        var output = Enumerable.Repeat(91d, count + 2).ToArray();
        Run(Data(input), MovingAvgType.SimpleMovingAverage, length, input, output);
        var expected = new double[count];
        if (length != int.MaxValue)
        {
            using var reference = new RoundedSimpleMovingAverageSmoother(length);
            for (var i = 0; i < count; i++) expected[i] = reference.Next(input[i], true);
        }
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), output.Take(count).Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(new[] { 91d, 91d }, output.Skip(count));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ShortBatchStillRejectsNonfiniteInputAtTheSamePosition(double invalid)
    {
        double[] input = [2, invalid, 4];
        var output = new[] { 91d, 91d, 91d, 91d };
        using var reference = new RoundedSimpleMovingAverageSmoother(4);
        reference.Next(input[0], true);
        var expected = Assert.Throws<ArithmeticException>(() => reference.Next(invalid, true));
        var actual = Assert.Throws<ArithmeticException>(() => Run(Data(input), MovingAvgType.SimpleMovingAverage, int.MaxValue, input, output));
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(new[] { 0d, 91d, 91d, 91d }, output);
    }

    [Fact]
    public void ShortBatchStillInvokesCustomerAverageWithOriginalPeriod()
    {
        double[] input = [2, 4, 6];
        Func<IReadOnlyList<double>, int, IReadOnlyList<double>> callback = (values, period) =>
        {
            Assert.Equal(int.MaxValue, period);
            Assert.Equal(input, values);
            return new[] { 7d, 8, 9 };
        };
        using var scope = ComponentAverage.Arm([callback]);
        var output = new double[input.Length];
        Run(Data(input), MovingAvgType.SimpleMovingAverage, int.MaxValue, input, output);
        Assert.Equal(1, ComponentAverage.Substitutions);
        Assert.Equal(new[] { 7d, 8, 9 }, output);
    }
}
