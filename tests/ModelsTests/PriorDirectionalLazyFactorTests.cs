using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriorDirectionalLazyFactorTests
{
    [Fact]
    public void NonfiniteInputsClearOutputAndLeaveTheOriginalStateUnchanged()
    {
        var bars = Enumerable.Range(0, 80)
            .Select(i => new Bar(default, 7, 10 + i % 7, 3 + i % 5, 7 + i % 3, 1)).ToArray();
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        {
            var expected = PriorDirectionalReference.Calculate(bars, measure, 3, 1);
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, 3, 1).CreateState();
            var output = new double[2];
            for (int row = 0; row < bars.Length; row++)
            {
                if (row is 0 or 2 or 20)
                {
                    for (int field = 0; field < (measure >= PriorDirectionalMeasure.PositiveIndicator ? 3 : 2); field++)
                    {
                        var bar = bars[row];
                        var invalid = new Bar(bar.Time, bar.Open,
                            field == 0 ? double.NaN : bar.High,
                            field == 1 ? double.NegativeInfinity : bar.Low,
                            field == 2 ? double.PositiveInfinity : bar.Close, bar.Volume);
                        output[0] = 17; output[1] = 19;
                        var error = Assert.Throws<ArithmeticException>(() => state.Update(in invalid, output));
                        Assert.Equal("An exact finite product requires finite inputs.", error.Message);
                        Assert.Equal(new double[2], output);
                    }
                }
                state.Update(in bars[row], output);
                Equal(expected[row], output);
            }
        }
    }

    [Fact]
    public void RawMovementStillIgnoresUnusedNonfiniteClosePrices()
    {
        var clean = Enumerable.Range(0, 70)
            .Select(i => new Bar(default, 0, 10 + i % 7, 3 + i % 5, 0, 1)).ToArray();
        double[] closes = [double.NaN, double.PositiveInfinity, double.NegativeInfinity];
        var bars = clean.Select((bar, i) => new Bar(bar.Time, bar.Open, bar.High, bar.Low, closes[i % 3], bar.Volume)).ToArray();
        foreach (var measure in new[] { PriorDirectionalMeasure.PositiveMovement, PriorDirectionalMeasure.NegativeMovement })
        foreach (int period in new[] { 1, 3 })
        {
            var expected = PriorDirectionalReference.Calculate(clean, measure, period, 3);
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, period, 3).CreateState();
            var output = new double[2];
            for (int row = 0; row < bars.Length; row++)
            {
                state.Update(in bars[row], output);
                Equal(expected[row], output);
            }
        }
    }

    [Fact]
    public void ExactFallbackAfterOrdinaryHistoryAndResetMatchesIndependentReference()
    {
        double m = double.MaxValue;
        Bar[] cycle = [new(default, 0, -m, -m, 0, 1), new(default, 0, m, m, 0, 1),
            new(default, 0, double.Epsilon, -double.Epsilon, 0, 1),
            new(default, 0, 4, 1, 3, 1), new(default, 0, 1, 4, 2, 1),
            new(default, 0, 1, 0, 0, 1), new(default, 0, 1e20, -1e20, 0, 1)];
        var bars = Enumerable.Range(0, 20).Select(i => new Bar(default, 0, 10 + i, 3 + i, 7 + i, 1))
            .Concat(Enumerable.Range(0, 77).Select(i => cycle[i % cycle.Length])).ToArray();
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (int period in new[] { 3, 14 })
        {
            var expected = PriorDirectionalReference.Calculate(bars, measure, period, 0);
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, period).CreateState();
            var output = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int row = 0; row < bars.Length; row++)
                {
                    state.Update(in bars[row], output);
                    Equal(expected[row], output);
                }
            }
        }
    }

    private static void Equal(double? expected, double[] output)
    {
        Assert.Equal(expected.HasValue ? 1d : 0d, output[1]);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
    }
}
