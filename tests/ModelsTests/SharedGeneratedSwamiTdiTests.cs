using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSwamiTdiTests
{
    private static IIndicator Create(int operation, int length) => operation == 0 ? new SwamiStochastics(length, 48) : new TradersDynamicIndex(length, 7, 2, 3);
    public static IEnumerable<object[]> Cases => from operation in new[] { 0, 1 } from length in new[] { int.MinValue, 0, 1, 2, 14, 32, int.MaxValue } select new object[] { operation, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int operation, int length)
    {
        var root = Create(operation, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory, InlineData(0), InlineData(1)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int operation)
    {
        var root = Create(operation, 14); IIndicator selected = Create(operation, 14) switch { IndicatorBase single => single.Of(new Sma(2)), MultiOutputIndicatorBase multi => multi.Of(new Sma(2)), _ => throw new InvalidOperationException() };
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(operation, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Fact]
    public void SwamiFixedRangeKeepsExactRecurrenceAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedSwamiState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [1, 3], null)!;
        var state = (IIndicatorState)disposable;
        // The range position is exactly 4/5 on every bar. The retained exact
        // recurrence gives 4/25, 36/125 and 244/625 before binary64 publication.
        double[] expected = [.16, .288, .3904];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            foreach (var value in expected)
            {
                var bar = new Bar(default, 8, 10, 0, 8, 1);
                Bits([value], [state.Update(in bar)]);
            }
            state.Reset();
        }
    }
    [Fact]
    public void TdiLengthOneKeepsIndependentBandsAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTradersDynamicState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [1, 1, 1, 1], null)!;
        var state = (IMultiOutputState)disposable;
        double[] prices = [1, 2, 1, 1], expected = [100, 100, 0, 100];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                var bar = new Bar(default, prices[i], prices[i], prices[i], prices[i], 1);
                var outputs = new double[5]; state.Update(in bar, outputs);
                Bits(Enumerable.Repeat(expected[i], 5).ToArray(), outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public async Task CustomAverageRetainsFallback()
    {
        var root = new TradersDynamicIndex(3, 5, 2, 7, new Sma());
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], BarsOf([1, 3, 2, 4]));
    }
    [Fact]
    public async Task DistinctPeriodsPreserveOriginalCandlesAndVolumes()
    {
        await Compare([new SwamiStochastics(3, 7), new TradersDynamicIndex(7, 3, 2, 4)],
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
