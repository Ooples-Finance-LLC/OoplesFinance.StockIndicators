using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedHampelHarmonicTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(HampelFilter) || c.IndicatorType == typeof(HarmonicMeanMovingAverage)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IndicatorBase Create(int kind, int length) => kind == 0 ? new HampelFilter(length) : new HarmonicMeanMovingAverage(length);
    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { kind, length };

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
    [InlineData(-1d)] [InlineData(0d)] [InlineData(double.MaxValue)] [InlineData(double.Epsilon)]
    public async Task FiniteScalingKeepsExactOutlierDecisions(double factor)
    { var root = new HampelFilter(3, factor); Assert.True(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([0, 1, -2, 4, 0, 2, -1])); }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public async Task NonfiniteScalingKeepsFallbackErrors(double factor)
    { var root = new HampelFilter(3, factor); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, 3])); }

    [Theory] [InlineData(0d)] [InlineData(1d)]
    public async Task RawMadEqualityAndZeroSeedKeepHandValues(double factor)
    {
        var root = new HampelFilter(3, factor); var bars = ToBars([2, 4, 100, 4]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(factor == 0 ? [1, 2, 3, 3.5] : [1, 2.5, 3.25, 3.625], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task HarmonicStartupZeroSkippingAndCancellationKeepHandValues()
    {
        var root = new HarmonicMeanMovingAverage(3); var bars = ToBars([1, 2, 4, 0, -4, 0, 0, 0]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([1, 2, 12d / 7, 8d / 3, 0, -4, -4, 0], run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public async Task HugeHarmonicPeriodKeepsStartupPricesWithoutPeriodSizedStorage()
    {
        var prices = new double[] { -0d, 2, -3, double.Epsilon, 0 }; var root = new HarmonicMeanMovingAverage(int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(prices))).ConfigureIndicators(root).BuildAsync();
        Bits(prices, run[root.Outputs[0]].ToArray());
    }

    [Fact]
    public void OutputsRemainScalar() { Assert.Single(new HampelFilter().Outputs); Assert.Single(new HarmonicMeanMovingAverage().Outputs); }

    [Fact]
    public async Task SeveralWindowExpirationsRetainExactDecisions()
    { await Compare([new HampelFilter(11), new HarmonicMeanMovingAverage(11), new HampelFilter(65), new HarmonicMeanMovingAverage(65)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Theory] [InlineData(0)] [InlineData(1)]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts(int kind)
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = Create(kind, 3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory] [InlineData(0)] [InlineData(1)]
    public void WindowsReplayAfterReset(int kind)
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType(kind == 0 ? "GeneratedHampelState" : "GeneratedHarmonicState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IIndicatorState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, kind == 0 ? new object[] { 3, 3d } : new object[] { 3, bars.Length }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) values.Add(state.Update(in bar)); return values.ToArray(); }
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
