using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedResidualPairTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(StandardErrorCore) || c.IndicatorType == typeof(ErgodicMeanDeviationIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    [Theory]
    [InlineData(int.MinValue)] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(5)] [InlineData(20)] [InlineData(4096)]
    public async Task PopulationMeanErrorRetainsEveryLegacyBit(int length) => await CheckPrices(new StandardErrorCore(length));

    [Theory]
    [InlineData(int.MinValue, 0, -1, 1)] [InlineData(1, 1, 1, 1)]
    [InlineData(3, 2, 5, 7)] [InlineData(32, 5, 5, 5)]
    [InlineData(4096, 3, 2, 9)] [InlineData(3, 4096, 2, 9)]
    [InlineData(3, 2, 4096, 9)] [InlineData(3, 2, 9, 4096)]
    [InlineData(int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)]
    public async Task WideResidualAndSignalStagesRetainLegacyBits(int mean, int first, int second, int signal) =>
        await CheckPrices(new ErgodicMeanDeviationIndicator(mean, first, second, signal));

    private static async Task CheckPrices(IIndicator root)
    {
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Range(0, 47).Select(i => (i * 13 % 101 - 50) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task MeanErrorUsesPopulationVarianceAndFullWindowWarmup()
    {
        var root = new StandardErrorCore(2);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 3, 5]))).ConfigureIndicators(root).BuildAsync();
        var actual = run[root.Outputs[0]].ToArray();
        Assert.Equal(0, actual[0]);
        Assert.InRange(Math.Abs(actual[1] - Math.Sqrt(.5)), 0, 1e-15);
        Assert.Equal(actual[1], actual[2]);
        var residual = new ErgodicMeanDeviationIndicator(3, 2, 5, 7);
        using var flat = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars(Enumerable.Repeat(7d, 31).ToArray()))).ConfigureIndicators(residual).BuildAsync();
        foreach (var output in residual.Outputs) Bits(new double[31], flat[output].ToArray());
    }

    [Fact]
    public async Task MixedRootsSourcesAndErrorsKeepTheirContracts()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4, 7, 3]);
        var repeated = new ErgodicMeanDeviationIndicator(3, 2, 5, 7);
        IIndicator[] roots = [repeated, new StandardErrorCore(3), new StandardError(3), new EhlersDeviationScaledMovingAverage(3), new EhlersFisherizedDeviationScaledOscillator(3), new EhlersDeviationScaledSuperSmoother(3, 2), repeated];
        await Compare(roots, bars);
        foreach (var root in new IIndicator[] { new StandardErrorCore(3).Of(new Sma(2)), new ErgodicMeanDeviationIndicator(3, 2, 5, 7).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
        }
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowsReplayAfterReset()
    {
        double[] prices = [1, 3, -2, 0, 4, 7, 2, 5, 1];
        using var error = new ExactStandardErrorWindow(3, false);
        var expected = prices.Select(value => error.Next(value, true)).ToArray();
        error.Reset(); Bits(expected, prices.Select(value => error.Next(value, true)).ToArray());
        using var residual = new ResidualAverageWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage, 3, OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage, 2, 5, 7);
        var points = prices.Select(value => residual.Next(value, true)).ToArray();
        residual.Reset(); var replay = prices.Select(value => residual.Next(value, true)).ToArray();
        Bits(points.Select(p => p.Value).ToArray(), replay.Select(p => p.Value).ToArray());
        Bits(points.Select(p => p.Signal).ToArray(), replay.Select(p => p.Signal).ToArray());
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

