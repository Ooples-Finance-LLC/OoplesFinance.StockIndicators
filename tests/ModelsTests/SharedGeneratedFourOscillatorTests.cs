using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;
public sealed class SharedGeneratedFourOscillatorTests
{
    private static IIndicator Create(int kind, int length) => kind == 0 ? new _4MovingAverageConvergenceDivergence(length) : new _4PercentagePriceOscillator(length);
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
        var root = Create(kind, 14);
        IIndicator selected = kind == 0 ? new _4MovingAverageConvergenceDivergence(14).Of(new Sma(2)) : new _4PercentagePriceOscillator(14).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected]));
        var bars = BarsOf([1, 3, 3, 1, 2]); await Compare([selected], bars);
        IIndicator[] roots = [root, Create(kind, 3), root]; Assert.True(ValuesBarExecution.SupportsOwned(roots)); await Compare(roots, bars);
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            await Compare([root], [new(default, 1, 2, 0, 1, invalid)]);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }
    [Theory, InlineData(0), InlineData(1)]
    public void IndependentOutputsAndReset(int kind)
    {
        var bars = BarsOf([1, 3, 3, 1, 2]);
        var root = Create(kind, int.MaxValue);
        var rules = OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.For((IBuiltInIndicator)root).ToArray(); Assert.NotEmpty(rules);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedFourOscillatorState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [int.MaxValue, 8, 10, 17, kind == 1], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var outputs = new[] { new double[bars.Length], new double[bars.Length], new double[bars.Length], new double[bars.Length], new double[bars.Length], new double[bars.Length] };
            for (var i = 0; i < bars.Length; i++)
            {
                var point = new double[6]; state.Update(in bars[i], point);
                for (var slot = 0; slot < 6; slot++) outputs[slot][i] = point[slot];
            }
            foreach (var rule in rules) rule.Check(new OoplesFinance.StockIndicators.Validation.IndicatorValidationContext("four-reset", bars, outputs, 0));
            state.Reset();
        }
    }
    [Fact]
    public async Task CustomAveragesAndOriginalCandleExtremesKeepContracts()
    {
        var first = new _4MovingAverageConvergenceDivergence(3, 7, maType: new Ema());
        var second = new _4MovingAverageConvergenceDivergence(3, 7, secondAverage: new Ema());
        Assert.False(ValuesBarExecution.SupportsOwned([first])); Assert.False(ValuesBarExecution.SupportsOwned([second]));
        await Compare([first, second, Create(0, 3), Create(1, 3)],
            [new(default, 0, 2, 1, 9, 1), new(default, 0, 4, -1, -8, 1), new(default, 0, 2, 2, 3, 1)]);
    }
    [Theory, InlineData(0), InlineData(1)]
    public async Task DifferentPeriodsAndUnpublishedStagesKeepSixOutputs(int kind)
    {
        IIndicator root = kind == 0 ? new _4MovingAverageConvergenceDivergence(2, 1, 5, 3, int.MaxValue, 0, -3, double.MaxValue)
            : new _4PercentagePriceOscillator(2, 1, 5, 3, int.MaxValue, 0, -3, double.MaxValue);
        await Compare([root], BarsOf([1, 0, -1, double.MaxValue, -double.MaxValue, 2, 3]));
        IIndicator custom = kind == 0 ? new _4MovingAverageConvergenceDivergence(average10Average: new Ema())
            : new _4PercentagePriceOscillator(average10Average: new Ema());
        Assert.False(ValuesBarExecution.SupportsOwned([custom])); await Compare([custom], BarsOf([1, 3, 2]));
    }
    [Theory, InlineData(double.NaN), InlineData(double.PositiveInfinity), InlineData(double.NegativeInfinity)]
    public async Task NonfiniteMultipliersRetainLegacyValidation(double value)
    {
        IIndicator[] roots = [new _4MovingAverageConvergenceDivergence(blueMult: value), new _4PercentagePriceOscillator(yellowMult: value)];
        foreach (var root in roots) Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare(roots, BarsOf([1, 3, 2]));
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
