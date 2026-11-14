using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedMedianTests
{
    public static IEnumerable<object[]> Cases =>
        from kind in new[] { 0, 1, 2 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 39, int.MaxValue }
        select new object[] { kind, length };

    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new EhlersRecursiveMedianFilter(length),
        1 => new EhlersRecursiveMedianOscillator(length, length, length),
        _ => new EhlersMedianAverageAdaptiveFilter(length)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task OrderedWindowsAndAdaptivePeriodsMatchLegacy(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            [3, 1, 3, 2, 3, 1, 2, 2, 1, 3],
            Enumerable.Range(0, 33).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task ZeroInputKeepsEveryFilterAtZero()
    {
        foreach (var kind in new[] { 0, 1, 2 })
        {
            var root = Create(kind, 3);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(new double[9]))).ConfigureIndicators(root).BuildAsync();
            Assert.False(((IndicatorRun)run).HasLegacyRuntime);
            foreach (var output in root.Outputs) Bits(new double[9], run[output].ToArray());
        }
    }

    [Fact]
    public async Task IgnoredAlphaAndIndependentOscillatorPeriodsRetainLegacyBehavior()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2]);
        foreach (var alpha in new[] { -1d, -0d, .5, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([new EhlersRecursiveMedianFilter(3, alpha)], bars);
        foreach (var root in new IIndicator[] { new EhlersRecursiveMedianOscillator(2, 3, 4), new EhlersRecursiveMedianOscillator(3, 2, 1), new EhlersRecursiveMedianOscillator(int.MaxValue, 2, 3), new EhlersRecursiveMedianOscillator(2, int.MaxValue, 3), new EhlersRecursiveMedianOscillator(2, 3, int.MaxValue) })
            await Compare([root], bars);
        foreach (var threshold in new[] { -double.MaxValue, -1d, -0d, 0d, .002, 1d, double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var root = new EhlersMedianAverageAdaptiveFilter(5, threshold);
            Assert.Equal(double.IsFinite(threshold), ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
            await Compare([root], []);
        }
    }

    [Fact]
    public async Task MixedRootsSelectedSourcesAndInvalidFieldsPreserveContracts()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4]);
        var root = Create(0, 3);
        IIndicator[] roots = [root, Create(1, 3), Create(2, 3), new VolatilityIndexDynamicAverageIndicator(3), root];
        await Compare(roots, bars);
        foreach (var selected in new IIndicator[] { new EhlersRecursiveMedianFilter(3).Of(new Sma(2)), new EhlersRecursiveMedianOscillator(3, 2, 4).Of(new Sma(2)), new EhlersMedianAverageAdaptiveFilter(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([selected]));
            await Compare([selected], bars);
        }
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    private static Bar[] ToBars(double[] prices) => prices.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray();

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
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
