using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVolatilityWaveTests
{
    [Theory]
    [InlineData(int.MinValue)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task ExactPowerMeansMatchLegacy(int length)
    {
        var root = new VolatilityWaveMovingAverage(length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, 0, 1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2],
            [1, 2, 4, 8, 4, 2, 1, 0, -1]
        }) await Compare([root], prices.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray());
    }

    [Fact]
    public async Task UnitLengthRetainsMinimumTwoBarSmoothing()
    {
        var root = new VolatilityWaveMovingAverage(1);
        double[] prices = [1, 3, -2, 0, double.Epsilon];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(prices.Select(v => new Bar(default, v, v, v, v, 1)).ToArray())).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        // The two weighted means retain their minimum period of two.
        // Their combined response is (8*x + 2*previous - older)/9.
        Bits([8d / 9, 26d / 9, -11d / 9, -7d / 9, 2d / 9], run[root.Value].ToArray());
    }

    [Fact]
    public async Task MixedRootsFallbackAndErrorPriority()
    {
        var bars = Enumerable.Range(1, 9).Select(i => new Bar(default, i, i + 2, i - 1, i, 1)).ToArray();
        var wave = new VolatilityWaveMovingAverage(3);
        IIndicator[] roots = [wave, new VolatilityIndexDynamicAverageIndicator(3), new ChandeVolatilityIndexDynamicAverageIndicator(3), wave];
        await Compare(roots, bars);
        foreach (var root in new IIndicator[] { new VolatilityWaveMovingAverage(3, new Sma(2)), new VolatilityWaveMovingAverage(3, new Sma(2), new Ema(2)), new VolatilityWaveMovingAverage(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
        }
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var input = bars.ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(roots).ConfigureHistory(history);
            IIndicatorRun? expected = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    IIndicatorRun? actual = null;
                    try
                    {
                        var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                        Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                        Assert.Equal(expectedError?.Message, actualError?.Message);
                        if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                        Assert.NotNull(expected); Assert.NotNull(actual);
                        if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned(roots))
                            Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                        if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
                    }
                    finally { actual?.Dispose(); }
                }
            }
            finally { expected?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
