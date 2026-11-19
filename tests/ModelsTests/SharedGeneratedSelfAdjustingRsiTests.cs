using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSelfAdjustingRsiTests
{
    [Theory, InlineData(int.MinValue), InlineData(0), InlineData(1), InlineData(2), InlineData(14), InlineData(32)]
    public async Task PeriodsKeepBitsErrorsAndOwnership(int length)
    {
        var root = new SelfAdjustingRelativeStrengthIndex(length, 3);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [double.MaxValue, -double.MaxValue, double.MaxValue, 0, 1, 2, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 2, 4, 1],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], BarsOf(prices));
    }
    [Fact]
    public async Task SourcesDuplicatesErrorsAndCancellationKeepContracts()
    {
        var root = new SelfAdjustingRelativeStrengthIndex(14, 3);
        var selected = root.Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        await Compare([root, new SelfAdjustingRelativeStrengthIndex(3, 7), root], bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(1, 1), InlineData(14, 3), InlineData(int.MaxValue, int.MaxValue)]
    public void IndependentPopulationAndReset(int length, int smoothing)
    {
        var bars = BarsOf(Enumerable.Range(0, 40).Select(i => Math.Sin(i * .7)).ToArray());
        var expected = OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.SelfAdjustingRsiOutputs(
            bars, new SelfAdjustingRelativeStrengthIndex(length, smoothing));
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedSelfAdjustingRsiState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [length, smoothing, 2d, bars.Length], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var outputs = new double[4]; state.Update(in bars[i], outputs);
                Bits(new[] { "SaRsi", "Signal", "ObLevel", "OsLevel" }.Select(k => expected[k][i]).ToArray(), outputs);
            }
            state.Reset();
        }
    }
    [Fact]
    public async Task CustomAverageAndIndependentSmoothingKeepContracts()
    {
        var custom = new SelfAdjustingRelativeStrengthIndex(3, 7, 2, new Ema());
        Assert.False(ValuesBarExecution.SupportsOwned([custom]));
        await Compare([custom, new SelfAdjustingRelativeStrengthIndex(3, int.MaxValue)], BarsOf([1, 3, 3, 1, 2]));
    }
    [Theory, InlineData(-1d), InlineData(0d), InlineData(double.MaxValue), InlineData(double.Epsilon),
        InlineData(double.NaN), InlineData(double.PositiveInfinity), InlineData(double.NegativeInfinity)]
    public async Task MultipliersPreserveBitsAndErrors(double multiplier)
    {
        var root = new SelfAdjustingRelativeStrengthIndex(3, 2, multiplier);
        Assert.Equal(double.IsFinite(multiplier), ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], BarsOf([1, 3, 3, 1, 2]));
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
