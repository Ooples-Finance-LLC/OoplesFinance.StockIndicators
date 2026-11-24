using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriorDirectionalRawStageTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RoundedPositiveTiesRetainTheExactDominantDirection(bool positiveWins)
    {
        Bar prior = positiveWins ? new(default, 0, 0, -1, 0, 1) : new(default, 0, 1, 0, 0, 1);
        Bar next = new(default, 0, 1e20, -1e20, 0, 1);
        Assert.Equal(next.High - prior.High, prior.Low - next.Low);
        foreach (var measure in new[] { PriorDirectionalMeasure.PositiveMovement, PriorDirectionalMeasure.NegativeMovement })
        {
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, 1).CreateState();
            var output = new double[2];
            state.Update(prior, output);
            state.Update(next, output);
            Assert.Equal(1d, output[1]);
            bool selected = measure == PriorDirectionalMeasure.PositiveMovement ? positiveWins : !positiveWins;
            Assert.Equal(selected ? 1e20 : 0, output[0]);
        }
    }

    [Theory]
    [InlineData(PriorDirectionalMeasure.PositiveMovement)]
    [InlineData(PriorDirectionalMeasure.NegativeMovement)]
    [InlineData(PriorDirectionalMeasure.PositiveIndicator)]
    [InlineData(PriorDirectionalMeasure.NegativeIndicator)]
    [InlineData(PriorDirectionalMeasure.Index)]
    [InlineData(PriorDirectionalMeasure.Average)]
    [InlineData(PriorDirectionalMeasure.Rating)]
    public void OverflowingRawDifferencesRetainExtendedStagesAndReset(PriorDirectionalMeasure measure)
    {
        double m = double.MaxValue;
        Bar[] cycle = [new(default, 0, -m, -m, 0, 1), new(default, 0, m, m, 0, 1),
            new(default, 0, double.Epsilon, -double.Epsilon, 0, 1),
            new(default, 0, 4, 1, 3, 1), new(default, 0, 1, 4, 2, 1),
            new(default, 0, 1, 0, 0, 1), new(default, 0, 1e20, -1e20, 0, 1)];
        var bars = Enumerable.Range(0, 91).Select(i => cycle[i % cycle.Length]).ToArray();
        foreach (int period in new[] { 2, 3, 14, 31 })
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
                    Assert.Equal(expected[row].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[row] ?? 0),
                        BitConverter.DoubleToInt64Bits(output[0]));
                }
            }
        }
    }
}
