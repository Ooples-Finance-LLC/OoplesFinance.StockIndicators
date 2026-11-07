using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedEhlersSimpleFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersSimpleDerivIndicator) || c.IndicatorType == typeof(EhlersSimpleClipIndicator) || c.IndicatorType == typeof(EhlersSimpleWindowIndicator) || c.IndicatorType == typeof(EhlersTripleDelayLineDetrender)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2, 3 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096, int.MaxValue } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new EhlersSimpleDerivIndicator(length, 3),
        1 => new EhlersSimpleClipIndicator(length, 10, 5, 3),
        2 => new EhlersSimpleWindowIndicator(length),
        _ => new EhlersTripleDelayLineDetrender(length)
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
            new EhlersSimpleDerivIndicator(3, 2, new Wma(2)),
            new EhlersSimpleClipIndicator(3, 10, 5, 2, new Wma(2)),
            new EhlersSimpleWindowIndicator(3, new Wma(2), new Sma(2), new Ema(2)),
            new EhlersTripleDelayLineDetrender(3, new Wma(2), new Sma(2)),
            new EhlersSimpleDerivIndicator(3).Of(new Sma(2)),
            new EhlersSimpleClipIndicator(3).Of(new Sma(2)),
            new EhlersSimpleWindowIndicator(3).Of(new Sma(2)),
            new EhlersTripleDelayLineDetrender(3).Of(new Sma(2)) })
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
        IIndicator[] roots = [Create(0, 3), Create(2, 3), Create(3, 3), new EhlersAMDetector(3, 2), repeated, repeated];
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
        using var deriv = new EhlersDerivWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage, 2, 3);
        var expected = prices.Select(value => deriv.Next(value, true)).ToArray(); deriv.Reset();
        var actual = prices.Select(value => deriv.Next(value, true)).ToArray();
        Bits(expected.Select(x => x.Line).ToArray(), actual.Select(x => x.Line).ToArray());
        Bits(expected.Select(x => x.Signal).ToArray(), actual.Select(x => x.Signal).ToArray());
        using var clip = new EhlersClipWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.ExponentialMovingAverage, 2, 3, 2);
        expected = prices.Select(value => clip.Next(value, true)).ToArray(); clip.Reset();
        actual = prices.Select(value => clip.Next(value, true)).ToArray();
        Bits(expected.Select(x => x.Line).ToArray(), actual.Select(x => x.Line).ToArray());
        Bits(expected.Select(x => x.Signal).ToArray(), actual.Select(x => x.Signal).ToArray());
        using var delay = new TripleDelayWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.EhlersModifiedOptimumEllipticFilter, 3);
        expected = prices.Select(value => delay.Next(value, true)).ToArray(); delay.Reset();
        actual = prices.Select(value => delay.Next(value, true)).ToArray();
        Bits(expected.Select(x => x.Line).ToArray(), actual.Select(x => x.Line).ToArray());
        Bits(expected.Select(x => x.Signal).ToArray(), actual.Select(x => x.Signal).ToArray());
        using var window = new EhlersSimpleWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.SimpleMovingAverage, 2);
        var expectedWindow = prices.Select(value => window.Next(-value, value, true)).ToArray(); window.Reset();
        var actualWindow = prices.Select(value => window.Next(-value, value, true)).ToArray();
        Bits(expectedWindow.Select(x => x.Line).ToArray(), actualWindow.Select(x => x.Line).ToArray());
        Bits(expectedWindow.Select(x => x.Roc).ToArray(), actualWindow.Select(x => x.Roc).ToArray());
    }

    [Theory]
    [InlineData(int.MinValue, 0, int.MaxValue)] [InlineData(1, 1, 1)]
    [InlineData(2, 3, 5)] [InlineData(7, 2, 3)] [InlineData(2, int.MaxValue, int.MaxValue)]
    public async Task IndependentLagRmsAndSignalPeriodsRetainTheirContracts(int lag, int rms, int signal)
    {
        var bars = ToBars(Enumerable.Range(0, 37).Select(i => (i * 13 % 101 - 50) / 8d).ToArray());
        await Compare([new EhlersSimpleDerivIndicator(lag, signal), new EhlersSimpleClipIndicator(lag, 10, rms, signal)], bars);
    }

    [Fact]
    public async Task ClipSecondLengthRemainsIgnoredAndWindowReadsOriginalOpen()
    {
        var bars = ToBars([1, 3, -2, 4, 0, 2, 5, -1, 7, 0, 0, 2, 3]);
        foreach (var ignored in new[] { int.MinValue, 0, 1, 10, int.MaxValue })
            await Compare([new EhlersSimpleClipIndicator(2, ignored, 3, 2)], bars);
        await Compare([new EhlersSimpleWindowIndicator(2), new EhlersTripleDelayLineDetrender(3)],
            [new(default, -double.MaxValue, 1, -1, double.MaxValue, 1),
             new(default, double.MaxValue, 1, -1, -double.MaxValue, 1), new(default, 0, 1, -1, 0, 1)]);
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

