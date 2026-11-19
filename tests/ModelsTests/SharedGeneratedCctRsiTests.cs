using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedCctRsiTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(CCTStochRSI) || c.IndicatorType == typeof(CCTStochRelativeStrengthIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    private static IIndicator Create(int kind, int length) => kind == 0 ? new CCTStochRelativeStrengthIndex(length)
        : new CCTStochRSI(length, length, length, length, length, length, length, MovingAvgType.ExponentialMovingAverage);
    public static IEnumerable<object[]> Cases => from kind in Enumerable.Range(0, 2)
        from length in new[] { int.MinValue, 1, 2, 20, 65, 1024 } select new object[] { kind, length };

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
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task EachPeriodKeepsItsOwnWindow(int axis)
    {
        int[] lengths = [5, 8, 13, 14, 21, 3, 8]; lengths[axis] = 2;
        await Compare([new CCTStochRSI(lengths[0], lengths[1], lengths[2], lengths[3], lengths[4], lengths[5], lengths[6], MovingAvgType.ExponentialMovingAverage)],
            ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)] [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)] [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public async Task SupportedAveragesKeepAllEightOutputs(MovingAvgType kind)
    {
        var root = new CCTStochRSI(5, 8, 13, 14, 21, 3, 8, kind); Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public void EightOutputsKeepFirstPrimary()
    {
        foreach (var root in new[] { Create(0, 14), Create(1, 14) })
        { Assert.Equal(8, root.Outputs.Count); Assert.Same(root.Outputs[0], ((IPrimaryOutputIndicator)root).PrimaryOutput); }
    }

    [Fact]
    public async Task AliasIgnoresObsoleteLengthAndMatchesDefaults()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var alias = new CCTStochRelativeStrengthIndex(int.MaxValue);
        var full = new CCTStochRSI(5, 8, 13, 14, 21, 3, 8, MovingAvgType.ExponentialMovingAverage);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(alias, full).BuildAsync();
        for (var i = 0; i < 8; i++) Bits(run[alias.Outputs[i]].ToArray(), run[full.Outputs[i]].ToArray());
    }

    [Fact]
    public async Task LongHistoryCrossesEveryWindow()
    {
        await Compare([new CCTStochRelativeStrengthIndex(14), new CCTStochRSI(5, 8, 13, 14, 21, 3, 8, MovingAvgType.ExponentialMovingAverage)],
            ToBars(Enumerable.Range(0, 257).Select(i => 10 + Math.Sin(i * .7)).ToArray()));
    }

    [Fact]
    public async Task UnsupportedAverageAndSourceKeepFallback()
    {
        foreach (var root in new IIndicator[] { new CCTStochRSI(5, 8, 13, 14, 21, 3, 8, MovingAvgType.DoubleExponentialMovingAverage), new CCTStochRelativeStrengthIndex(14).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0])); }
    }

    [Fact]
    public async Task RepeatedRootsErrorsAndCancellationKeepContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]); var repeated = Create(0, 14);
        IIndicator[] roots = [repeated, Create(1, 14), new EhlersAdaptiveRelativeStrengthIndexV1(), repeated];
        await Compare(roots, bars); bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true))); Assert.Null(builder.LastExecution);
    }

    [Fact]
    public async Task HugeOwnedPeriodsMatchObservedEmaHistory()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var n = bars.Length;
        var bounded = new CCTStochRSI(n, n, n, n, n, n, n, MovingAvgType.ExponentialMovingAverage);
        n = int.MaxValue;
        var huge = new CCTStochRSI(n, n, n, n, n, n, n, MovingAvgType.ExponentialMovingAverage);
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(bounded).ConfigureBehavior(_ => { }).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(huge).BuildAsync();
        Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
        for (var i = 0; i < 8; i++) Bits(expected[bounded.Outputs[i]].ToArray(), actual[huge.Outputs[i]].ToArray());
    }

    [Fact]
    public void ExactStateReplaysAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 65).Select(i => 10 + Math.Sin(i * .7)).ToArray());
        var type = typeof(ValuesBarExecution).GetNestedType("GeneratedCctRsiState", System.Reflection.BindingFlags.NonPublic)!;
        var state = (IMultiOutputState)Activator.CreateInstance(type,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
            null, new object[] { 5, 8, 13, 14, 21, 3, 8, MovingAvgType.ExponentialMovingAverage, bars.Length }, null)!;
        double[] Run() { var result = new List<double>(); var outputs = new double[8]; foreach (var bar in bars) { state.Update(in bar, outputs); result.AddRange(outputs); } return result.ToArray(); }
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
