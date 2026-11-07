using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSvamaProjectedTests
{
    private static IIndicator Create(int operation, int length) => operation == 0 ? new Svama(length) : new ProjectedSupportAndResistance(length);
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
    public void VolumeWeightsKeepIndependentValuesAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedSvamaState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type, nonPublic: true)!;
        double[] prices = [2, 4, 8, 0], volumes = [4, 2, 0, 4], expected = [2, 3, 3, 0];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < prices.Length; i++)
            {
                var bar = new Bar(default, prices[i], prices[i], prices[i], prices[i], volumes[i]);
                Bits([expected[i]], [state.Update(in bar)]);
            }
            state.Reset();
        }
    }
    [Fact]
    public void ProjectedBandsKeepIndependentValuesAndReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedProjectedLevelsState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [2], null)!;
        var state = (IMultiOutputState)disposable;
        Bar[] bars = [new(default, 6, 8, 4, 6, 1), new(default, 4, 6, 2, 4, 1), new(default, 2, 4, 0, 2, 1)];
        double[][] expected = [[3, 2, 9, 10, 6], [.5, -1, 9.5, 11, 5], [-1.5, -3, 7.5, 9, 3]];
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var outputs = new double[5]; state.Update(in bars[i], outputs);
                Bits(expected[i], outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public async Task DistinctPeriodsPreserveOriginalCandlesAndVolumes()
    {
        await Compare([new Svama(3), new ProjectedSupportAndResistance(7)],
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
