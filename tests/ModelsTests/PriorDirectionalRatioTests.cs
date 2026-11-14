using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriorDirectionalRatioTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IndicatorRoundsTheExactWeightedRatioOnce(bool positive)
    {
        double movement = Math.BitIncrement(1d), seed = 2 * movement;
        Bar[] bars = positive
            ? [new(default, 0, 0, seed - 5, 0, 1), new(default, 0, seed, seed - 5, 0, 1), new(default, 0, 0, 0, 0, 1)]
            : [new(default, 0, 5 - seed, 0, 0, 1), new(default, 0, 5 - seed, -seed, 0, 1), new(default, 0, 0, 0, 0, 1)];
        var measure = positive ? PriorDirectionalMeasure.PositiveIndicator : PriorDirectionalMeasure.NegativeIndicator;
        var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, 2).CreateState();
        var output = new double[2];
        foreach (var bar in bars) state.Update(bar, output);
        Assert.Equal(1d, output[1]);
        Assert.Equal(Math.BitIncrement(40d), output[0]);
        Assert.Equal(Math.BitIncrement(Math.BitIncrement(40d)), 100 * movement / 2.5);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PeriodOneRetainsZeroRangeConventionWithNonzeroMovement(bool positive)
    {
        double price = positive ? 10 : -10;
        var measure = positive ? PriorDirectionalMeasure.PositiveIndicator : PriorDirectionalMeasure.NegativeIndicator;
        var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, 1).CreateState();
        var output = new double[2];
        state.Update(new Bar(default, 0, 0, 0, price, 1), output);
        state.Update(new Bar(default, 0, price, price, price, 1), output);
        Assert.Equal(1d, output[1]);
        Assert.Equal(0d, output[0]);
    }
}
