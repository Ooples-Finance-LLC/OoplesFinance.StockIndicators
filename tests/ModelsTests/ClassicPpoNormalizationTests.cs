using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ClassicPpoNormalizationTests
{
    [Theory]
    [InlineData(1d)]
    [InlineData(-1d)]
    public void PercentageRoundsOnceAfterExactDifferenceAndReset(double sign)
    {
        double previous = sign * 0.8832541825499436;
        double current = sign * 2.2520046369717046;
        var bars = new[]
        {
            new Bar(default, previous, previous, previous, previous, 1),
            new Bar(default, current, current, current, current, 1),
        };
        var owner = new ClassicPriceOscillator(1, 2, ClassicAverageMethod.Sma, percentage: true);
        var fast = ClassicAverageReference.ExtendedValues(bars, owner.Fast);
        var slow = ClassicAverageReference.ExtendedValues(bars, owner.Slow);
        double expected = ((fast[1]!.Value - slow[1]!.Value)
            * new ReferenceFraction(100) / slow[1]!.Value).ToDouble();
        Assert.Equal(0x4045d40ea8c338efL, BitConverter.DoubleToInt64Bits(expected));
        double roundedSlow = (previous + current) / 2;
        Assert.NotEqual(BitConverter.DoubleToInt64Bits(expected),
            BitConverter.DoubleToInt64Bits(100 * (current - roundedSlow) / roundedSlow));
        var state = (IMultiOutputState)owner.CreateState();
        using var lifetime = state as IDisposable;
        var output = new double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            state.Update(in bars[0], output);
            Assert.Equal(0d, output[1]);
            state.Update(in bars[1], output);
            Assert.Equal(1d, output[1]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(output[0]));
        }
    }
}
