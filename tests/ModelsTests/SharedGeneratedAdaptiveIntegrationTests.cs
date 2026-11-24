using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAdaptiveIntegrationTests
{
    private static IIndicator[] Roots()
    {
        var repeated = new EhlersAdaptiveCommodityChannelIndexV1();
        return [
            new EhlersAutoCorrelationIndicator(), new EhlersAutoCorrelationPeriodogram(),
            new EhlersAdaptiveBandPassFilter(), new EhlersZeroCrossingsDominantCycle(),
            new EhlersAdaptiveRelativeStrengthIndexV1(), new EhlersAdaptiveRsiFisherTransformV1(),
            new EhlersAdaptiveStochasticIndicatorV1(), repeated,
            new EhlersAdaptiveRelativeStrengthIndexV2(), new EhlersAdaptiveRsiFisherTransformV2(),
            new EhlersAdaptiveStochasticIndicatorV2(), new EhlersAdaptiveStochasticInverseFisherTransform(),
            new EhlersAdaptiveCommodityChannelIndexV2(), repeated
        ];
    }

    private static Bar[] BarsFor(int count) => Enumerable.Range(0, count).Select(i =>
    {
        var price = Math.Sin(i * .7) + .25 * Math.Sin(i * .11);
        return new Bar(default, price / 2, price + 2, price - 1, price, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)]
    [InlineData(129)]
    public async Task CombinedFamiliesPreserveEverySlotAndOwnedLifecycle(int count)
    {
        var roots = Roots();
        Assert.True(ValuesBarExecution.SupportsOwned(roots));
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(BarsFor(count))).ConfigureIndicators(roots).ConfigureHistory(history);
            using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Builder();
            for (var replay = 0; replay < 2; replay++)
            {
                using var actual = await builder.BuildAsync();
                if (history == IndicatorHistoryMode.Full) Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                foreach (var root in roots)
                    foreach (var output in root.Outputs)
                        Bits(expected[output].ToArray(), actual[output].ToArray());
                if (count > 0)
                {
                    Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                actual.Dispose();
                foreach (var root in roots)
                    foreach (var output in root.Outputs)
                        Bits(expected[output].ToArray(), actual[output].ToArray());
            }
        }
    }

    [Fact]
    public async Task CombinedFamiliesKeepRawValidationAndCancellationPriority()
    {
        var roots = Roots();
        var bars = BarsFor(17);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
            var expected = await Record.ExceptionAsync(async () =>
            {
                using var run = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            });
            var builder = Builder();
            var actual = await Record.ExceptionAsync(async () =>
            {
                using var run = await builder.BuildAsync();
            });
            Assert.NotNull(expected);
            Assert.Equal(expected.GetType(), actual?.GetType());
            Assert.Equal(expected.Message, actual?.Message);
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            Assert.Null(builder.LastExecution);
        }
    }

    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
