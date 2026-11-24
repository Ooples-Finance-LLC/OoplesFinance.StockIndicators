using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriorDirectionalWordSmoothingTests
{
    [Fact]
    public void AcceptedWordsMatchIndependentRationalsAcrossExponentsAndPeriods()
    {
        double[] edges = [0, -0d, double.Epsilon, Math.ScaleB(1, -1022),
            1, Math.BitIncrement(1), Math.BitDecrement(2), 2,
            Math.ScaleB(1, -900), Math.ScaleB(1, 900), double.MaxValue];
        int accepted = 0;
        foreach (int period in new[] { 1, 2, 3, 7, 14, 20, 100, 2047, 2048, int.MaxValue })
        {
            var recurrence = new PriorSeededDirectionalMeasure.WordSmoothing(period);
            foreach (double previous in edges)
            foreach (double current in edges) Check(previous, current, period, recurrence, ref accepted);
            var random = new Random(1114 + period % 100);
            for (int i = 0; i < 1000; i++)
            {
                int exponent = random.Next(-1000, 1001);
                double previous = i % 13 == 0 ? 0 : Math.ScaleB(1 + random.NextDouble(), exponent);
                double current = i % 17 == 0 ? 0 : Math.ScaleB(1 + random.NextDouble(), exponent + random.Next(-20, 21));
                Check(previous, current, period, recurrence, ref accepted);
            }
        }
        Assert.True(accepted > 2500, $"Only {accepted} accepted word cases were exercised.");
    }

    [Fact]
    public void ExactMidpointsCarryAndCanonicalZeroMatchIndependentRationals()
    {
        var recurrence = new PriorSeededDirectionalMeasure.WordSmoothing(2);
        foreach (double previous in new[] { Math.BitIncrement(1),
            Math.BitIncrement(Math.BitIncrement(Math.BitIncrement(1))), Math.BitDecrement(2) })
        {
            Assert.True(recurrence.TrySum(previous, 1, out double actual));
            Bits(Expected(previous, 1, 2), actual);
        }
        Assert.True(recurrence.TrySum(Math.BitDecrement(2), 1, out double carry));
        Bits(2, carry);
        Assert.True(recurrence.TrySum(-0d, 0, out double zero));
        Bits(0, zero);
    }

    [Fact]
    public void UnsupportedInputsReturnToOriginalArithmetic()
    {
        var recurrence = new PriorSeededDirectionalMeasure.WordSmoothing(20);
        foreach (double value in new[] { -1d, double.Epsilon, -double.Epsilon, double.NaN,
            double.PositiveInfinity, double.NegativeInfinity })
        {
            Assert.False(recurrence.TrySum(value, 1, out _));
            Assert.False(recurrence.TrySum(1, value, out _));
        }
        Assert.False(recurrence.TrySum(1, Math.ScaleB(1, 63), out _)); // Alignment guard.
        Assert.False(recurrence.TrySum(1, Math.ScaleB(1, 20), out _)); // Word overflow.
        Assert.False(recurrence.TrySum(Math.ScaleB(1, -1022), 0, out _)); // Subnormal result.
        Assert.False(recurrence.TrySum(double.MaxValue, double.MaxValue, out _));
        foreach (int period in new[] { 0, -1, 2048, int.MaxValue })
            Assert.False(new PriorSeededDirectionalMeasure.WordSmoothing(period).TrySum(1, 2, out _));
    }

    [Theory]
    [InlineData(PriorDirectionalMeasure.PositiveMovement)]
    [InlineData(PriorDirectionalMeasure.NegativeMovement)]
    [InlineData(PriorDirectionalMeasure.PositiveIndicator)]
    [InlineData(PriorDirectionalMeasure.NegativeIndicator)]
    [InlineData(PriorDirectionalMeasure.Index)]
    [InlineData(PriorDirectionalMeasure.Average)]
    [InlineData(PriorDirectionalMeasure.Rating)]
    public void FullStagesAndResetMatchIndependentReferenceIncludingExtendedFallback(PriorDirectionalMeasure measure)
    {
        foreach (int period in new[] { 2, 20, 2047, 2048 })
        {
            var bars = Enumerable.Range(0, 3 * period + 9).Select(i =>
            {
                double close = 100 + (i * 13 % 101) / 8d;
                return new Bar(default, close, close + 2, close - 2, close, 1);
            }).ToArray();
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, period, 2).CreateState();
            var output = new double[2];
            for (int pass = 0; pass < 2; pass++)
            {
                var input = bars.ToArray();
                if (pass == 0)
                {
                    input[^5] = new(default, 0, double.MaxValue, -double.MaxValue, double.MaxValue, 1);
                    input[^4] = new(default, 0, -double.MaxValue, double.MaxValue, 0, 1);
                    input[^3] = new(default, 0, double.Epsilon, -double.Epsilon, -double.MaxValue, 1);
                }
                var expected = PriorDirectionalReference.Calculate(input, measure, period, 2);
                state.Reset();
                for (int i = 0; i < input.Length; i++)
                {
                    state.Update(in input[i], output);
                    Bits(expected[i] ?? 0, output[0]);
                    Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
                }
            }
        }
    }

    private static void Check(double previous, double current, int period,
        PriorSeededDirectionalMeasure.WordSmoothing recurrence, ref int accepted)
    {
        if (!recurrence.TrySum(previous, current, out double actual)) return;
        Bits(Expected(previous, current, period), actual);
        accepted++;
    }

    private static double Expected(double previous, double current, int period) =>
        ((ReferenceFraction.FromDouble(previous) * new ReferenceFraction(period - 1)
            + ReferenceFraction.FromDouble(current) * new ReferenceFraction(period))
            / new ReferenceFraction(period)).RoundExtendedBinary64().ToDouble();

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}
