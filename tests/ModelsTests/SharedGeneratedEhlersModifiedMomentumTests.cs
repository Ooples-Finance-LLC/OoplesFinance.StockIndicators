using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEhlersModifiedMomentumTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersModifiedRelativeStrengthIndex) || c.IndicatorType == typeof(EhlersModifiedStochasticIndicator) || c.IndicatorType == typeof(EhlersRocketRelativeStrengthIndex) || c.IndicatorType == typeof(EhlersMovingAverageDifferenceIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096, int.MaxValue } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new EhlersModifiedRelativeStrengthIndex(length, 3, 5),
        1 => new EhlersModifiedStochasticIndicator(length, 3, 5),
        2 => new EhlersRocketRelativeStrengthIndex(length, 3),
        _ => new EhlersMovingAverageDifferenceIndicator(length, 3)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task ExactKernelsRetainLegacyBits(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            [-.2, .1, -.05, .3, .15, 8, -9, .05],
            Enumerable.Range(0, 67).Select(i => (i * 13 % 101 - 50) / 80d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task SelectedSourcesKeepTheirFallbackContracts()
    {
        var bars = ToBars([.01, .02, -.03, .05, -.02, 0, .04]);
        foreach (var root in new IIndicator[] {
            new EhlersModifiedStochasticIndicator(7, 3, 5, new Wma(2)),
            new EhlersRocketRelativeStrengthIndex(3, 2, maType: new Wma(2)),
            new EhlersMovingAverageDifferenceIndicator(3, 2, new Ema(2), new Sma(2)),
            new EhlersModifiedRelativeStrengthIndex(7, 3, 5).Of(new Sma(2)),
            new EhlersModifiedStochasticIndicator(7, 3, 5).Of(new Sma(2)),
            new EhlersRocketRelativeStrengthIndex(3, 2).Of(new Sma(2)),
            new EhlersMovingAverageDifferenceIndicator(3, 2).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
        }
    }

    [Fact]
    public async Task MixedRootsErrorsCancellationAndOwnership()
    {
        var bars = ToBars([.01, .02, -.03, .05, -.02, 0, .04]);
        var repeated = Create(1, 3);
        IIndicator[] roots = [Create(0, 3), Create(2, 3), Create(3, 3), new EhlersStochasticCyberCycle(3), repeated, repeated];
        await Compare(roots, bars);
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    [Fact]
    public void ExactWindowsReplayAfterReset()
    {
        double[] prices = [.01, .02, -.03, .05, -.02, 0, .04];
        var rsi = new ModifiedRsiWindow(7, 3, 5);
        var expectedRsi = prices.Select(value => rsi.Next(value, true)).ToArray(); rsi.Reset();
        var actualRsi = prices.Select(value => rsi.Next(value, true)).ToArray();
        Bits(expectedRsi.Select(x => x.Line).ToArray(), actualRsi.Select(x => x.Line).ToArray());
        Bits(expectedRsi.Select(x => x.Signal).ToArray(), actualRsi.Select(x => x.Signal).ToArray());
        using var stochastic = new RoofingStochasticWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 7, 3, 5, true);
        var expected = prices.Select(value => stochastic.Next(value, true)).ToArray(); stochastic.Reset();
        Bits(expected, prices.Select(value => stochastic.Next(value, true)).ToArray());
        using var rocket = new RocketRsiWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.Ehlers2PoleSuperSmootherFilterV2, 3, 2, 1);
        expected = prices.Select(value => rocket.Next(value, true)).ToArray(); rocket.Reset();
        Bits(expected, prices.Select(value => rocket.Next(value, true)).ToArray());
        using var gap = new AverageGapWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.WeightedMovingAverage, 3, 2, prices.Length);
        expected = prices.Select(value => gap.Next(value, true)).ToArray(); gap.Reset();
        Bits(expected, prices.Select(value => gap.Next(value, true)).ToArray());
    }

    [Theory]
    [InlineData(int.MinValue, 0, int.MaxValue)] [InlineData(1, 1, 1)]
    [InlineData(7, 3, 5)] [InlineData(2, int.MaxValue, 3)] [InlineData(5, 2, int.MaxValue)]
    public async Task IndependentPeriodsAndZeroDenominatorsKeepTheirContracts(int first, int second, int third)
    {
        IIndicator[] roots = [new EhlersModifiedRelativeStrengthIndex(first, second, third),
            new EhlersModifiedStochasticIndicator(first, second, third), new EhlersRocketRelativeStrengthIndex(first, second),
            new EhlersMovingAverageDifferenceIndicator(first, second)];
        await Compare(roots, ToBars([0, 0, 1, -1, 0, 2, -2, 0, 3, 4, -1, 0]));
    }

    [Theory]
    [InlineData(-double.MaxValue)] [InlineData(-1)] [InlineData(-0d)]
    [InlineData(double.Epsilon)] [InlineData(1)] [InlineData(double.MaxValue)]
    public async Task RocketFiniteFactorsAndThresholdsRetainExactOutputs(double factor)
    {
        var bars = ToBars([0, 1, -2, 3, 0, 0, 2, -3, 1, 7, -5]);
        await Compare([new EhlersRocketRelativeStrengthIndex(3, 2, 2, factor)], bars);
        await Compare([new EhlersRocketRelativeStrengthIndex(3, 2, factor, 1)], bars);
    }

    [Theory]
    [InlineData(double.NaN)] [InlineData(double.NegativeInfinity)] [InlineData(double.PositiveInfinity)]
    public async Task RocketNonfiniteOptionsKeepLegacyFallbackAndErrors(double invalid)
    {
        foreach (var root in new[] { new EhlersRocketRelativeStrengthIndex(3, 2, invalid, 1), new EhlersRocketRelativeStrengthIndex(3, 2, 2, invalid) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], []);
            await Compare([root], ToBars([1, 3, 2]));
            await Compare([new EhlersMovingAverageDifferenceIndicator(3, 2), root], [new(default, 1, 2, 0, 1, double.NaN)]);
        }
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

