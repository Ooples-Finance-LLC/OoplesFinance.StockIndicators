using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedObvFollowupsTests
{
    private static MultiOutputIndicatorBase Create(int kind, int length) => kind == 0
        ? new OnBalanceVolumeModified(length, 3) : new OnBalanceVolumeReflex(length, 3);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2) from length in new[] { int.MinValue, 0, 1, 2, 14, 32, int.MaxValue } select new object[] { kind, length };
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
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedObvFollowupState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [14, 3, kind == 1], null)!;
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
        await Compare([new OnBalanceVolumeModified(3, 7), new OnBalanceVolumeReflex(4, 8)],
            BarsOf(Enumerable.Range(0, 65).Select(i => 5 + Math.Sin(i * .7)).ToArray()));
        IIndicator[] custom = [new OnBalanceVolumeModified(3, 7, new Wma()), new OnBalanceVolumeReflex(3, 7, new Ema())];
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
        var expected = kind == 0
            ? OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.ModifiedObvOutputs(bars, int.MaxValue, int.MaxValue, 3)
            : OoplesFinance.StockIndicators.Validation.BuiltInFormulaReferences.ObvReflexOutputs(bars, new OnBalanceVolumeReflex(int.MaxValue, int.MaxValue));
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedObvFollowupState", System.Reflection.BindingFlags.NonPublic)!;
        using var disposable = (IDisposable)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, [int.MaxValue, int.MaxValue, kind == 1], null)!;
        var state = (IMultiOutputState)disposable;
        for (var repeat = 0; repeat < 2; repeat++)
        {
            for (var i = 0; i < bars.Length; i++)
            {
                var outputs = new double[2]; state.Update(in bars[i], outputs);
                Bits([expected[kind == 0 ? "Obvm" : "Obvr"][i], expected["Signal"][i]], outputs);
            }
            state.Reset();
        }
    }
    [Theory, InlineData(0), InlineData(1)]
    public async Task SignedAndExtremeVolumesKeepCancellation(int kind)
    {
        var volumes = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 0, 1, -1 };
        var bars = Enumerable.Range(0, 60).Select(i => new Bar(default, 1, 4, -4, i % 3 - 1, volumes[i % volumes.Length])).ToArray();
        await Compare([Create(kind, 3)], bars);
    }
    private static Bar[] BarsOf(double[] prices) => prices.Select((v, i) => new Bar(default, v / 2, Math.Abs(v) + 1, -Math.Abs(v) - 2, v, i % 7 + 1)).ToArray();
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
