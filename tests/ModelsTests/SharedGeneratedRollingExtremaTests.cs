using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedRollingExtremaTests
{
    public static IEnumerable<object[]> Cases =>
        from maximum in new[] { false, true }
        from period in new[] { int.MinValue, 0, 1, 2, 14, 65, int.MaxValue }
        select new object[] { maximum, period };

    private static IndicatorBase Create(bool maximum, int period) =>
        maximum ? new RollingMax(period) : new RollingMin(period);

    [Theory, MemberData(nameof(Cases))]
    public async Task BothHistoriesPreserveLegacyBitsAndOwnership(bool maximum, int period)
    {
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0d, -0d, 0d, -double.Epsilon, double.Epsilon, 0d],
            [double.MaxValue, -double.MaxValue, 0, double.MaxValue, -double.MaxValue],
            [3, 3, 2, 1, 3, 2, 3, 0, 0, 0, -1, 3],
            Enumerable.Range(0, 129).Select(i => (i * 13 % 31 - 15) / 8d).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = ToBars(prices);
            var root = Create(maximum, period);
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(root).ConfigureHistory(history);
            IIndicatorRun? expected = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () =>
                    expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
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
                        Assert.NotNull(expected);
                        Assert.NotNull(actual);
                        if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned([root]))
                            Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
                        var reference = expected[root.Value].ToArray();
                        Bits(reference, actual[root.Value].ToArray());
                        if (bars.Length > 0)
                        {
                            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        }
                        actual.Dispose();
                        Bits(reference, expected[root.Value].ToArray());
                    }
                    finally { actual?.Dispose(); }
                }
            }
            finally { expected?.Dispose(); }
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, int.MaxValue)]
    public async Task OldestTiesAndOriginalCloseMatchIndependentScan(bool maximum, int period)
    {
        double[] prices = [-0d, 0d, -0d, 0d, 2, 2, -2, -2, 0, 1, 1, 0];
        var root = Create(maximum, period);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(prices)))
            .ConfigureIndicators(root).BuildAsync();
        Bits(Scan(prices, period, maximum), run[root.Value].ToArray());
    }

    [Theory]
    [InlineData(false, 3)]
    [InlineData(true, 3)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, int.MaxValue)]
    public void ResetReplaysBothDequeAndPrefixPaths(bool maximum, int period)
    {
        var bars = ToBars([-0d, 0d, 1, -1, 2, -2, 0, 4, -4]);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedRollingCloseExtremeState",
            System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { period, bars.Length, maximum }, null)!;
        double[] Run() => bars.Select(bar => state.Update(in bar)).ToArray();
        var first = Run();
        Bits(Scan(bars.Select(bar => bar.Close).ToArray(), period, maximum), first);
        state.Reset();
        Bits(first, Run());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SourcesDuplicateRootsCancellationAndErrorsKeepContracts(bool maximum)
    {
        var bars = ToBars([3, 1, 4, 1, 5, 9, 2, 6]);
        var selected = Create(maximum, 3).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        await Compare([selected], bars);
        var repeated = Create(maximum, 3);
        IIndicator[] roots = [repeated, Create(!maximum, 2), repeated];
        await Compare(roots, bars);
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
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () =>
                    expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected);
                Assert.NotNull(actual);
                foreach (var root in roots)
                    foreach (var output in root.Outputs)
                        Bits(expected[output].ToArray(), actual[output].ToArray());
            }
            finally { actual?.Dispose(); expected?.Dispose(); }
        }
    }

    private static double[] Scan(double[] prices, int period, bool maximum) =>
        Enumerable.Range(0, prices.Length).Select(i =>
        {
            var first = (int)Math.Max(0L, (long)i - period + 1);
            var best = prices[first];
            for (var j = first + 1; j <= i; j++)
                if (maximum ? prices[j] > best : prices[j] < best) best = prices[j];
            return best;
        }).ToArray();

    private static Bar[] ToBars(double[] prices) => prices.Select((price, i) =>
        new Bar(default, i, 100 + i, -100 - i, price, 1)).ToArray();

    private static void Bits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
