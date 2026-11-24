using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSpearmanTests
{
    private static SpearmanIndicator Create(int length) => new SpearmanIndicator(length, 3);
    public static IEnumerable<object[]> Cases => from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int length)
    {
        var root = Create(length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Fact]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts()
    {
        var root = Create(14); var selected = Create(14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(3, 1), InlineData(int.MaxValue, 1), InlineData(3, int.MaxValue), InlineData(int.MaxValue, int.MaxValue)]
    public void TiedRankPairsAndReset(int length, int signalLength)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedSpearmanState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [length, signalLength, 4], null)!;
        var state = (IMultiOutputState)disposable;
        double[] prices = [1, 1, 2, 1];
        // Sorted-rank pairing gives -1/2 for the rolling triple and -1/3
        // for the unexpired four-item window, with average ranks for ties.
        double[] expected = [0, 0, 100, length == 3 ? -50 : (-1d / 3) * 100];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                var bar = new Bar(default, prices[i], prices[i], prices[i], prices[i], 1);
                var outputs = new double[2]; state.Update(in bar, outputs);
                Bits([expected[i], signalLength == 1 ? expected[i] : 0], outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public async Task CustomAverageRetainsFallback()
    {
        var root = new SpearmanIndicator(3, 7, new Sma());
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], BarsOf([1, 3, 3, 1, 2]));
    }
    [Fact]
    public async Task DistinctPeriodsPreserveOriginalCandlesAndVolumes()
    {
        await Compare([new SpearmanIndicator(3, 7), new SpearmanIndicator(7, 3)],
            BarsOf(Enumerable.Range(0, 65).Select(i => 5 + Math.Sin(i * .7)).ToArray()));
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select(v => new Bar(default, v / 2, Math.Abs(v) + 1, -Math.Abs(v) - 2, v, 1)).ToArray();
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
