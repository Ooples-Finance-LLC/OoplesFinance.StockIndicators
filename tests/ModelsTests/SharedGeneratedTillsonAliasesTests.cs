using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTillsonAliasesTests
{
    private static IndicatorBase Create(int kind, int length) => kind == 0 ? new T3(length) : new TillsonT3MovingAverage(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 }
        from length in new[] { int.MinValue, 0, 1, 2, 14, 32, int.MaxValue } select new object[] { kind, length };
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
    [Theory, InlineData(0), InlineData(1)]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts(int kind)
    {
        var root = Create(kind, 14); var selected = Create(kind, 14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(kind, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots));
        await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(1, .7), InlineData(14, -1d), InlineData(int.MaxValue, .7), InlineData(3, double.MaxValue), InlineData(3, double.Epsilon)]
    public void IndependentCoefficientsAndReset(int length, double factor)
    {
        var bars = BarsOf([1, 3, 3, 1, 2]);
        var expected = OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.TillsonOutputs(bars, new TillsonT3MovingAverage(length, factor))["T3"];
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedTillsonAliasState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [length, factor], null)!;
        var state = (IIndicatorState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            Bits(expected, bars.Select(bar => state.Update(in bar)).ToArray()); state.Reset();
        }
    }
    [Fact]
    public async Task CustomAveragesRetainFallback()
    {
        IIndicator[] roots = [new T3(3, new Sma()), new TillsonT3MovingAverage(3, .7, new Wma())];
        foreach (var root in roots) Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare(roots, BarsOf([1, 3, 3, 1, 2]));
    }
    [Theory, InlineData(double.NaN), InlineData(double.PositiveInfinity), InlineData(double.NegativeInfinity)]
    public async Task InvalidFactorsRetainLegacyErrors(double factor)
    {
        var root = new TillsonT3MovingAverage(3, factor);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], BarsOf([1, 3, 2]));
    }
    [Theory, InlineData(-1d), InlineData(0d), InlineData(1d), InlineData(double.MaxValue), InlineData(double.Epsilon)]
    public async Task FiniteFactorsPreserveOutputBits(double factor) =>
        await Compare([new TillsonT3MovingAverage(3, factor)], BarsOf([1, 3, 3, 1, 2]));
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
