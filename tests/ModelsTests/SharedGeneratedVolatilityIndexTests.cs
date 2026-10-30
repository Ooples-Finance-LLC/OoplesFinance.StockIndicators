using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVolatilityIndexTests
{
    public static IEnumerable<object[]> Cases =>
        from chande in new[] { false, true }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, int.MaxValue }
        select new object[] { chande, length };

    private static IIndicator Create(bool chande, int length, double alpha1 = .2, double alpha2 = .04) => chande
        ? new ChandeVolatilityIndexDynamicAverageIndicator(length, alpha1, alpha2)
        : new VolatilityIndexDynamicAverageIndicator(length, alpha1, alpha2);

    [Theory, MemberData(nameof(Cases))]
    public async Task BothRecursiveOutputsMatchLegacy(bool chande, int length)
    {
        var root = Create(chande, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Range(0, 33).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task SinglePriceWindowAndZeroGainsHoldInitialPrice()
    {
        foreach (var chande in new[] { false, true })
        foreach (var root in new[] { Create(chande, 1), Create(chande, 3, 0, -0d), Create(chande, int.MaxValue) })
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([3, 5, -2, 0, 9]))).ConfigureIndicators(root).BuildAsync();
            Assert.False(((IndicatorRun)run).HasLegacyRuntime);
            foreach (var output in root.Outputs) Bits([3, 3, 3, 3, 3], run[output].ToArray());
        }
    }

    [Fact]
    public async Task ExtrapolationAndUnequalGainsPreserveBothOutputs()
    {
        foreach (var chande in new[] { false, true })
        foreach (var gains in new[] { (0d, 1d), (1d, 0d), (2d, .5), (double.MaxValue, 0d) })
            await Compare([Create(chande, 2, gains.Item1, gains.Item2)], ToBars([1, 3, -2, 4, 0, 2]));
    }

    [Fact]
    public async Task MixedRootsComponentsAndInvalidInputsPreserveFallback()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4]);
        var root = Create(false, 3);
        IIndicator[] roots = [root, Create(true, 3), new RelativeVolatilityIndex(3), root];
        await Compare(roots, bars);
        foreach (var fallback in new IIndicator[]
        {
            new VolatilityIndexDynamicAverageIndicator(3, maType: new Sma(3)),
            new ChandeVolatilityIndexDynamicAverageIndicator(3, maType: new Sma(3)),
            new VolatilityIndexDynamicAverageIndicator(3).Of(new Sma(2)),
            new ChandeVolatilityIndexDynamicAverageIndicator(3).Of(new Sma(2))
        })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([fallback]));
            await Compare([fallback], bars);
        }
        foreach (var chande in new[] { false, true })
        foreach (var bad in new[] { -1d, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var invalid in new[] { Create(chande, 3, bad, .04), Create(chande, 3, .2, bad) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([invalid]));
            await Compare([invalid], bars);
            await Compare([invalid], []);
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
