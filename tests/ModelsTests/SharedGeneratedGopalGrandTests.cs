using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedGopalGrandTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(GopalakrishnanRangeIndex) || c.IndicatorType == typeof(GrandTrendForecasting)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static MultiOutputIndicatorBase Create(int kind, int length) => kind == 0 ? new GopalakrishnanRangeIndex(length) : new GrandTrendForecasting(length, length);
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
    [InlineData(1, 1)] [InlineData(2, 65)] [InlineData(65, 2)] [InlineData(14, 7)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task IndependentTrendAndHorizonKeepBits(int length, int horizon)
    { await Compare([new GrandTrendForecasting(length, horizon)], ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Theory]
    [InlineData(0d)] [InlineData(1d)] [InlineData(double.MaxValue)] [InlineData(double.Epsilon)]
    public async Task FiniteNonnegativeMultipliersKeepWideBands(double multiplier)
    { var root = new GrandTrendForecasting(3, 7, multiplier); Assert.True(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1])); }

    [Theory]
    [InlineData(-1d)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public async Task InvalidMultipliersKeepFallbackErrors(double multiplier)
    { var root = new GrandTrendForecasting(3, 7, multiplier); Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, 3])); }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EitherGopalAverageKeepsFallback(bool second)
    {
        var root = new GopalakrishnanRangeIndex(3, new Ema(2), second ? new Ema(2) : null);
        Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 2, -3, 4, 0, 2, -1, 0, 1]));
    }

    [Fact]
    public void SecondCustomAverageRequiresPrecedingAverage()
    {
        var error = Assert.Throws<ArgumentException>(() => new GopalakrishnanRangeIndex(3, null, new Ema(2)));
        Assert.Equal("secondAverage", error.ParamName);
    }

    [Fact]
    public void OutputShapesKeepOrder()
    {
        var range = new GopalakrishnanRangeIndex(); var grand = new GrandTrendForecasting(); Assert.Equal(2, range.Outputs.Count); Assert.Equal(4, grand.Outputs.Count);
        Assert.Same(range.Value, range.Outputs[0]); Assert.Same(range.Signal, range.Outputs[1]); Assert.Same(grand.Value, grand.Outputs[0]); Assert.Same(grand.UpperBand, grand.Outputs[1]); Assert.Same(grand.MiddleBand, grand.Outputs[2]); Assert.Same(grand.LowerBand, grand.Outputs[3]);
    }

    [Fact]
    public async Task GopalOriginalRangeAndMinimumPeriodKeepHandValues()
    {
        var root = new GopalakrishnanRangeIndex(1); Bar[] bars = [new(default, 99, 4, 0, 2, 1), new(default, 99, 8, 0, 4, 1), new(default, 99, 2, 1, 1, 1)]; await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([2, 3, 3], run[root.Outputs[0]].ToArray()); Bits([4d / 3, 8d / 3, 3], run[root.Outputs[1]].ToArray());
    }

    [Fact]
    public async Task GrandZeroOriginKeepsHandValues()
    {
        var root = new GrandTrendForecasting(3, 7, 2); var bars = ToBars([10]); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([9], run[root.Outputs[0]].ToArray()); Bits([38], run[root.Outputs[1]].ToArray()); Bits([18], run[root.Outputs[2]].ToArray()); Bits([-2], run[root.Outputs[3]].ToArray());
    }

    [Fact]
    public async Task ZeroMultiplierCollapsesForecastBands()
    {
        var root = new GrandTrendForecasting(3, 7, 0); var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()); await Compare([root], bars);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits(run[root.Outputs[2]].ToArray(), run[root.Outputs[1]].ToArray()); Bits(run[root.Outputs[2]].ToArray(), run[root.Outputs[3]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesAllWindows()
    { await Compare([new GopalakrishnanRangeIndex(5), new GrandTrendForecasting(100, 200)], ToBars(Enumerable.Range(0, 257).Select(i => Math.Sin(i * .7)).ToArray())); }

    [Fact]
    public async Task SourcesRepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        for (var kind = 0; kind < 2; kind++) { var selected = Create(kind, 3).Of(new Sma(2)); Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars); }
        var repeated = Create(0, 3); IIndicator[] roots = [repeated, Create(1, 3), repeated]; await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WindowsReplayAfterReset(bool grand)
    {
        var bars = ToBars(Enumerable.Range(0, 97).Select(i => Math.Sin(i * .7)).ToArray()); var type = typeof(ValuesBarExecution).GetNestedType(grand ? "GeneratedGrandTrendState" : "GeneratedGopalakrishnanState", System.Reflection.BindingFlags.NonPublic)!;
        object[] args = grand ? [3, 7, 2d] : [3, bars.Length]; var state = (IMultiOutputState)Activator.CreateInstance(type, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, args, null)!;
        double[] Run() { var values = new List<double>(); var outputs = new double[grand ? 4 : 2]; foreach (var bar in bars) { state.Update(in bar, outputs); values.AddRange(outputs); } return values.ToArray(); }
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
