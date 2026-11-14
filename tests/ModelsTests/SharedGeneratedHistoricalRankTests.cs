using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedHistoricalRankTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(HistoricalVolatilityPercentile)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory]
    [InlineData(int.MinValue, 0)] [InlineData(0, int.MinValue)] [InlineData(1, 1)]
    [InlineData(2, 3)] [InlineData(5, 7)] [InlineData(21, 252)] [InlineData(4096, 4096)]
    public async Task RanksAndSignalRetainLegacyBits(int length, int annual)
    {
        var root = new HistoricalVolatilityPercentile(length, annual);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Repeat(7d, 31).ToArray(),
            Enumerable.Range(0, 311).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task ConstantPricesHaveZeroRankAndSignal()
    {
        var root = new HistoricalVolatilityPercentile(3, 7);
        var bars = ToBars(Enumerable.Repeat(7d, 31).ToArray());
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        foreach (var output in root.Outputs) Bits(new double[bars.Length], run[output].ToArray());
    }

    [Fact]
    public async Task MixedRootsComponentsSourcesAndErrorsKeepTheirContracts()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4, 7, 3]);
        var repeated = new HistoricalVolatilityPercentile(3, 7);
        IIndicator[] roots = [repeated, new GarmanKlassVolatility(3), repeated];
        await Compare(roots, bars);
        foreach (var selected in new IIndicator[] { new HistoricalVolatilityPercentile(3, 7).Of(new Sma(2)), new HistoricalVolatilityPercentile(3, 7, new Sma(2)), new HistoricalVolatilityPercentile(3, 7, new Sma(2), new Ema(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([selected]));
            await Compare([selected], bars);
        }
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

