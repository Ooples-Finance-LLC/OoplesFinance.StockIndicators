using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSpectrumRestoreTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersSpectrumDerivedFilterBank) || c.IndicatorType == typeof(EhlersRestoringPullIndicator))
        .Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int minimum, int maximum, int cutoff, int median) => kind == 0
        ? new EhlersSpectrumDerivedFilterBank(minimum, maximum, cutoff, median)
        : new EhlersRestoringPullIndicator(minimum, maximum, cutoff, median);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2)
        from length in new[] { int.MinValue, 1, 2, 20, 65 } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsAndOutputSlotsRetainBits(int kind, int length)
    {
        var root = Create(kind, length, length, length, length); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(1, 7, 2, 3)] [InlineData(12, 3, 1, 2)] [InlineData(3, 12, int.MaxValue, 3)] [InlineData(3, 12, 20, 65)]
    public async Task IndependentBoundsAndCutoffRetainNormalization(int minimum, int maximum, int cutoff, int median)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var kind in Enumerable.Range(0, 2)) await Compare([Create(kind, minimum, maximum, cutoff, median)], bars);
    }

    [Fact]
    public void ExtremeAllocationLimitsRetainLegacyRouting()
    {
        // Do not instantiate the legacy engine's enormous arrays merely to test routing.
        foreach (var kind in Enumerable.Range(0, 2))
        foreach (var root in new[] { Create(kind, int.MaxValue, 12, 20, 3), Create(kind, 3, int.MaxValue, 20, 3), Create(kind, 3, 12, 20, int.MaxValue) })
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
    }

    [Fact]
    public async Task OriginalVolumeScalesPullAndSignal()
    {
        var root = new EhlersRestoringPullIndicator(3, 12, 20, 3);
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(default, 1, 2, 0, Math.Sin(i * .7),
            i % 4 == 0 ? 0 : i % 4 == 1 ? double.Epsilon : i % 4 == 2 ? 2 : -1)).ToArray();
        await Compare([root], bars);
        var zero = bars.Select(bar => new Bar(bar.Time, bar.Open, bar.High, bar.Low, bar.Close, 0)).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(zero)).ConfigureIndicators(root).BuildAsync();
        Assert.All(run[root.Outputs[0]].ToArray(), value => Assert.Equal(0d, value));
    }

    [Fact]
    public async Task SignalStaysInvalidAfterOverflowThenFinitePull()
    {
        var root = new EhlersRestoringPullIndicator(1, 3, 3, 1);
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(default, 1, 2, 0, 1, i == 0 ? double.MaxValue : 1)).ToArray();
        await Compare([root], bars);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedRestoringPullState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 1, 3, 3, 1 }, null)!;
        var pull = new double[bars.Length]; var signal = new double[bars.Length];
        try
        {
            var outputs = new double[2];
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], outputs);
                pull[i] = outputs[0]; signal[i] = outputs[1];
            }
        }
        finally { ((IDisposable)state).Dispose(); }
        Assert.True(double.IsPositiveInfinity(pull[0])); Assert.All(pull.Skip(1), value => Assert.True(double.IsFinite(value)));
        Assert.All(signal, value => Assert.True(double.IsNaN(value)));
    }

    [Fact]
    public async Task ExplicitSignalAverageRetainsComponentPath()
    {
        var bars = ToBars(Enumerable.Range(0, 33).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var average in new IMovingAverage[] { new Sma(3), new Ema(4), new Wma(5) })
        {
            var root = new EhlersRestoringPullIndicator(3, 12, 20, 3, average);
            Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars);
        }
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new EhlersSpectrumDerivedFilterBank(3, 12, 20, 3).Of(new Sma(2)),
            new EhlersRestoringPullIndicator(3, 12, 20, 3).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(1, 3, 12, 20, 3);
        IIndicator[] roots = [repeated, Create(0, 3, 12, 20, 3), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactStatesReplayAfterResetIncludingSignalInvalidation()
    {
        var bars = Enumerable.Range(0, 33).Select(i => new Bar(default, 1, 2, 0, Math.Sin(i * .7), i == 2 ? double.MaxValue : 1)).ToArray();
        foreach (var name in new[] { "GeneratedSpectrumBankState", "GeneratedRestoringPullState" })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { 1, 3, 3, 1 }, null)!;
            double[] Run() {
                var result = new List<double>(); var outputs = new double[2];
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
                }
                return result.ToArray();
            }
            try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); }
            finally { ((IDisposable)state).Dispose(); }
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
