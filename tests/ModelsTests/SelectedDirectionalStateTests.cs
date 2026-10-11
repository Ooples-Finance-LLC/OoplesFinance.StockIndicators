using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SelectedDirectionalStateTests
{
    [Theory]
    [InlineData(PriorDirectionalMeasure.PositiveMovement)]
    [InlineData(PriorDirectionalMeasure.NegativeMovement)]
    [InlineData(PriorDirectionalMeasure.PositiveIndicator)]
    [InlineData(PriorDirectionalMeasure.NegativeIndicator)]
    [InlineData(PriorDirectionalMeasure.Index)]
    [InlineData(PriorDirectionalMeasure.Average)]
    [InlineData(PriorDirectionalMeasure.Rating)]
    public void ResetClearsRetainedPricesSeedsAndRatingHistory(PriorDirectionalMeasure measure)
    {
        double[] prices = [-0d, 0d, double.Epsilon, -double.Epsilon, 1, -.125, 1e-200, double.MaxValue / 1024, -double.MaxValue / 1024];
        foreach (int period in new[] { measure <= PriorDirectionalMeasure.NegativeIndicator ? 1 : 2, 3, 20 })
        {
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, period, 2).CreateState();
            var output = new double[2];
            for (int pass = 0; pass < 2; pass++)
            {
                var bars = Enumerable.Range(0, 83).Select(i => new Bar(default, 0,
                    prices[(i + pass * 3) % prices.Length], prices[(i + pass * 3 + 2) % prices.Length],
                    prices[(i + pass * 3 + 4) % prices.Length], 1)).ToArray();
                var expected = PriorDirectionalReference.Calculate(bars, measure, period, 2);
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(bars[i], output);
                    Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                }
            }
        }
    }

    [Fact]
    public async Task PrunedArithmeticStillValidatesEveryInputField()
    {
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (int field in Enumerable.Range(0, 5))
        {
            var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, 1, 1), 65).ToArray();
            bars[^1] = new Bar(default, field == 0 ? double.NaN : 1, field == 1 ? double.NaN : 2,
                field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(new PriorSeededDirectionalMeasure(measure, 3)).ConfigureHistory(history);
            var expected = Record.Exception(() => IndicatorInputDomain.Finite.Validate(in bars[^1]));
            var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(actual);
            Assert.Equal(expected!.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(builder.LastExecution);
        }
    }
}
