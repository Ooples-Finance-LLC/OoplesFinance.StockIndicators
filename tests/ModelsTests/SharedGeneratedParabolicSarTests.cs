using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedParabolicSarTests
{
    private static IndicatorBase Create(int length) => new ParabolicSar(length);
    public static IEnumerable<object[]> Cases => new[] { int.MinValue, 0, 1, 2, 14, 32, int.MaxValue }
        .Select(length => new object[] { length });
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
        Assert.False(ValuesBarExecution.SupportsOwned([selected])); var bars = BarsOf([1, 3, 3, 1, 2]);
        await Compare([selected], bars);
        IIndicator[] roots = [root, Create(3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Fact]
    public void ResetReplaysTheNumericalWindow()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedParabolicSarState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [.02, .02, .2], null)!;
        var state = (IIndicatorState)disposable;
        double[] Run() => BarsOf([1, 3, 3, 1, 2]).Select(bar => state.Update(in bar)).ToArray();
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    public static IEnumerable<object[]> AccelerationCases => new double[][] {
        [0, 0, 0], [.125, .125, .5], [1, 0, 1],
        [double.MaxValue, double.MaxValue, double.MaxValue],
        [double.MaxValue / 2, double.MaxValue, double.MaxValue], [0, double.MaxValue, double.MaxValue]
    }.Select(values => values.Cast<object>().ToArray());
    [Theory, MemberData(nameof(AccelerationCases))]
    public async Task AccelerationLimitsKeepReversalsAndOutputBits(double start, double increment, double maximum)
    {
        var root = new ParabolicSar(14, start, increment, maximum);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], BarsOf([1, 3, 5, 7, 9, 11, -2, -4, 8, -8, 1]));
        await Compare([root], BarsOf([-double.MaxValue, double.MaxValue, 0, -double.MaxValue, 0, double.MaxValue]));
    }
    [Fact]
    public async Task InvalidParametersKeepLegacyErrors()
    {
        foreach (var values in new double[][] { [double.NaN, .02, .2], [.02, double.PositiveInfinity, .2],
            [.02, .02, double.NegativeInfinity], [-1, .02, .2], [.02, -1, .2], [.3, .02, .2] })
        {
            var root = new ParabolicSar(14, values[0], values[1], values[2]);
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], BarsOf([1, 2, 3]));
        }
    }
    [Fact]
    public async Task OriginalCloseStillUsesTolerantCustomRange()
    {
        var bars = new[] {
            new Bar(default, 0, 2, 1, 10, 1),
            new Bar(default, 0, 2, 1, -10, 1),
            new Bar(default, 0, 2, 1, Math.BitIncrement(2), 1),
            new Bar(default, 0, 2, 1, 2 + 1e-10, 1),
            new Bar(default, 0, 2, 1, 1, 1),
            new Bar(default, 0, 2, 1, 0, 1)
        };
        await Compare([new ParabolicSar()], bars);
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select(v => new Bar(default, v / 2, Math.Abs(v), -Math.Abs(v), v, 1)).ToArray();
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
