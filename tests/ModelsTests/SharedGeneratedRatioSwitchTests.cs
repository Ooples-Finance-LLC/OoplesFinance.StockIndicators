using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedRatioSwitchTests
{
    public static IEnumerable<object[]> Cases =>
        from ratio in new[] { false, true }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 14, 4096, int.MaxValue }
        select new object[] { ratio, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactLaggedRangesAndSmoothedDeviationMatchLegacy(bool ratio, int length)
    {
        IIndicator root = ratio ? new VolatilityRatio(length) : new VolatilitySwitchIndicator(length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.Epsilon, double.MaxValue, double.MaxValue, double.Epsilon],
            [-double.MaxValue, -double.Epsilon, 0, double.MaxValue, -1, 1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            [1, 2, 4, 8, 4, 2, 1, 0, -1, -2, -4],
            Enumerable.Range(0, 33).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], prices.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray());
    }

    [Fact]
    public async Task IndependentLaggedPriceAndDegenerateDeviation()
    {
        var ratio = new VolatilityRatio(2); var volatility = new VolatilitySwitchIndicator(1);
        Bar[] bars = [new(default, 1, 2, 0, 10, 1), new(default, 3, 4, 2, 3, 1), new(default, 2, 3, 1, 2, 1), new(default, 4, 5, 3, 4, 1)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(ratio, volatility).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits([0, 4, 1, 1d / 3], run[ratio.Value].ToArray());
        Bits([0, 0, 0, 0], run[volatility.Value].ToArray());
    }

    [Fact]
    public async Task MixedRootsOriginalFieldsInvalidTailAndComponentFallback()
    {
        Bar[] bars = [new(default, 7, 2, 1, 4, 1), new(default, -4, 3, -1, -2, 1), new(default, 0, 0, 0, 1, 1), new(default, 2, -1, 3, 2, 1)];
        var ratio = new VolatilityRatio(3);
        IIndicator[] roots = [ratio, new VolatilitySwitchIndicator(3), new Cci(3), ratio];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        foreach (var root in new IIndicator[] { new VolatilityRatio(3, new Sma(3)), new VolatilitySwitchIndicator(3, new Ema(3)), new VolatilitySwitchIndicator(3, new Ema(3), new Sma(3)), new VolatilityRatio(3).Of(new Sma(2)), new VolatilitySwitchIndicator(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars[..^1]);
        }
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
