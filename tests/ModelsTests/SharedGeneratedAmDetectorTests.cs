using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAmDetectorTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersAMDetector)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 3)] [InlineData(4, 8)] [InlineData(7, 2)]
    [InlineData(int.MinValue, 0)] [InlineData(0, int.MinValue)]
    [InlineData(int.MaxValue, 2)] [InlineData(3, int.MaxValue)] [InlineData(int.MaxValue, int.MaxValue)]
    public async Task ExactEnvelopeAndTwoAveragesRetainLegacyBits(int envelope, int smooth)
    {
        var root = new EhlersAMDetector(envelope, smooth);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][] { [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 0, -1, 2],
            Enumerable.Range(0, 41).Select(i => (i * 13 % 101 - 50) / 8d).ToArray() })
            await Compare([root], ToBars(prices));
        await Compare([root], [new(default, -double.MaxValue, 1, -1, double.MaxValue, 1),
            new(default, double.MaxValue, 1, -1, -double.MaxValue, 1), new(default, 0, 1, -1, 0, 1)]);
    }

    [Fact]
    public async Task ExpiringMaximumAndSecondSmoothingHaveKnownOutputs()
    {
        var bars = new double[] { 8, 0, 2, 0, 0, 0 }.Select(v => new Bar(default, 0, 10, 0, v, 1)).ToArray();
        var root = new EhlersAMDetector(2, 2);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Bits([0, 8, 5, 2, 1, 0], run[root.Value].ToArray());
        Bits([0, 4, 6.5, 3.5, 1.5, .5], run[root.Signal].ToArray());
    }

    [Fact]
    public async Task ComponentsSourcesOriginalFieldsAndErrorsKeepTheirContracts()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7]);
        foreach (var root in new IIndicator[] {
            new EhlersAMDetector(3, 2, new Wma(2)),
            new EhlersAMDetector(3, 2, new Wma(2), new Sma(2), new Ema(2)),
            new EhlersAMDetector(3, 2).Of(new Sma(2)) })
        { Assert.False(ValuesBarExecution.SupportsOwned([root])); await Compare([root], bars); }
        var repeated = new EhlersAMDetector(3, 2);
        IIndicator[] roots = [repeated, new EhlersKaufmanAdaptiveMovingAverage(3), new EhlersSimpleCycleIndicator(), repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN); await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowReplaysAfterReset()
    {
        using var window = new AmDetectorWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage, 3, 2);
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7]);
        var expected = bars.Select(bar => window.Next(bar.Open, bar.Close, true)).ToArray();
        window.Reset();
        var actual = bars.Select(bar => window.Next(bar.Open, bar.Close, true)).ToArray();
        Bits(expected.Select(x => x.Line).ToArray(), actual.Select(x => x.Line).ToArray());
        Bits(expected.Select(x => x.Signal).ToArray(), actual.Select(x => x.Signal).ToArray());
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

