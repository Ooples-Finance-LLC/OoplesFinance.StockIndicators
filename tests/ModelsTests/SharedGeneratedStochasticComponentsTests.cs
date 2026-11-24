using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedStochasticComponentsTests
{
    private static MultiOutputIndicatorBase Create(int kind, int length) => kind == 0 ? new StochasticCustomOscillator(length) : new StochasticMomentumIndex(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 0, 1, 2, 14, 32 } select new object[] { kind, length };
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
    [Theory, InlineData(0), InlineData(1)]
    public void ResetReplaysTheNumericalWindow(int kind)
    {
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticComponentsState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [14, kind == 0 ? 3 : 8, kind == 0 ? 1 : 5, kind == 0 ? 12 : 5, 40, kind == 1], null)!;
        var state = (IMultiOutputState)disposable;
        double[] Run()
        {
            var values = new List<double>();
            foreach (var bar in BarsOf(Enumerable.Range(0, 40).Select(i => Math.Sin(i * .7)).ToArray()))
            {
                var outputs = new double[2]; state.Update(in bar, outputs); values.AddRange(outputs);
            }
            return values.ToArray();
        }
        var expected = Run(); state.Reset(); Bits(expected, Run());
    }
    [Theory, InlineData(0), InlineData(1)]
    public void OutputSlotsRetainPrimary(int kind)
    {
        var root = Create(kind, 14);
        Assert.Equal(2, root.Outputs.Count); Assert.Same(root.Outputs[0], root.PrimaryOutput);
    }
    [Fact]
    public async Task DistinctPeriodsAndCustomAveragesKeepContracts()
    {
        await Compare([new StochasticMomentumIndex(3, 4, 5, 6)], BarsOf(Enumerable.Range(0, 65).Select(i => 5 + Math.Sin(i * .7)).ToArray()));
        IIndicator[] custom = [new StochasticCustomOscillator(3, new Wma()),
            new StochasticCustomOscillator(3, new Sma(), new Wma(), new Sma()),
            new StochasticMomentumIndex(3, 4, 5, 6, new Wma())];
        foreach (var root in custom)
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], BarsOf([1, 3, 3, 1, 2]));
        }
    }
    [Theory, InlineData(0), InlineData(1)]
    public void MaximumPeriodsKeepIndependentPrefixValuesAndReset(int kind)
    {
        var bars = BarsOf([1, 3, 3, 1, 2]);
        IBuiltInIndicator root = kind == 0 ? new StochasticCustomOscillator(int.MaxValue)
            : new StochasticMomentumIndex(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue);
        var expected = kind == 0
            ? OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.StochasticCustomOutputs(bars, root)
            : OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.StochasticMomentumOutputs(bars, root);
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedStochasticComponentsState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [int.MaxValue, kind == 0 ? 3 : int.MaxValue, kind == 0 ? 1 : int.MaxValue,
                kind == 0 ? 12 : int.MaxValue, bars.Length, kind == 1], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var outputs = new double[2]; state.Update(in bars[i], outputs);
                Bits([expected[kind == 0 ? "Sco" : "Smi"][i], expected["Signal"][i]], outputs);
            }
            state.Reset();
        }
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
