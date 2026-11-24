using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedFractalChaosTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(int.MaxValue)]
    public async Task IgnoredOscillatorPeriodPreservesBitsWarmupOwnershipAndRepeatedBuilds(int period)
    {
        foreach (var scale in new[] { double.Epsilon, 1d, double.MaxValue / 16 })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (var count in new[] { 0, 1, 4, 5, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, -0d,
                (i * 7 % 13) * scale, -(i * 11 % 13) * scale, -0d, i)).ToArray();
            IIndicator[] roots = [new FractalChaosBands(), new FractalChaosOscillator(period)];
            Assert.True(ValuesBarExecution.SupportsOwned(roots));
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators([.. roots, roots[0]]).ConfigureHistory(history);
            using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            var builder = Builder();
            for (var repeat = 0; repeat < 2; repeat++)
            {
                using var actual = await builder.BuildAsync();
                if (history == IndicatorHistoryMode.Full) Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                foreach (var root in roots) foreach (var output in root.Outputs)
                    Bits(expected[output].ToArray(), actual[output].ToArray());
                if (count > 0)
                {
                    Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                actual.Dispose();
                foreach (var root in roots) foreach (var output in root.Outputs)
                    Bits(expected[output].ToArray(), actual[output].ToArray());
            }
        }
    }

    [Fact]
    public async Task FiveRealBarsStrictTiesAndSimultaneousChangesKeepUpperPriority()
    {
        foreach (var tiedNeighbor in new[] { -1, 0, 1, 3, 4 })
        {
            var prices = new[] { 2d, 3, 5, 3, 2 };
            if (tiedNeighbor >= 0) prices[tiedNeighbor] = 5;
            var bars = prices.Select(v => new Bar(default, 0, v, -v, 0, 1)).ToArray();
            var bands = new FractalChaosBands(); var oscillator = new FractalChaosOscillator(999);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(bands, oscillator).BuildAsync();
            var confirmed = tiedNeighbor < 0;
            Bits([0, 0, 0, 0, confirmed ? 5 : 0], run[bands.UpperBand].ToArray());
            Bits([0, 0, 0, 0, confirmed ? -5 : 0], run[bands.LowerBand].ToArray());
            Bits(new double[5], run[bands.Value].ToArray());
            Bits([0, 0, 0, 0, confirmed ? 1 : 0], run[oscillator].ToArray());
        }
    }

    [Fact]
    public async Task SourcesFallbackAndInvalidInputRetainErrorPriorityAndCancellation()
    {
        foreach (IIndicator root in new IIndicator[] { new FractalChaosBands().Of(new Sma(2)), new FractalChaosOscillator(5).Of(new Sma(2)) })
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
        var bars = new[] { new Bar(default, 0, 5, -5, 0, 1), new Bar(default, 0, 0, 0, 0, double.NaN) };
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new FractalChaosBands(), new FractalChaosOscillator(5));
        var expected = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
        var builder = Builder();
        var actual = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.IsType<ArgumentOutOfRangeException>(expected);
        Assert.Equal(expected!.GetType(), actual!.GetType()); Assert.Equal(expected.Message, actual.Message);
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
