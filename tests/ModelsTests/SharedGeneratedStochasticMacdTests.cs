using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedStochasticMacdTests
{
    private static StochasticMacdOscillator Create(int length) => new StochasticMacdOscillator(length);
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
    [Theory, InlineData(1), InlineData(14), InlineData(int.MaxValue)]
    public void IndependentOutputsAndReset(int length)
    {
        var bars = BarsOf([1, 3, 3, 1, 2]);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticMacdState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [length, bars.Length], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var outputs = new[] { new double[bars.Length], new double[bars.Length], new double[bars.Length] };
            for (var i = 0; i < bars.Length; i++)
            {
                var point = new double[3]; state.Update(in bars[i], point);
                for (var slot = 0; slot < 3; slot++) outputs[slot][i] = point[slot];
            }
            var rules = OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.For(new StochasticMacdOscillator(length)).ToArray();
            Assert.NotEmpty(rules);
            foreach (var rule in rules)
                rule.Check(new OoplesFinance.StockIndicators.Validation.IndicatorValidationContext("stochastic-macd-reset", bars, outputs, 0));
            state.Reset();
        }
    }
    [Fact]
    public void AdjustedRangesForgetPriorClosesOnReset()
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticMacdState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, [32, 28], null)!;
        var state = (IMultiOutputState)disposable;
        // After 26 constant bars seed both means, the next bar gives
        // EMA(12)=405 and EMA(26)=377 exactly. Keep the initial range in
        // the window so a stale pre-reset close would change this result.
        // The adjusted [351,702] range gives 280/351; EMA(9) gives one fifth.
        var line = 280d / 351; var signal = line / 5;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var first = new Bar(default, 0, 1, 1, 351, 1);
            var second = new Bar(default, 0, 1, 1, 702, 1);
            var distant = new Bar(default, 0, 1, 1, 10000, 1);
            var outputs = new double[3];
            for (var warmup = 0; warmup < 26; warmup++)
            {
                state.Update(in first, outputs); Bits([0, 0, 0], outputs);
            }
            state.Update(in second, outputs); Bits([line, signal, line - signal], outputs);
            state.Update(in distant, outputs);
            state.Reset();
        }
    }
    [Fact]
    public async Task ZeroAndTiedPricesKeepAllOutputs()
        => await Compare([new StochasticMacdOscillator(1), new StochasticMacdOscillator(3)], BarsOf([0, 0, 1, 1, -1, -1, 0]));
    [Fact]
    public async Task NarrowRangesAndOverflowPreserveSignalFailurePolicy()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(default, 0, double.Epsilon, 0, i % 2 == 0 ? double.MaxValue : -double.MaxValue, 1)).ToArray();
        await Compare([new StochasticMacdOscillator(2)], bars);
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
