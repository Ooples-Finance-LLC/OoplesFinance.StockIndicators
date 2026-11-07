using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedErgodicAdaptiveSelectionTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(AdaptiveErgodicCandlestickOscillator) || c.IndicatorType == typeof(ErgodicCommoditySelectionIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 14, 4096, int.MaxValue } select new object[] { kind, length };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsFieldsSeedBoundaryAndOutputsRetainLegacyBits(int kind, int length)
    {
        IIndicator root = kind == 0 ? new AdaptiveErgodicCandlestickOscillator(3, length, 5) : new ErgodicCommoditySelectionIndex(length, 3, 1);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await CheckPrices(root);
    }
    private static async Task CheckPrices(IIndicator root)
    {
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Range(0, 83).Select(i => (i * 13 % 101 - 20) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(-double.MaxValue)] [InlineData(-1)] [InlineData(-0d)]
    [InlineData(double.Epsilon)] [InlineData(1)] [InlineData(double.MaxValue)]
    public Task ExactPointScaleRetainsWideQuotients(double pointValue) => CheckPrices(new ErgodicCommoditySelectionIndex(3, 2, pointValue));

    [Theory]
    [InlineData(1, 2, 3)] [InlineData(7, 5, 2)] [InlineData(int.MaxValue, 3, int.MaxValue)]
    public Task AdaptivePeriodsRemainIndependent(int smooth, int stochastic, int signal) =>
        CheckPrices(new AdaptiveErgodicCandlestickOscillator(smooth, stochastic, signal));

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)] [InlineData(double.NegativeInfinity)]
    public async Task InvalidPointValueKeepsLegacyErrorPriority(double pointValue)
    {
        var root = new ErgodicCommoditySelectionIndex(3, 2, pointValue);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([new AdaptiveErgodicCandlestickOscillator(), root], []);
        await Compare([root], ToBars([1, 3, 2]));
        var bars = ToBars([1, 3, 2]); bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare([root], bars);
    }

    [Fact]
    public async Task ConstantBodyRatioAndZeroPointValueHaveKnownOutputs()
    {
        var adaptive = new AdaptiveErgodicCandlestickOscillator(3, 2, 5);
        var zero = new ErgodicCommoditySelectionIndex(3, 2, 0);
        var bars = Enumerable.Repeat(new Bar(default, 1, 5, 1, 3, 1), 31).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(adaptive, zero).BuildAsync();
        foreach (var output in adaptive.Outputs) Bits(Enumerable.Repeat(50d, bars.Length).ToArray(), run[output].ToArray());
        foreach (var output in zero.Outputs) Bits(new double[bars.Length], run[output].ToArray());
    }

    [Fact]
    public async Task ComponentsSourcesMixedRootsErrorsAndOwnership()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4, 7, 3]);
        foreach (var root in new IIndicator[]
        {
            new AdaptiveErgodicCandlestickOscillator(3, 2, 5, new Ema(2)),
            new ErgodicCommoditySelectionIndex(3, 2, 1, new Ema(2)),
            new AdaptiveErgodicCandlestickOscillator(3, 2, 5).Of(new Sma(2)),
            new ErgodicCommoditySelectionIndex(3, 2, 1).Of(new Sma(2))
        }) { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = new ErgodicCommoditySelectionIndex(3, 2, -1);
        IIndicator[] roots = [new AdaptiveErgodicCandlestickOscillator(3, 2, 5), new ErgodicCandlestickOscillator(3), repeated, repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowsReplayAfterReset()
    {
        var bars = ToBars(Enumerable.Range(0, 41).Select(i => (i * 13 % 101 - 20) / 8d).ToArray());
        using var adaptive = new AdaptiveCandleWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage, 3, 2, 5, bars.Length);
        var expected = bars.Select(b => adaptive.Next(b.Open, b.High, b.Low, b.Close, true)).ToArray();
        adaptive.Reset(); var replay = bars.Select(b => adaptive.Next(b.Open, b.High, b.Low, b.Close, true)).ToArray();
        Bits(expected.Select(p => p.Eco.Publish()).ToArray(), replay.Select(p => p.Eco.Publish()).ToArray());
        Bits(expected.Select(p => p.Signal).ToArray(), replay.Select(p => p.Signal).ToArray());
        using var selection = new ErgodicSelectionWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.WildersSmoothingMethod, 3, 2, 1);
        var before = bars.Select(b => selection.Next(b.High, b.Low, b.Close, true)).ToArray();
        selection.Reset(); var after = bars.Select(b => selection.Next(b.High, b.Low, b.Close, true)).ToArray();
        Bits(before.Select(p => p.Line).ToArray(), after.Select(p => p.Line).ToArray());
        Bits(before.Select(p => p.SignalLine).ToArray(), after.Select(p => p.SignalLine).ToArray());
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

