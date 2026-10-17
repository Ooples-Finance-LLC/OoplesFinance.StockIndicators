using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedOhlcVolatilityTests
{
    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover([typeof(IIndicator).Assembly])
        .Where(c => c.IndicatorType == typeof(ParkinsonVolatility) || c.IndicatorType == typeof(RogersSatchellVolatility) || c.IndicatorType == typeof(YangZhangVolatility))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(NumericalCases))]
    public Task PublicConfigurationsRetainIndependentNumericalContracts(IndicatorValidationCase testCase) =>
        new OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);

    public static IEnumerable<object[]> Cases =>
        from kind in new[] { 0, 1, 2 }
        from length in new[] { 1, 2, 5, 20, 4096, 536_870_911, 536_870_912, int.MaxValue }
        select new object[] { kind, length };

    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new ParkinsonVolatility(length),
        1 => new RogersSatchellVolatility(length),
        _ => new YangZhangVolatility(length)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task CachedTermsRetainEveryLegacyFoldBit(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.Equal(kind switch { 0 => length <= 536_870_911, 2 => length < int.MaxValue, _ => true }, ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Range(0, 47).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task UndefinedDenominatorsKeepFullVarianceDivisors()
    {
        var root = new YangZhangVolatility(2);
        Bar[] bars = [new(default, 1, 1, 0, 0, 1), new(default, 2, 2, 1, 1, 1), new(default, 4, 4, 2, 2, 1)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        var actual = run[root.Value].ToArray();
        Assert.Equal(0, actual[0]); Assert.Equal(0, actual[1]);
        Assert.InRange(Math.Abs(actual[2] - Math.Log(2) * Math.Sqrt(252)), 0, 1e-12);
        foreach (var kind in new[] { 0, 1, 2 })
            await Compare([Create(kind, 2)], bars.Concat(bars.Reverse()).ToArray());
    }

    [Fact]
    public async Task EqualCandlesGiveZeroIncludingWarmupAndExpiry()
    {
        foreach (var kind in new[] { 0, 1, 2 })
        {
            var root = Create(kind, 3);
            var bars = Enumerable.Repeat(new Bar(default, 7, 7, 7, 7, 1), 9).ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).BuildAsync();
            foreach (var output in root.Outputs) Bits(new double[9], run[output].ToArray());
        }
    }

    [Fact]
    public void CachedWindowsReplayAfterReset()
    {
        var bars = ToBars([1, 3, -2, 0, 4, 7, 2, 5, 1]);
        foreach (var kind in Enum.GetValues<OhlcVolatilityWindow.Estimator>())
        {
            using var window = new OhlcVolatilityWindow(kind, 3, bars.Length);
            var expected = bars.Select(bar => window.Next(in bar)).ToArray();
            window.Reset();
            Bits(expected, bars.Select(bar => window.Next(in bar)).ToArray());
            window.Dispose();
        }
    }

    [Fact]
    public async Task NonpositiveLegacyPeriodsAndSelectedSourcesKeepTheirRoutes()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4]);
        foreach (var kind in new[] { 0, 1, 2 })
        foreach (var length in new[] { int.MinValue, -1, 0 })
        {
            var root = Create(kind, length);
            Assert.Equal(kind == 2, ValuesBarExecution.SupportsOwned([root]));
            await Compare([root], bars);
        }
        foreach (var selected in new IIndicator[] { new ParkinsonVolatility(3).Of(new Sma(2)), new RogersSatchellVolatility(3).Of(new Sma(2)), new YangZhangVolatility(3).Of(new Sma(2)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([selected]));
            await Compare([selected], bars);
        }
        var repeated = Create(2, 3);
        IIndicator[] roots = [Create(0, 2), Create(1, 3), repeated, new ClosedFormDistanceVolatility(3), repeated];
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

