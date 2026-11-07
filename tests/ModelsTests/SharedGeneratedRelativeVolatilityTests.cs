using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedRelativeVolatilityTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 5)
        from length in new[] { int.MinValue, 0, 1, 2, 5, 14, 4096 }
        select new object[] { family, length };
    private static IIndicator Create(int family, int length) => family switch
    {
        0 => new RelativeVolatilityIndex(length), 1 => new RviVolatility(length),
        2 => new RelativeVolatilityIndexV2(length, length),
        3 => new RelativeVolatilityIndexHigh(length, length),
        _ => new RelativeVolatilityIndexLow(length, length)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactDeviationDirectionsAndVariantSeedsMatchLegacy(int family, int length)
    {
        var root = Create(family, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
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
    public async Task IndependentVariantSeedsAndOriginalHighLowFields()
    {
        var high = new RelativeVolatilityIndexHigh(1, 2); var low = new RelativeVolatilityIndexLow(1, 2);
        var pair = new RelativeVolatilityIndexV2(2, 1); var single = new RelativeVolatilityIndex(2);
        var bars = new[] { 1d, 3, 1, 1 }.Select(v => new Bar(default, 0, v, -v, 0, 1)).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(high, low, pair, single).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits([0, 100, 0, 50], run[high.Value].ToArray());
        Bits([0, 0, 100, 50], run[low.Value].ToArray());
        Bits([100, 50, 50, 100], run[pair.Value].ToArray());
        Bits([100, 100, 100, 100], run[single.Value].ToArray());
    }

    [Fact]
    public async Task UnfillableWindowsRetainDistinctZeroDenominatorRules()
    {
        var bars = Enumerable.Range(1, 4).Select(v => new Bar(default, v, v, -v, v, 1)).ToArray();
        IIndicator[] roots = [new RelativeVolatilityIndex(int.MaxValue), new RviVolatility(int.MaxValue), new RelativeVolatilityIndexV2(int.MaxValue, int.MaxValue), new RelativeVolatilityIndexHigh(2, int.MaxValue), new RelativeVolatilityIndexLow(2, int.MaxValue), new RelativeVolatilityIndexHigh(int.MaxValue, 2)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        foreach (var root in roots[..3]) Bits([100, 100, 100, 100], run[root.Outputs[0]].ToArray());
        foreach (var root in roots[3..5]) Bits([0, 0, 50, 50], run[root.Outputs[0]].ToArray());
        Bits([0, 0, 0, 0], run[roots[5].Outputs[0]].ToArray());
        foreach (var root in new IIndicator[] { new RelativeVolatilityIndexV2(2, 4096), new RelativeVolatilityIndexHigh(2, 4096), new RelativeVolatilityIndexLow(4096, 2) })
            await Compare([root], bars);
    }

    [Fact]
    public async Task MixedRootsInvalidTailAndComponentFallback()
    {
        var bars = Enumerable.Range(1, 33).Select(i => new Bar(default, i, i + 2, i - 1, i, 1)).ToArray();
        var first = new RelativeVolatilityIndex(3);
        IIndicator[] roots = [first, new RviVolatility(3), new RelativeVolatilityIndexV2(3, 2), new RelativeVolatilityIndexHigh(2, 3), new RelativeVolatilityIndexLow(3, 2), first];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        foreach (var root in new IIndicator[] { new RelativeVolatilityIndexV2(3, 2, new Sma(2)), new RelativeVolatilityIndex(3).Of(new Sma(2)), new RviVolatility(3).Of(new Sma(2)), new RelativeVolatilityIndexV2(3, 2).Of(new Sma(2)), new RelativeVolatilityIndexHigh(3, 2).Of(new Sma(2)), new RelativeVolatilityIndexLow(3, 2).Of(new Sma(2)) })
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
