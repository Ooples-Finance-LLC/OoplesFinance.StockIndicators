using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTruncatedBandTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersTruncatedBandPassFilter)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory]
    [InlineData(int.MinValue, 0)] [InlineData(0, int.MinValue)] [InlineData(1, 1)]
    [InlineData(2, 7)] [InlineData(7, 2)] [InlineData(20, 10)]
    [InlineData(int.MaxValue, 3)] [InlineData(3, int.MaxValue)]
    public async Task IndependentPeriodAndCutoffRetainBits(int length, int cutoff)
    {
        var root = new EhlersTruncatedBandPassFilter(length, cutoff);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7, 2, 0, -1],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2, 3, -2, 0],
            Enumerable.Range(0, 37).Select(i => (i * 13 % 101 - 50) / 8d).ToArray() })
            await Compare([root], ToBars(prices));
    }

    [Theory]
    [InlineData(7, double.MaxValue, true)] [InlineData(10, -double.MaxValue, true)]
    [InlineData(1, double.MaxValue, false)] [InlineData(1, -double.MaxValue, false)]
    [InlineData(3, 0, true)] [InlineData(3, double.Epsilon, true)] [InlineData(3, -.25, true)]
    [InlineData(3, double.NaN, false)] [InlineData(3, double.PositiveInfinity, false)] [InlineData(3, double.NegativeInfinity, false)]
    public async Task BandwidthRescueFallbackAndErrorPriority(int length, double bandwidth, bool supported)
    {
        var root = new EhlersTruncatedBandPassFilter(length, 3, bandwidth);
        Assert.Equal(supported, ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []);
        await Compare([root], ToBars([1, -2, 3, 0, 0, 2, -3, 1, 7, -5, 2, 0, 1, 2]));
        await Compare([new EhlersUniversalOscillator(3), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Fact]
    public async Task SourcesMixedRootsAndCancellationKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 2, 3, -1, 0]);
        var selected = new EhlersTruncatedBandPassFilter(7, 3).Of(new Sma(2));
        Assert.False(ValuesBarExecution.SupportsOwned([selected])); await Compare([selected], bars);
        var repeated = new EhlersTruncatedBandPassFilter(7, 3);
        IIndicator[] roots = [repeated, new EhlersReverseEmaIndicatorV2(), new EhlersEvenBetterSineWaveIndicator(3, 2), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowReplaysAfterReset()
    {
        var window = new TruncatedBandPassWindow(7, 3, .25);
        var prices = Enumerable.Range(0, 23).Select(i => (i * 13 % 31 - 15) / 8d).ToArray();
        var expected = prices.Select(value => window.Next(value, true)).ToArray(); window.Reset();
        Bits(expected, prices.Select(value => window.Next(value, true)).ToArray());
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

