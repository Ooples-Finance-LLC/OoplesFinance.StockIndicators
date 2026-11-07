using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedFearFireTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(FearAndGreedIndicator) || c.IndicatorType == typeof(FireflyOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static MultiOutputIndicatorBase Create(int kind, int length) => kind == 0 ? new FearAndGreedIndicator(length, length, length) : new FireflyOscillator(length);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2) from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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
    [InlineData(1, 2, 1)] [InlineData(2, 65, 3)] [InlineData(65, 2, 14)] [InlineData(10, 30, 2)] [InlineData(int.MaxValue, 1, int.MaxValue)]
    public async Task IndependentFearPeriodsKeepBits(int fast, int slow, int smooth)
    { await Compare([new FearAndGreedIndicator(fast, slow, smooth)], ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CustomAverageKeepsFallback(bool firefly)
    {
        IIndicator root = firefly ? new FireflyOscillator(3, new Ema(2)) : new FearAndGreedIndicator(3, 7, 2, new Ema(2));
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1]));
    }

    [Fact]
    public void PairedShapesKeepOrder()
    {
        var fear = new FearAndGreedIndicator(); var firefly = new FireflyOscillator();
        Assert.Equal(2, fear.Outputs.Count); Assert.Equal(2, firefly.Outputs.Count);
        Assert.Same(fear.Value, fear.Outputs[0]); Assert.Same(fear.Signal, fear.Outputs[1]);
        Assert.Same(firefly.Value, firefly.Outputs[0]); Assert.Same(firefly.Signal, firefly.Outputs[1]);
    }

    [Fact]
    public async Task FireflyUnitLengthKeepsFortySixSeed()
    {
        var root = new FireflyOscillator(1); var bars = ToBars([1, 2, -3, 4, 0, 2]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(Enumerable.Repeat(46d, bars.Length).ToArray(), run[root.Outputs[0]].ToArray());
        Bits(Enumerable.Repeat(46d, bars.Length).ToArray(), run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task FearOriginalRangeAndFlatDirectionKeepHandValues()
    {
        var root = new FearAndGreedIndicator(1, 2, 1);
        Bar[] bars = [new(default, 99, 2, 0, 1, 1), new(default, 99, 3, 0, 2, 1), new(default, 99, 100, 0, 2, 1)]; await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 1, -1], run[root.Outputs[0]].ToArray()); Bits([0, 1, -1], run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task FireflySignalIsThreeBarMaximum()
    {
        var root = new FireflyOscillator(10); var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        var line = run[root.Outputs[0]].ToArray();
        Bits(Enumerable.Range(0, line.Length).Select(i => line.Skip(Math.Max(0, i - 2)).Take(Math.Min(i + 1, 3)).Max()).ToArray(), run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesAllWindows()
    { await Compare([new FearAndGreedIndicator(10, 30, 2), new FireflyOscillator(10)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        for (var kind = 0; kind < 2; kind++) { var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), repeated]; await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WindowsReplayAfterReset(bool firefly)
    {
        var type = typeof(ValuesBarExecution).GetNestedType(firefly ? "GeneratedFireflyState" : "GeneratedFearGreedState", System.Reflection.BindingFlags.NonPublic)!;
        object[] args = firefly ? [3] : [3, 7, 2];
        var state = (IMultiOutputState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray());
        double[] Run() { var values = new List<double>(); var outputs = new double[2]; foreach (var bar in bars) { state.Update(in bar, outputs); values.AddRange(outputs); } return values.ToArray(); }
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
