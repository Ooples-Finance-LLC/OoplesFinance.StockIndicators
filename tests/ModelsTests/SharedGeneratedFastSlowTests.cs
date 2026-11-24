using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedFastSlowTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(FastSlowKurtosisOscillator) || c.IndicatorType == typeof(FastandSlowKurtosisOscillator) || c.IndicatorType == typeof(FastSlowRsiOscillator) || c.IndicatorType == typeof(FastSlowStochasticOscillator) || c.IndicatorType == typeof(FastSlowDegreeOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static MultiOutputIndicatorBase Create(int kind, int length) => kind switch { 0 => new FastSlowKurtosisOscillator(length), 1 => new FastandSlowKurtosisOscillator(length), 2 => new FastSlowRsiOscillator(length), 3 => new FastSlowStochasticOscillator(length), _ => new FastSlowDegreeOscillator(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 5) from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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

    [Theory]
    [InlineData(0d)] [InlineData(-1d)] [InlineData(1d)] [InlineData(double.MaxValue)] [InlineData(double.Epsilon)]
    public async Task FiniteRatiosKeepWideRecurrence(double ratio)
    { var root = new FastandSlowKurtosisOscillator(3, ratio); Assert.True(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1])); }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public async Task InvalidRatiosKeepFallbackErrors(double ratio)
    { var root = new FastandSlowKurtosisOscillator(3, ratio); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, 3])); }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CustomAverageKeepsFallback(bool degree)
    {
        IIndicator root = degree ? new FastSlowDegreeOscillator(3, new Ema(2)) : new FastandSlowKurtosisOscillator(3, .03, new Ema(2));
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1]));
    }

    [Theory]
    [InlineData(2)] [InlineData(3)]
    public async Task CompositeAliasesPreserveIgnoredLength(int kind)
    {
        var a = Create(kind, 1); var z = Create(kind, int.MaxValue); var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray());
        await Compare([a, z], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(a, z).BuildAsync();
        for (var i = 0; i < a.Outputs.Count; i++) Bits(run[a.Outputs[i]].ToArray(), run[z.Outputs[i]].ToArray());
    }

    [Fact]
    public void OutputShapesKeepOrder()
    {
        for (var kind = 0; kind < 5; kind++) Assert.Equal(kind == 4 ? 3 : 2, Create(kind, 3).Outputs.Count);
        var degree = new FastSlowDegreeOscillator(); Assert.Same(degree.Value, degree.Outputs[0]); Assert.Same(degree.Signal, degree.Outputs[1]); Assert.Same(degree.Histogram, degree.Outputs[2]);
        var kurtosis = new FastandSlowKurtosisOscillator(); Assert.Same(kurtosis.Value, kurtosis.Outputs[0]); Assert.Same(kurtosis.Signal, kurtosis.Outputs[1]);
    }

    [Fact]
    public async Task OriginalRangeAndSeedsKeepHandValues()
    {
        var stochastic = new FastSlowStochasticOscillator(1); var rsi = new FastSlowRsiOscillator(1); var kurtosis = new FastSlowKurtosisOscillator(); var degree = new FastSlowDegreeOscillator();
        IIndicator[] roots = [stochastic, rsi, kurtosis, degree]; Bar[] bars = [new(default, 99, 2, 0, 1, 1)]; await Compare(roots, bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).BuildAsync();
        Bits([10], run[stochastic.Outputs[0]].ToArray()); Bits([2], run[stochastic.Outputs[1]].ToArray()); Bits([100], run[rsi.Outputs[0]].ToArray());
        Bits([0], run[kurtosis.Outputs[0]].ToArray()); Bits([0], run[degree.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesAllWindows()
    { await Compare(Enumerable.Range(0, 5).Select(i => Create(i, 11)).ToArray(), ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        for (var kind = 0; kind < 5; kind++) { var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), Create(2, 3), Create(3, 3), Create(4, 3), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void SharedStatesReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray());
        var name = kind == 0 ? "GeneratedFastSlowKurtosisState" : kind == 3 ? "GeneratedFastSlowDegreeState" : "GeneratedFastSlowCompositeState";
        object[] args = kind == 0 ? [3, .03] : kind == 3 ? [11] : [kind == 1];
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[kind == 3 ? 3 : 2]; foreach (var bar in bars) { state.Update(in bar, outputs); values.AddRange(outputs); } return values.ToArray(); }
        try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { ((IDisposable)state).Dispose(); }
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
