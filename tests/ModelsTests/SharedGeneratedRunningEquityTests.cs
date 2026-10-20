using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedRunningEquityTests
{
    [Theory]
    [InlineData(int.MinValue)] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(14)] [InlineData(65)] [InlineData(1024)] [InlineData(int.MaxValue)]
    public async Task PeriodsRetainBitsErrorsAndOwnership(int length)
    {
        var root = new RunningEquity(length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0d, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, 3],
            [1, 3, 3, 1, 2], Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Fact]
    public void ResetReplaysPreviousPositionAndPartialWindow()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedRunningEquityState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, [2])!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf([1, 3, 3, 1, 2]).Select(bar => state.Update(in bar)).ToArray();
        Bits([0, 2, 2, 0, -1], Run()); state.Reset(); Bits([0, 2, 2, 0, -1], Run());
    }
    [Fact]
    public async Task ComponentAverageAndSelectedPricesRetainLegacyRouting()
    {
        var bars = BarsOf([1, 3, 3, 1, 2, 4, 1]);
        foreach (var root in new IndicatorBase[] { new RunningEquity(2, new Ema(2)), new RunningEquity(2).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
    }
    [Fact]
    public async Task DuplicateRootsErrorsAndCancellationKeepContracts()
    {
        var root = new RunningEquity(2);
        var bars = BarsOf([1, 3, 3, 1, 2]);
        await Compare([root, new RunningEquity(3), root], bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], BarsOf([1, invalid, 2]));
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).ConfigureHistory(history);
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
                        if (bars.Length > 0)
                        {
                            Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                            Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                        }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Assert.Equal(bars.Length, expected[output].ToArray().Length);
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
