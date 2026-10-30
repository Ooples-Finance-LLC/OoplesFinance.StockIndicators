using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDynamicMomentumTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DynamicMomentumIndex) || c.IndicatorType == typeof(DynamicMomentumOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new DynamicMomentumIndex(length)
        : new DynamicMomentumOscillator(length);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2)
        from length in new[] { int.MinValue, 1, 2, 14, 65, 1024 } select new object[] { kind, length };

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
    public async Task IndexIgnoresObsoleteLengthAndKeepsThreeSlots()
    {
        var first = new DynamicMomentumIndex(int.MinValue); var second = new DynamicMomentumIndex(int.MaxValue);
        Assert.Equal(3, first.Outputs.Count); Assert.Same(first.Outputs[0], first.Value);
        Assert.Same(first.Outputs[1], first.Signal); Assert.Same(first.Outputs[2], first.Histogram);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 3, -2, 4, 0]))).ConfigureIndicators(first, second).BuildAsync();
        for (var i = 0; i < 3; i++) Bits(run[first.Outputs[i]].ToArray(), run[second.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task FirstBarKeepsOriginalRangeAndNeutralIndex()
    {
        var index = new DynamicMomentumIndex(14); var oscillator = new DynamicMomentumOscillator(1);
        Bar[] bars = [new(default, 999, 2, 0, 1, 1)];
        await Compare([index, oscillator], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(index, oscillator).BuildAsync();
        Bits([100], run[index.Outputs[0]].ToArray()); Bits([100], run[index.Outputs[1]].ToArray()); Bits([0], run[index.Outputs[2]].ToArray());
        Assert.Single(oscillator.Outputs); Bits([100], run[oscillator.Outputs[0]].ToArray());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ExplicitAverageKeepsFallback(bool index)
    {
        IIndicator root = index ? new DynamicMomentumIndex(3, new Ema(10)) : new DynamicMomentumOscillator(3, new Ema(3));
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public async Task HugeOscillatorPeriodNeverReachesFullMean()
    {
        var root = new DynamicMomentumOscillator(int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 3, -2, 4, 0]))).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime); Bits([0, 0, 0, 0, 0], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesDynamicPeriodsAndRunningExtrema()
    {
        var bars = Enumerable.Range(0, 129).Select(i => new Bar(default, 100 + i, 15 + i % 7, 1 + i % 3, 4 + i % 5, i + 1)).ToArray();
        await Compare([new DynamicMomentumIndex(14), new DynamicMomentumOscillator(10)], bars);
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new DynamicMomentumIndex(14).Of(new Sma(2)), new DynamicMomentumOscillator(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 14), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StatesReplayAfterReset(bool index)
    {
        var bars = ToBars(Enumerable.Range(0, 39).Select(i => Math.Sin(i * .7)).ToArray());
        var name = index ? "GeneratedDynamicMomentumIndexState" : "GeneratedDynamicMomentumOscillatorState";
        var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
        var state = Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, index ? Array.Empty<object>() : new object[] { 3, bars.Length }, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[3]; foreach (var bar in bars) {
            if (state is IMultiOutputState multi) { multi.Update(in bar, outputs); values.AddRange(outputs); }
            else values.Add(((IIndicatorState)state).Update(in bar)); } return values.ToArray(); }
        try { var expected = Run(); if (state is IMultiOutputState multi) multi.Reset(); else ((IIndicatorState)state).Reset(); Bits(expected, Run()); }
        finally { ((IDisposable)state).Dispose(); }
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
