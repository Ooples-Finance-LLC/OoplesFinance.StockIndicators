using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEhlersStochasticVigorTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersStochasticCenterOfGravityOscillator) || c.IndicatorType == typeof(EhlersStochastic) || c.IndicatorType == typeof(EhlersStochasticCyberCycle) || c.IndicatorType == typeof(EhlersRelativeVigorIndex)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096, int.MaxValue } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new EhlersStochasticCenterOfGravityOscillator(length),
        1 => new EhlersStochastic(length),
        2 => new EhlersStochasticCyberCycle(length),
        _ => new EhlersRelativeVigorIndex(length, 3)
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
            new EhlersStochastic(3, new Wma(2), new Sma(2)),
            new EhlersRelativeVigorIndex(3, 2, new Wma(2), new Sma(2)),
            new EhlersStochasticCenterOfGravityOscillator(3).Of(new Sma(2)),
            new EhlersStochastic(3).Of(new Sma(2)),
            new EhlersStochasticCyberCycle(3).Of(new Sma(2)),
            new EhlersRelativeVigorIndex(3).Of(new Sma(2)) })
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
        IIndicator[] roots = [Create(0, 3), Create(2, 3), Create(3, 3), new EhlersSimpleClipIndicator(2, 10, 3, 2), repeated, repeated];
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
        using var gravity = new StochasticGravityWindow(3, prices.Length);
        var expected = prices.Select(value => gravity.Next(value, true)).ToArray(); gravity.Reset();
        Bits(expected, prices.Select(value => gravity.Next(value, true)).ToArray());
        using var roof = new RoofingStochasticWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.Ehlers2PoleSuperSmootherFilterV1, 48, 10, 3, false);
        expected = prices.Select(value => roof.Next(value, true)).ToArray(); roof.Reset();
        Bits(expected, prices.Select(value => roof.Next(value, true)).ToArray());
        var cyber = new StochasticCyberWindow(3, .7);
        var expectedCyber = prices.Select(value => cyber.Next(value, true)).ToArray(); cyber.Reset();
        var actualCyber = prices.Select(value => cyber.Next(value, true)).ToArray();
        Bits(expectedCyber.Select(x => x.Line).ToArray(), actualCyber.Select(x => x.Line).ToArray());
        Bits(expectedCyber.Select(x => x.Signal).ToArray(), actualCyber.Select(x => x.Signal).ToArray());
        using var vigor = new EhlersVigorWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage, 3, 2, prices.Length);
        var expectedVigor = prices.Select(value => vigor.Next(value, -value, 1, -1, true)).ToArray(); vigor.Reset();
        var actualVigor = prices.Select(value => vigor.Next(value, -value, 1, -1, true)).ToArray();
        Bits(expectedVigor.Select(x => x.Value).ToArray(), actualVigor.Select(x => x.Value).ToArray());
        Bits(expectedVigor.Select(x => x.Signal).ToArray(), actualVigor.Select(x => x.Signal).ToArray());
    }

    [Theory]
    [InlineData(double.NegativeInfinity)] [InlineData(-double.MaxValue)] [InlineData(-0d)]
    [InlineData(double.Epsilon)] [InlineData(.01)] [InlineData(.7)] [InlineData(.99)]
    [InlineData(1)] [InlineData(double.MaxValue)] [InlineData(double.PositiveInfinity)]
    public async Task CyberAlphaClampsBeforeStartupAndSignalCalculation(double alpha)
    {
        var root = new EhlersStochasticCyberCycle(3, alpha);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], ToBars(Enumerable.Range(0, 23).Select(i => (i * 13 % 31 - 15) / 8d).ToArray()));
    }

    [Fact]
    public async Task CyberNaNKeepsFallbackAndValidationPriority()
    {
        var root = new EhlersStochasticCyberCycle(3, double.NaN);
        Assert.False(ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []);
        await Compare([root], ToBars([1, 3, 2]));
        await Compare([new EhlersRelativeVigorIndex(3), root], [new(default, 1, 2, 0, 1, double.NaN)]);
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(2, 3)] [InlineData(7, 2)]
    [InlineData(int.MaxValue, 2)] [InlineData(3, int.MaxValue)]
    public async Task VigorWideRatiosReversedAndZeroRangesKeepBothAverages(int length, int signal)
    {
        var root = new EhlersRelativeVigorIndex(length, signal);
        await Compare([root], [new(default, -double.MaxValue, double.Epsilon, 0, double.MaxValue, 1),
            new(default, double.MaxValue, 0, double.Epsilon, -double.MaxValue, 1),
            new(default, 1, 0, 0, -1, 1), new(default, 0, 1, -1, 0, 1), new(default, 2, 1, -1, 3, 1)]);
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

