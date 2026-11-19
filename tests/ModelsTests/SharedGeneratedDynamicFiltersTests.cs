using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDynamicFiltersTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DynamicSupportAndResistance) || c.IndicatorType == typeof(DynamicallyAdjustableFilter) || c.IndicatorType == typeof(DynamicallyAdjustableMovingAverage)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new DynamicSupportAndResistance(length), 1 => new DynamicallyAdjustableFilter(length),
        _ => new DynamicallyAdjustableMovingAverage(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
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

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 5)] [InlineData(5, 2)]
    [InlineData(6, 200)] [InlineData(65, 3)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task IndependentAveragePeriodsKeepVarianceSelection(int fast, int slow)
    {
        await Compare([new DynamicallyAdjustableMovingAverage(fast, slow)], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public void SupportMidpointRemainsThirdAndPrimary()
    {
        var root = new DynamicSupportAndResistance(25); Assert.Equal(3, root.Outputs.Count);
        Assert.Same(root.Outputs[0], root.Support); Assert.Same(root.Outputs[1], root.Resistance);
        Assert.Same(root.Outputs[2], root.Value); Assert.Same(root.Outputs[2], root.PrimaryOutput);
        Assert.Single(new DynamicallyAdjustableFilter().Outputs); Assert.Single(new DynamicallyAdjustableMovingAverage().Outputs);
    }

    [Fact]
    public async Task FirstBarSeedsAndZeroPaddedAverageStayExplicit()
    {
        var support = new DynamicSupportAndResistance(1); var filter = new DynamicallyAdjustableFilter(2); var average = new DynamicallyAdjustableMovingAverage(2, 5);
        Bar[] bars = [new(default, 99, 2, 0, 1, 1)];
        await Compare([support, filter, average], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(support, filter, average).BuildAsync();
        Bits([0], run[support.Outputs[0]].ToArray()); Bits([2], run[support.Outputs[1]].ToArray()); Bits([1], run[support.Outputs[2]].ToArray());
        Bits([1], run[filter.Outputs[0]].ToArray()); Bits([.5], run[average.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task HugeSupportPeriodRetainsMultiplierAndExactMidpoint()
    {
        var root = new DynamicSupportAndResistance(int.MaxValue);
        Bar[] bars = [new(default, 99, 2, 0, 1, 1)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits([1], run[root.Outputs[2]].ToArray());
        // Wilder ATR starts at zero: first range2 / full period, then sqrt(period).
        Assert.InRange(run[root.Outputs[0]].ToArray()[0], 1.999, 2);
        Assert.InRange(run[root.Outputs[1]].ToArray()[0], double.Epsilon, .001);
    }

    [Fact]
    public async Task CustomSupportAverageKeepsFallback()
    {
        var root = new DynamicSupportAndResistance(3, new Ema(3)); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -2, 0, 1, 3, -1, 4]));
    }

    [Fact]
    public async Task LongHistoryCrossesExtremaAndAdaptiveVariance()
    {
        var bars = Enumerable.Range(0, 257).Select(i => new Bar(default, 100 + i, 15 + i % 7, 1 + i % 3, 4 + i % 5, i + 1)).ToArray();
        await Compare([Create(0, 25), Create(1, 14), Create(2, 6)], bars);
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new DynamicSupportAndResistance(3).Of(new Sma(2)), new DynamicallyAdjustableFilter(3).Of(new Sma(2)), new DynamicallyAdjustableMovingAverage(3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 14), Create(2, 6), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void StatesReplayAfterReset(int kind)
    {
        var bars = ToBars([1, 2, 3, 4, -2, 0, 1]);
        var names = new[] { "GeneratedDynamicSupportState", "GeneratedDynamicFilterState", "GeneratedDynamicAverageState" };
        var type = typeof(ValuesBarExecution).GetNestedType(names[kind], System.Reflection.BindingFlags.NonPublic)!;
        object[] args = kind == 0 ? [3, bars.Length] : kind == 1 ? [3] : [2, 5];
        var state = Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
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
