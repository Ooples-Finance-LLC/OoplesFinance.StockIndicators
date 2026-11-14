using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedSpectralFiltersTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersCombFilterSpectralEstimate) || c.IndicatorType == typeof(EhlersDominantCycleTunedBypassFilter) || c.IndicatorType == typeof(EhlersAutoCorrelationReversals))
        .Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind switch {
        0 => new EhlersCombFilterSpectralEstimate(length, length),
        1 => new EhlersDominantCycleTunedBypassFilter(length, length, length, length),
        _ => new EhlersAutoCorrelationReversals(length, length, length) };
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 3)
        from length in new[] { int.MinValue, 1, 2, 20, 65, int.MaxValue } select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public async Task SingleBinPeriodsRetainOutputsAndWarmup(int kind, int length)
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
    [InlineData(0, 12, 3, 0)] [InlineData(0, 3, 12, 0)] [InlineData(0, 1, int.MaxValue, 0)]
    [InlineData(1, 3, 12, 1)] [InlineData(1, 12, 3, 2)] [InlineData(1, 3, 12, int.MaxValue)]
    public async Task IndependentBoundsAndCutoffKeepOptionsNormalization(int kind, int first, int second, int cutoff)
    {
        IIndicator root = kind == 0 ? new EhlersCombFilterSpectralEstimate(first, second)
            : new EhlersDominantCycleTunedBypassFilter(first, second, cutoff, 3);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(0)] [InlineData(double.Epsilon)] [InlineData(.3)] [InlineData(1)] [InlineData(double.MaxValue)]
    public async Task NonnegativeBandwidthRetainsOverflowSafeAngle(double bandwidth)
    {
        var root = new EhlersCombFilterSpectralEstimate(12, 3, bandwidth);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(-double.Epsilon)] [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task InvalidBandwidthKeepsOptionErrorAndPriority(double invalid)
    {
        var root = new EhlersCombFilterSpectralEstimate(12, 3, invalid);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []); await Compare([root], ToBars([1, 2, 3]));
        await Compare([Create(1, 12), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Fact]
    public async Task TunedOutputOrderAndPrimaryRemainStable()
    {
        var root = new EhlersDominantCycleTunedBypassFilter(3, 12, 1, 3);
        Assert.Equal(2, root.Outputs.Count);
        Assert.Same(root.V1, root.Outputs[0]);
        Assert.Same(root.V2, root.Outputs[1]);
        Assert.Same(root.V1, root.PrimaryOutput);
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        foreach (var root in new IIndicator[] { new EhlersCombFilterSpectralEstimate(12, 3).Of(new Sma(2)),
            new EhlersDominantCycleTunedBypassFilter(3, 12, 20, 3).Of(new Sma(2)), new EhlersAutoCorrelationReversals(12, 3, 1).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = new EhlersCombFilterSpectralEstimate(12, 3);
        IIndicator[] roots = [repeated, new EhlersDominantCycleTunedBypassFilter(3, 12, 20, 3), new EhlersAutoCorrelationReversals(12, 3, 1), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task ReversalExplicitAveragesRetainComponentPath()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        foreach (var average in new IMovingAverage[] { new Sma(3), new Ema(4), new Wma(5) })
        {
            var root = new EhlersAutoCorrelationReversals(12, 3, 1, average);
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
        }
        foreach (var lag in new[] { 1, 3, 12, int.MaxValue }) await Compare([new EhlersAutoCorrelationReversals(12, 3, lag)], bars);
    }

    [Fact]
    public void ExactStatesReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 53).Select(i => Math.Sin(2 * Math.PI * i / 7)).ToArray());
        foreach (var config in new[] {
            (Name: "GeneratedCombSpectrumState", Arguments: new object[] { 12, 3, .3 }),
            (Name: "GeneratedTunedBypassState", Arguments: new object[] { 3, 12, 20, 3 }),
            (Name: "GeneratedAutocorrelationReversalState", Arguments: new object[] { 12, 3, 1 }) })
        {
            var type = typeof(ValuesBarExecution).GetNestedType(config.Name, System.Reflection.BindingFlags.NonPublic)!;
            var state = Activator.CreateInstance(type,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null, config.Arguments, null)!;
            double[] Run() {
                var result = new List<double>(); var outputs = new double[2];
                foreach (var bar in bars) {
                    if (state is IIndicatorState scalar) result.Add(scalar.Update(in bar));
                    else { ((IMultiOutputState)state).Update(in bar, outputs); result.AddRange(outputs); }
                }
                return result.ToArray();
            }
            try { var expected = Run(); if (state is IIndicatorState scalar) scalar.Reset(); else ((IMultiOutputState)state).Reset(); Bits(expected, Run()); }
            finally { (state as IDisposable)?.Dispose(); }
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
