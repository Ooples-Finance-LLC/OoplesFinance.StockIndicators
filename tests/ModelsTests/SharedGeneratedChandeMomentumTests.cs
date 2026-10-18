using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedChandeMomentumTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(ChandeMomentumOscillatorAbsolute) || c.IndicatorType == typeof(ChandeMomentumOscillatorAbsoluteAverage) || c.IndicatorType == typeof(ChandeMomentumOscillatorAverage) || c.IndicatorType == typeof(ChandeMomentumOscillatorAverageDisparityIndex) || c.IndicatorType == typeof(ChandeMomentumOscillatorFilter)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new ChandeMomentumOscillatorAbsolute(length),
        1 => new ChandeMomentumOscillatorAbsoluteAverage(length),
        2 => new ChandeMomentumOscillatorAverage(length),
        3 => new ChandeMomentumOscillatorAverageDisparityIndex(length),
        _ => new ChandeMomentumOscillatorFilter(length)
    };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 5)
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
    public async Task AbsoluteOscillatorKeepsFullWarmup()
    {
        var root = new ChandeMomentumOscillatorAbsolute(3); var bars = ToBars([1, 2, 3, 4, 5]);
        await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 0, 0, 100, 100], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task AverageAliasesIgnoreLengthAndKeepZeroOrigin()
    {
        var bars = ToBars([1, 2, -3, 0, 4, -5, 7, 2]);
        for (var kind = 1; kind <= 3; kind++)
        {
            var first = Create(kind, int.MinValue); var second = Create(kind, int.MaxValue);
            await Compare([first, second], bars);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(first, second).BuildAsync();
            Bits(run[first.Outputs[0]].ToArray(), run[second.Outputs[0]].ToArray());
            if (kind <= 2) Assert.Equal(100d, run[first.Outputs[0]].ToArray()[0]);
        }
    }

    [Fact]
    public async Task FilterRetainsRoundedThresholdAndWeightedSignal()
    {
        await Compare([new ChandeMomentumOscillatorFilter(3)], ToBars([0, 3, 0, Math.BitIncrement(3), 0, Math.BitDecrement(3), 0, -3, -6, 1]));
    }

    [Fact]
    public async Task LongHistoryCrossesDisparityPeriods()
    {
        await Compare(Enumerable.Range(0, 5).Select(k => Create(k, 14)).ToArray(), ToBars(Enumerable.Range(0, 401).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task ExplicitAverageKeepsFallback()
    {
        var root = new ChandeMomentumOscillatorFilter(9, new Sma(3)); Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        for (var kind = 0; kind < 5; kind++)
        {
            var original = Create(kind, 14);
            IIndicator root = original is IndicatorBase scalar ? scalar.Of(new Sma(2)) : ((MultiOutputIndicatorBase)original).Of(new Sma(2));
            Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars);
        }
        var repeated = Create(0, 14); var roots = Enumerable.Range(0, 5).Select(k => Create(k, 14)).Concat(new IIndicator[] { repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated }).ToArray();
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedChandeAbsoluteState", Arguments: new object[] { 14, bars.Length }),
            (Name: "GeneratedChandeAverageState", Arguments: new object[] { bars.Length, true }),
            (Name: "GeneratedChandeAverageState", Arguments: new object[] { bars.Length, false }),
            (Name: "GeneratedChandeDisparityState", Arguments: Array.Empty<object>()),
            (Name: "GeneratedChandeFilterState", Arguments: new object[] { 14, bars.Length }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() { var result = new List<double>(); var outputs = new double[2]; foreach (var bar in bars) {
                if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar)); else { ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
            } return result.ToArray(); }
            try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); }
            finally { ((IDisposable)state).Dispose(); }
        }
    }

    [Fact]
    public async Task HugeOwnedPeriodsDoNotAllocateFullLegacyWindows()
    {
        var bars = ToBars([1, 2, 3, 4, 5]);
        var absolute = new ChandeMomentumOscillatorAbsolute(int.MaxValue);
        var filter = new ChandeMomentumOscillatorFilter(int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(absolute, filter).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits(new double[bars.Length], run[absolute.Outputs[0]].ToArray());
        Bits([0, 100, 100, 100, 100], run[filter.Value].ToArray());
        Assert.All(run[filter.Signal].ToArray(), value => Assert.True(double.IsFinite(value)));
    }

    [Fact]
    public void ScalarAndFilterPairKeepOutputShape()
    {
        for (var kind = 0; kind < 4; kind++) Assert.Single(Create(kind, 14).Outputs);
        var filter = new ChandeMomentumOscillatorFilter(); Assert.Equal(2, filter.Outputs.Count);
        Assert.Same(filter.Outputs[0], filter.Value); Assert.Same(filter.Outputs[1], filter.Signal);
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
