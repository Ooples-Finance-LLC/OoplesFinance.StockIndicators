using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedNaturalLiquidTests
{
    private static IndicatorBase Create(int kind, int length) => new LiquidRelativeStrengthIndex(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 1 } from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { kind, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Theory, InlineData(1)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int kind)
    {
        var root = Create(kind, 14); var selected = Create(kind, 14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected])); var bars = BarsOf([1, 3, 3, 1, 2]);
        await Compare([selected], bars);
        IIndicator[] roots = [root, Create(kind, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(1)]
    public void ResetReplaysTheNumericalWindow(int kind)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedNaturalLiquidState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, new object[] { 14 }, null)!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf([1, 3, 3, 1, 2]).Select(bar => state.Update(in bar)).ToArray();
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    [Fact]
    public async Task LiquidMaximumPeriodRetainsTheRecurrence()
    {
        await Compare([new LiquidRelativeStrengthIndex(int.MaxValue)], BarsOf([1, 3, 3, 1, 2]));
    }
    [Fact]
    public async Task LiquidVolumeTransitionsKeepEveryBit()
    {
        foreach (var volumes in new double[][] { [0, 0, 0, 0, 0], [1, -1, 0, 2, -2], [double.MaxValue, double.MaxValue, -double.MaxValue, double.Epsilon, 0] })
        {
            var bars = BarsOf([1, 3, 3, 1, 2]).Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, volumes[i])).ToArray();
            await Compare([new LiquidRelativeStrengthIndex(2)], bars);
        }
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select((v, i) => new Bar(default, v / 2, Math.Abs(v), -Math.Abs(v), v, 1 + i % 3)).ToArray();
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
