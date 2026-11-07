using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedDeviationFamilyTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(EhlersDeviationScaledMovingAverage) || c.IndicatorType == typeof(EhlersFisherizedDeviationScaledOscillator) || c.IndicatorType == typeof(EhlersDeviationScaledSuperSmoother)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(NumericalCases))]
    public Task IndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases => from kind in new[] { 0, 1, 2 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 20, 4096 } select new object[] { kind, length };
    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new EhlersDeviationScaledMovingAverage(length),
        1 => new EhlersFisherizedDeviationScaledOscillator(length),
        _ => new EhlersDeviationScaledSuperSmoother(length, 2)
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

    [Theory]
    [InlineData(1_073_741_823)] [InlineData(1_073_741_824)] [InlineData(int.MaxValue)]
    public async Task DoubledPeriodOverflowRetainsLegacyFallback(int length)
    {
        var root = new EhlersDeviationScaledMovingAverage(length);
        Assert.Equal(length <= 1_073_741_823, ValuesBarExecution.SupportsOwned([root]));
        await Compare([root], []);
        await Compare([root], ToBars([.01, .02, -.03]));
    }

    [Fact]
    public async Task IgnoredPoleOptionAndSelectedSourcesKeepTheirContracts()
    {
        var bars = ToBars([.01, .02, -.03, .05, -.02, 0, .04]);
        foreach (var poles in new[] { int.MinValue, 0, 1, 2, 3, int.MaxValue })
            await Compare([new EhlersDeviationScaledSuperSmoother(3, poles)], bars);
        foreach (var root in new IIndicator[] { new EhlersDeviationScaledMovingAverage(3).Of(new Sma(2)), new EhlersFisherizedDeviationScaledOscillator(3).Of(new Sma(2)), new EhlersDeviationScaledSuperSmoother(3, 2).Of(new Sma(2)) })
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
        IIndicator[] roots = [Create(0, 3), Create(2, 3), repeated, repeated];
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
        var scaled = new DeviationScaledWindow(3, 6);
        var expected = prices.Select(value => scaled.Next(value, true, out _)).ToArray();
        scaled.Reset();
        Bits(expected, prices.Select(value => scaled.Next(value, true, out _)).ToArray());
        var smoother = new DeviationSuperSmootherWindow(OoplesFinance.StockIndicators.Enums.MovingAvgType.EhlersHannMovingAverage, 3, 50);
        expected = prices.Select(value => smoother.Next(value, true)).ToArray();
        smoother.Reset();
        Bits(expected, prices.Select(value => smoother.Next(value, true)).ToArray());
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

