using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedMacZVwapTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(MacZVwapIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => new MacZVwapIndicator(length);
    public static IEnumerable<object[]> Cases => new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue }.Select(length => new object[] { 0, length });

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

    public static IEnumerable<object[]> OtherPeriods => from slot in new[] { 0, 1, 2, 3 } from length in new[] { int.MinValue, 1, 2, 3, 14, 65, int.MaxValue } select new object[] { slot, length };
    [Theory, MemberData(nameof(OtherPeriods))]
    public async Task SlowSignalVolumeAndDeviationPeriodsRemainIndependent(int slot, int length)
    { await Compare([new MacZVwapIndicator(3, slot == 0 ? length : 3, slot == 1 ? length : 2, slot == 2 ? length : 2, slot == 3 ? length : 2)], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Theory] [InlineData(-2)] [InlineData(-0d)] [InlineData(0)] [InlineData(.02)] [InlineData(1)] [InlineData(2)] [InlineData(double.MaxValue)]
    public async Task GammaRetainsExactSpecialCasesAndRecursiveFilter(double gamma)
    { var root = new MacZVwapIndicator(1, 2, 2, 2, 2, gamma); Assert.True(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Theory] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public async Task NonfiniteGammaKeepsFallbackFailure(double gamma)
    { var root = new MacZVwapIndicator(gamma: gamma); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, 1])); }
    [Fact]
    public async Task CustomAverageKeepsFallback()
    { var root = new MacZVwapIndicator(2, 3, 2, 2, 2, maType: new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, -1])); }
    [Theory] [InlineData(2)] [InlineData(-2)]
    public async Task ZeroVolumeRetainsFiniteFilterHand(double price)
    {
        var root = new MacZVwapIndicator(1, 2, 2, 2, 2, 0); var bars = Enumerable.Repeat(new Bar(default, price, price, price, price, 0), 5).ToArray(); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        var sign = Math.Sign(price); Bits(new[] { 0d, sign / 6d, sign / 2d, sign * 5d / 6, (double)sign }, run[root.Value].ToArray());
    }
    [Fact]
    public async Task SignedVolumeCancellationKeepsOriginalWeights()
    {
        double[] prices = [2, 4, -1, 5, 0, 3, -2]; double[] volumes = [1, -1, 0, 2, -2, double.Epsilon, double.MaxValue];
        var bars = prices.Select((p, i) => new Bar(default, p / 2, Math.Abs(p), -Math.Abs(p), p, volumes[i])).ToArray();
        await Compare([new MacZVwapIndicator(1, 2, 2, 2, 3, 0)], bars);
    }
    [Fact]
    public async Task UnitGammaRetainsSeedWhileInputChanges()
    {
        var root = new MacZVwapIndicator(1, 2, 2, 2, 2, 1); var bars = ToBars([2, 4, -1, 5, 3]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        foreach (var output in root.Outputs) Bits(new double[bars.Length], run[output].ToArray());
    }
    [Fact]
    public void ThreeOutputsKeepOrderAndPrimary()
    { var root = new MacZVwapIndicator(); Assert.Equal(new[] { root.Value, root.Signal, root.Histogram }, root.Outputs); Assert.Same(root.Value, root.PrimaryOutput); }
    [Fact]
    public async Task LongHistoryCrossesSeveralExpirationsWithoutChangingRounding()
    {
        await Compare([new MacZVwapIndicator(11), new MacZVwapIndicator(65)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new MacZVwapIndicator(3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = new MacZVwapIndicator(3); IIndicator[] roots = [repeated, new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void AllOutputsReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 39).Select(i => Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedMacZVwapState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 3, 5, 2, 3, 2, .02 }, null)!;
        double[] Run() { var values = new List<double>(); foreach (var bar in bars) { var outputs = new double[3]; state.Update(in bar, outputs); values.AddRange(outputs); } return values.ToArray(); }
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
