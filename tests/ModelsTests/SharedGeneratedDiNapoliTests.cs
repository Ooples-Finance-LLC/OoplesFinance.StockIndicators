using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDiNapoliTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(DiNapoliMovingAverageConvergenceDivergence) || c.IndicatorType == typeof(DiNapoliPercentagePriceOscillator) || c.IndicatorType == typeof(DiNapoliPreferredStochasticOscillator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new DiNapoliMovingAverageConvergenceDivergence(Math.Max(1, length)),
        1 => new DiNapoliPercentagePriceOscillator(length), _ => new DiNapoliPreferredStochasticOscillator(length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 8, 65, 1024 } select new object[] { kind, length };

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
    [InlineData(1, 1, 1)] [InlineData(.25, .5, .75)] [InlineData(3, 1, 1)] [InlineData(1, 3, 7)]
    [InlineData(double.Epsilon, 2, 3)] [InlineData(double.MaxValue, 1, 1)]
    [InlineData(17.5185, 8.3896, 9.0503)] [InlineData(1, 1, double.MaxValue)]
    public async Task IndependentFractionalPeriodsPreserveAllFiveOutputs(double lc, double sc, double sp)
    {
        var root = new DiNapoliMovingAverageConvergenceDivergence(lc, sc, sp); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars([double.MaxValue, -double.MaxValue, 0, 1, -1, double.Epsilon, 2, 3, -2, 0]));
    }

    [Theory]
    [InlineData(0d)] [InlineData(-1d)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public async Task InvalidFractionalPeriodsKeepFallbackErrorPriority(double period)
    {
        foreach (var root in new IIndicator[] { new DiNapoliMovingAverageConvergenceDivergence(lc: period),
            new DiNapoliMovingAverageConvergenceDivergence(sc: period), new DiNapoliMovingAverageConvergenceDivergence(sp: period) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []); await Compare([root], ToBars([1, 2]));
            await Compare([root], [new(default, 1, 2, 0, 1, double.NaN)]);
        }
    }

    [Fact]
    public void OutputCountsAndPrimarySlotsArePreserved()
    {
        for (var kind = 0; kind < 3; kind++)
        {
            var root = Create(kind, 14); Assert.Equal(kind == 0 ? 5 : kind == 1 ? 3 : 2, root.Outputs.Count);
            Assert.Same(root.Outputs[kind == 0 ? 2 : 0], ((IPrimaryOutputIndicator)root).PrimaryOutput);
        }
    }

    [Fact]
    public async Task PercentageAliasIgnoresObsoleteLength()
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => Math.Sin(i * .7)).ToArray());
        var first = new DiNapoliPercentagePriceOscillator(int.MinValue); var second = new DiNapoliPercentagePriceOscillator(int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(first, second).BuildAsync();
        for (var i = 0; i < 3; i++) Bits(run[first.Outputs[i]].ToArray(), run[second.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task PublishedMacdOverflowKeepsStickySignalInvalidity()
    {
        var bars = ToBars(Enumerable.Repeat(double.MaxValue, 12).Concat(new[] { -double.MaxValue, double.MaxValue }).ToArray());
        var root = new DiNapoliMovingAverageConvergenceDivergence(5, 1, 1); await Compare([root], bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root);
        var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
        Assert.Equal(2, error.OutputSlot); Assert.Equal(12, error.BarIndex);
        Assert.Equal(double.NegativeInfinity, error.Value); Assert.Null(builder.LastExecution);

        // The builder rejects nonfinite output; inspect the raw state separately
        // to retain the original overflow and sticky-signal assertions.
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedDiNapoliOscillatorState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 5d, 1d, 1d, false }, null)!;
        var outputs = new double[5];
        for (var i = 0; i < bars.Length; i++)
        {
            state.Update(in bars[i], outputs);
            if (i == 12) Assert.Equal(double.NegativeInfinity, outputs[2]);
            if (i == 13) Assert.True(double.IsNaN(outputs[3]));
        }
    }

    [Fact]
    public async Task HugeStochasticPeriodUsesOnlyObservedExtrema()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var bounded = new DiNapoliPreferredStochasticOscillator(bars.Length); var huge = new DiNapoliPreferredStochasticOscillator(int.MaxValue);
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(bounded).ConfigureBehavior(_ => { }).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(huge).BuildAsync();
        Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
        for (var i = 0; i < 2; i++) Bits(expected[bounded.Outputs[i]].ToArray(), actual[huge.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task LongHistoryKeepsOriginalStochasticRangesAndFractionalSeeds()
    {
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 20 + i, 15 + i % 7, 1 + i % 3, 4 + i % 5, i + 1)).ToArray();
        await Compare([Create(0, 14), Create(1, 14), Create(2, 8)], bars);
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new DiNapoliMovingAverageConvergenceDivergence().Of(new Sma(2)),
            new DiNapoliPercentagePriceOscillator(14).Of(new Sma(2)), new DiNapoliPreferredStochasticOscillator(8).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), Create(2, 3), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ZeroSeededStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 25).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        (string Name, object[] Args, int Slots)[] cases = [ ("GeneratedDiNapoliOscillatorState", [3d, 2d, 4d, false], 5),
            ("GeneratedDiNapoliOscillatorState", [3d, 2d, 4d, true], 3), ("GeneratedDiNapoliStochasticState", [3], 2) ];
        foreach (var item in cases)
        {
            var type = typeof(ValuesBarExecution).GetNestedType(item.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = (IMultiOutputState)Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, item.Args, null)!;
            double[] Run() { var result = new List<double>(); var outputs = new double[item.Slots]; foreach (var bar in bars) { state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
            try { var expected = Run(); state.Reset(); Bits(expected, Run()); } finally { (state as IDisposable)?.Dispose(); }
        }
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
