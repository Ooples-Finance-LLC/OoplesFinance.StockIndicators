using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedReturnVolatilityTests
{
    public static IEnumerable<object[]> Cases =>
        from downside in new[] { false, true }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096 }
        select new object[] { downside, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactReturnWindowsMatchLegacy(bool downside, int length)
    {
        IIndicator root = downside ? new DownsideDeviation(length) : new CloseToCloseVolatility(length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.Epsilon, double.MaxValue, double.MaxValue, double.Epsilon],
            [-double.MaxValue, -double.Epsilon, 0, double.MaxValue, -1, 1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            [1, 2, 4, 8, 4, 2, 1, 0, -1, -2, -4],
            Enumerable.Range(0, 45).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], prices.Select(v => new Bar(default, v / 4, v, v / 2, v, 1)).ToArray());
    }

    [Fact]
    public async Task UnfillableWindowsAndDifferentStartupCounts()
    {
        IIndicator[] roots = [new CloseToCloseVolatility(int.MaxValue), new DownsideDeviation(int.MaxValue)];
        var bars = new[] { 1d, 2, 1, 4 }.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        foreach (var root in roots) Bits([0, 0, 0, 0], run[root.Outputs[0]].ToArray());
        var close = new CloseToCloseVolatility(2); var downside = new DownsideDeviation(2);
        using var startup = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(close, downside).BuildAsync();
        Assert.Equal(Math.Log(2) / 2 * Math.Sqrt(252), startup[close.Value][1]);
        Assert.Equal(0, startup[downside.Value][1]);
        Assert.Equal(.5, startup[downside.Value][2]);
    }

    [Fact]
    public async Task MixedRootsInvalidTailAndSourceFallback()
    {
        var bars = Enumerable.Range(1, 33).Select(i => new Bar(default, i, i + 2, i - 1, i, 1)).ToArray();
        var close = new CloseToCloseVolatility(3);
        IIndicator[] roots = [close, new DownsideDeviation(3), new Cci(3), close];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        foreach (var root in new IIndicator[] { new CloseToCloseVolatility(3).Of(new Sma(2)), new DownsideDeviation(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars[..^1]);
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
