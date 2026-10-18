using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedFoldedFisherTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(FoldedRelativeStrengthIndex) || c.IndicatorType == typeof(FisherTransformStochasticOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new FoldedRelativeStrengthIndex(length) : new FisherTransformStochasticOscillator(length);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2) from length in new[] { int.MinValue, 1, 2, 3, 14, 65, 1024 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task AliasPeriodsRetainAllOutputBits(int kind, int length)
    {
        var root = Create(kind, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Repeat(0d, 31).Concat(Enumerable.Repeat(1d, 31)).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task CustomFoldedAverageKeepsFallback()
    {
        var root = new FoldedRelativeStrengthIndex(3, new Ema(2));
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1]));
    }

    [Fact]
    public void PairedAndScalarShapesKeepOrder()
    {
        var folded = new FoldedRelativeStrengthIndex(); Assert.Equal(2, folded.Outputs.Count); Assert.Same(folded.Value, folded.Outputs[0]); Assert.Same(folded.Signal, folded.Outputs[1]);
        Assert.Single(new FisherTransformStochasticOscillator().Outputs);
    }

    [Fact]
    public async Task HugeFoldedPeriodRetainsFullSumAndSignalWithObservedStorage()
    {
        var root = new FoldedRelativeStrengthIndex(int.MaxValue); var bars = ToBars([1, 2, 3, 4, 5, 6]);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits([100, 200, 300, 400, 500, 600], run[root.Outputs[0]].ToArray()); Bits([100, 150, 200, 250, 300, 350], run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task FoldedSumExpiresAtFullPeriod()
    {
        var root = new FoldedRelativeStrengthIndex(2); var bars = ToBars([1, 2, 3, 4]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([100, 200, 200, 200], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task FlatFisherUsesLogisticFloor()
    {
        var root = new FisherTransformStochasticOscillator(1); var bars = ToBars([1, 1, 1, 1]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(Enumerable.Repeat(100 / (1 + Math.Exp(10)), bars.Length).ToArray(), run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task HugeFisherPeriodKeepsFullWeights()
    { await Compare([new FisherTransformStochasticOscillator(int.MaxValue)], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1])); }

    [Fact]
    public async Task LongHistoryCrossesAllWindows()
    { await Compare([new FoldedRelativeStrengthIndex(14), new FisherTransformStochasticOscillator(2)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        IIndicator[] selected = [new FoldedRelativeStrengthIndex(3).Of(new Sma(2)), new FisherTransformStochasticOscillator(3).Of(new Sma(2))];
        Assert.False(ValuesBarExecution.SupportsOwned(selected)); await Compare(selected, bars);
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), repeated]; await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WindowsReplayAfterReset(bool fisher)
    {
        var type = typeof(ValuesBarExecution).GetNestedType(fisher ? "GeneratedFisherStochasticState" : "GeneratedFoldedRsiState", System.Reflection.BindingFlags.NonPublic)!;
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()); object[] args = fisher ? [3] : [3, bars.Length];
        var state = Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[2]; foreach (var bar in bars) { if (state is IMultiOutputState multi) { multi.Update(in bar, outputs); values.AddRange(outputs); } else values.Add(((IIndicatorState)state).Update(in bar)); } return values.ToArray(); }
        try { var expected = Run(); if (state is IMultiOutputState multi) multi.Reset(); else ((IIndicatorState)state).Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
    }

    private static Bar[] ToBars(double[] prices) => prices.Select((v, i) => new Bar(default,
        i % 5 == 0 ? 0 : v / 4, Math.Abs(v), i % 7 == 0 ? 0 : Math.Abs(v) / 2, v, 1)).ToArray();

    private static async Task Compare(IIndicator[] roots, Bar[] bars)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
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
                        if (bars.Length > 0) { Assert.Equal(expected.Latest.Bar, actual.Latest.Bar); Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp); }
                        actual.Dispose();
                        foreach (var root in roots) foreach (var output in root.Outputs)
                            Bits(expected[output].ToArray(), actual[output].ToArray());
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
