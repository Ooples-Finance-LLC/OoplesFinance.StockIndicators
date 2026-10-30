using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedLaggedReturnsTests
{
    private static IndicatorBase Create(int kind, int length) => kind == 0 ? new SimpleReturns(length) : new LogReturns(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 }
        from length in new[] { int.MinValue, 0, 1, 2, 14, 65, 1024 } select new object[] { kind, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndHistoriesKeepBitsAndOwnership(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, 0, 1, double.Epsilon, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 8],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task MaximumPeriodHasBoundedZeroWarmup(int kind)
    {
        var root = Create(kind, int.MaxValue);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(BarsOf([1, 2, 4])))
                .ConfigureIndicators(root).ConfigureHistory(history).BuildAsync();
            Bits([0, 0, 0], run[root.Value].ToArray());
        }
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public void ResetReplaysLaggedArithmetic(int kind)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedLaggedReturnState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic, null, [2, 5, kind == 1], null)!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf([1, 2, 4, 8, 16]).Select(bar => state.Update(in bar)).ToArray();
        var expected = kind == 0 ? new double[] { 0, 0, 3, 3, 3 } : [0, 0, Math.Log(4), Math.Log(4), Math.Log(4)];
        Bits(expected, Run()); state.Reset(); Bits(expected, Run());
    }
    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task SelectedSourcesDuplicateRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var selected = Create(kind, 2).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 2, 4, 8, 16]);
        await Compare([selected], bars);
        var root = Create(kind, 2);
        await Compare([root, Create(1 - kind, 2), root], bars);
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
