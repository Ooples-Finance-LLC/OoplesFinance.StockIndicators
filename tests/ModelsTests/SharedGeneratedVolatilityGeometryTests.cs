using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedVolatilityGeometryTests
{
    public static IEnumerable<object[]> Cases =>
        from kind in new[] { 0, 1, 2 }
        from length in new[] { int.MinValue, 0, 1, 2, 5, 14, 4096 }
        select new object[] { kind, length };

    private static IIndicator Create(int kind, int length) => kind switch
    {
        0 => new TypicalPriceVolatility(length),
        1 => new StandardDeviationVolatility(length, 252),
        _ => new ClosedFormDistanceVolatility(length)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task OriginalRangesAndResidualOutputsMatchLegacy(int kind, int length)
    {
        var root = Create(kind, length);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        foreach (var prices in new double[][]
        {
            [], [1], [-0d, 0, double.Epsilon, -double.Epsilon],
            [-double.MaxValue, double.MaxValue, 0, 2, -3, 7],
            [1, Math.BitIncrement(1), Math.BitDecrement(1), 1, 2, 0, 3],
            Enumerable.Range(0, 33).Select(i => 100 + (i * 13 % 101) / 8d).ToArray()
        }) await Compare([root], ToBars(prices));
    }

    [Fact]
    public async Task IndependentPopulationDeviationAndDistanceEndpoints()
    {
        var typical = new TypicalPriceVolatility(2);
        Bar[] bars = [new(default, 1, 1, 1, 1, 1), new(default, 3, 3, 3, 3, 1)];
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(typical).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits([0, 1], run[typical.Value].ToArray());
        var distance = new ClosedFormDistanceVolatility(1);
        bars = [new(default, 1, 1, 1, 1, 1), new(default, 0, 0, 0, 0, 1), new(default, 1, 1, 0, 1, 1)];
        using var endpoints = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(distance).BuildAsync();
        Bits([0, 0, 1], endpoints[distance.Value].ToArray());
        var residual = new StandardDeviationVolatility(1, 252);
        using var flat = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([1, 3, -2, 0]))).ConfigureIndicators(residual).BuildAsync();
        foreach (var output in residual.Outputs) Bits([0, 0, 0, 0], flat[output].ToArray());
    }

    [Fact]
    public async Task UnfillableTypicalWindowAndIgnoredAnnualization()
    {
        var typical = new TypicalPriceVolatility(int.MaxValue);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(ToBars([double.MaxValue, -double.MaxValue, 1]))).ConfigureIndicators(typical).BuildAsync();
        Assert.False(((IndicatorRun)run).HasLegacyRuntime);
        Bits([0, 0, 0], run[typical.Value].ToArray());
        foreach (var factor in new[] { int.MinValue, 0, 1, 252, int.MaxValue })
            await Compare([new StandardDeviationVolatility(2, factor)], ToBars([1, 3, -2, 4, 0, 2]));
        foreach (var root in new IIndicator[] { new StandardDeviationVolatility(int.MaxValue, 252), new ClosedFormDistanceVolatility(int.MaxValue) })
            await Compare([root], ToBars([1, 3, -2, 4]));
    }

    [Fact]
    public async Task MixedRootsDomainOrderAndSourceComponentFallback()
    {
        var bars = ToBars([1, 3, 2, 5, -2, 0, 4]);
        var root = Create(0, 3);
        IIndicator[] roots = [root, Create(1, 3), Create(2, 3), new EhlersRecursiveMedianFilter(3), root];
        await Compare(roots, bars);
        foreach (var fallback in new IIndicator[]
        {
            new StandardDeviationVolatility(3, 252, new Sma(3)),
            new StandardDeviationVolatility(3, 252, new Sma(3), new Ema(3), new Sma(3)),
            new ClosedFormDistanceVolatility(3, new Sma(3)),
            new StandardDeviationVolatility(3, 252).Of(new Sma(2)),
            new ClosedFormDistanceVolatility(3).Of(new Sma(2)),
            new TypicalPriceVolatility(3).Of(new Sma(2))
        })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([fallback]));
            await Compare([fallback], bars);
        }
        foreach (var invalid in new[] { new Bar(default, 1, -1, 0, 1, 1), new Bar(default, 1, 2, -1, 1, 1) })
        {
            var domain = bars.ToArray(); domain[1] = invalid;
            await Compare(roots, domain);
            await Compare(roots.Reverse().ToArray(), domain);
            domain[^1] = new(default, 1, 2, 0, 1, double.NaN);
            await Compare(roots, domain);
        }
        bars[^1] = new(default, 1, 2, 0, 1, double.NaN);
        await Compare(roots, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Null(builder.LastExecution);
    }

    private static Bar[] ToBars(double[] prices) => prices.Select(v => new Bar(default, v / 4, Math.Abs(v), Math.Abs(v) / 2, v, 1)).ToArray();

    [Fact]
    public async Task GraphDomainsFollowRootOrderAfterAllRawFiniteChecks()
    {
        IIndicator[] roots = [new ClosedFormDistanceVolatility(2), new PrimeNumberOscillator(2), new AbsoluteStrengthIndex(2), new VervoortModifiedBollingerBandIndicator(2, 2, 1)];
        Bar[] bars = [new(default, 1, 1, 1, -1, 1), new(default, 1, -1, 0, 1, 1), new(default, double.MaxValue, 1, 0, 1, 1)];
        foreach (var order in new[] { roots, roots.Reverse().ToArray(), new[] { roots[1], roots[3], roots[0], roots[2] } })
        {
            await Compare(order, bars);
            var invalid = bars.ToArray(); invalid[^1] = new(default, 1, 2, 0, 1, double.NaN);
            await Compare(order, invalid);
        }
    }

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

