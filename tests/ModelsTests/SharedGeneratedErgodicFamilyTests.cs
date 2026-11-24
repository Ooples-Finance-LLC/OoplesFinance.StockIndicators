using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedErgodicFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(ErgodicCandlestickOscillator) || c.IndicatorType == typeof(ErgodicMovingAverageConvergenceDivergence) || c.IndicatorType == typeof(ErgodicPercentagePriceOscillator) || c.IndicatorType == typeof(ErgodicTrueStrengthIndexV2)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096, int.MaxValue } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new ErgodicCandlestickOscillator(length),
        1 => new ErgodicMovingAverageConvergenceDivergence(length, 3, 7),
        2 => new ErgodicPercentagePriceOscillator(length),
        _ => new ErgodicTrueStrengthIndexV2(length, 3, 5, 7, length, 2, 4)
    };
    [Theory, MemberData(nameof(Cases))]
    public async Task PeriodsOutputsAndNonfiniteSignalTailsRetainLegacyBits(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, double.MaxValue, 1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Range(0, 47).Select(i => (i * 13 % 101 - 50) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task CandleUsesOriginalFieldsAndMacdKeepsFirstMinusSecondSign()
    {
        var candle = new ErgodicCandlestickOscillator(3);
        var bars = Enumerable.Repeat(new Bar(default, 1, 5, 1, 3, 1), 11).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(candle).BuildAsync();
        foreach (var output in candle.Outputs) Bits(Enumerable.Repeat(50d, 11).ToArray(), run[output].ToArray());
        var macd = new ErgodicMovingAverageConvergenceDivergence(3, 1, 1);
        using var line = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 3, 5]))).ConfigureIndicators(macd).BuildAsync();
        Bits([0, -1, -2], line[macd.Value].ToArray());
        Bits([0, -1, -2], line[macd.Signal].ToArray());
        Bits([0, 0, 0], line[macd.Histogram].ToArray());
    }

    [Theory]
    [InlineData(1, 2, 3, 5, 7, 11, 13)] [InlineData(13, 11, 7, 5, 3, 2, 1)]
    [InlineData(0, -1, 1, 2, 3, 4, int.MaxValue)]
    public Task SeparateStrengthTriplesAndSignalKeepTheirSlots(int a, int b, int c, int d, int e, int f, int signal) =>
        Compare([new ErgodicTrueStrengthIndexV2(a, b, c, d, e, f, signal)], ToBars([1, 3, -2, 4, -5, 0, 7, 2, -1]));

    [Fact]
    public async Task ComponentsSourcesMixedRootsAndErrorsKeepTheirContracts()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4, 7, 3]);
        foreach (var kind in new[] { 0, 1, 2, 3 })
        {
            var source = (MultiOutputIndicatorBase)Create(kind, 3);
            var selected = source.Of(new Sma(2));
            Assert.False(ValuesBarExecution.SupportsOwned([selected]));
            await Compare([selected], bars);
        }
        foreach (var root in new IIndicator[]
        {
            new ErgodicCandlestickOscillator(3, new Sma(2), new Ema(2)),
            new ErgodicMovingAverageConvergenceDivergence(3, 2, 5, new Sma(2), new Ema(2)),
            new ErgodicPercentagePriceOscillator(3, new Sma(2), new Ema(2)),
            new ErgodicTrueStrengthIndexV2(maType: new Sma(2), secondAverage: new Ema(2))
        }) { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = Create(3, 3);
        IIndicator[] roots = [Create(0, 3), Create(1, 3), Create(2, 3), repeated, new ErgodicMeanDeviationIndicator(), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
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

