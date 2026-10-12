using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DeferredStrengthPublicationTests
{
    [Theory]
    [InlineData(NullableStrengthConvention.RelativeStrength)]
    [InlineData(NullableStrengthConvention.RelativeStrengthIndex)]
    [InlineData(NullableStrengthConvention.NetMomentum)]
    [InlineData(NullableStrengthConvention.RelativeMomentum)]
    [InlineData(NullableStrengthConvention.RelativeMomentumIndex)]
    public void DeferredOutputsPreserveIndependentFormulaAcrossGapsExtremesAndReset(NullableStrengthConvention convention)
    {
        int lag = NullableStrengthOscillator.IsMomentum(convention) ? 3 : 1;
        foreach (double scale in new[] { 1d, 1e-200, double.Epsilon, double.MaxValue / 8 })
        foreach (int gap in new[] { -1, 1, 23 })
        {
            var values = Enumerable.Range(0, 80).Select(i =>
                i == gap ? null : (double?)((i * 13 % 7 - 3) * scale)).ToArray();
            var expected = NullableStrengthReference.Calculate(values, 5, convention, lag);
            var state = new NullableStrengthOscillator.Calculator(5, convention, lag);
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int i = 0; i < values.Length; i++)
                {
                    bool publish = (i + replay) % 7 == 0 || i == values.Length - 1;
                    var actual = state.Next(values[i], publish);
                    if (!publish) { Assert.Null(actual); continue; }
                    Assert.Equal(expected[i].HasValue, actual.HasValue);
                    if (actual.HasValue)
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]!.Value), BitConverter.DoubleToInt64Bits(actual.Value));
                }
            }
        }
    }
}
