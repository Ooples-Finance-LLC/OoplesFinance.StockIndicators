using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedBollingerFamilyTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 4)
        from period in new[] { int.MinValue, -3, 0, 1, 2, 3, 20, 4096 }
        select new object[] { family, period };
    private static IIndicator Create(int family, int period) => family switch
    {
        0 => new BollingerBands(period), 1 => new BollingerBandsWidth(period),
        2 => new BollingerBandsPercentB(period), 3 => new NarrowSidewaysChannel(period, 3), _ => throw new ArgumentOutOfRangeException(nameof(family))
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task StagedRoundingOwnershipAndExceptionalValuesMatchLegacy(int family, int period)
    {
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            [double.Epsilon, double.MaxValue, 1, 2, 3, 4, 5, 6, 7],
            [1, -1, 0, 0, 2, -2, 0, 3, -3],
            Enumerable.Range(0, 257).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select((v, i) => new Bar(default, v / 4, v, v / 2, v, i % 7 == 0 ? 0 : i % 7 == 1 ? double.Epsilon : i % 7 == 2 ? double.MaxValue : i % 7 == 3 ? -7 : i % 7)).ToArray();
            var indicator = Create(family, period);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var referenceBuilder = Builder().ConfigureBehavior(_ => { });
                var expectedError = await Record.ExceptionAsync(async () => expected = await referenceBuilder.BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected);
                Assert.NotNull(actual);
                if (history == IndicatorHistoryMode.Full && ValuesBarExecution.SupportsOwned([indicator])) Assert.False(((IndicatorRun)actual!).HasLegacyRuntime);
                foreach (var output in indicator.Outputs) Bits(expected![output].ToArray(), actual![output].ToArray());
                if (bars.Length > 0)
                {
                    Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                actual.Dispose();
                Array.Clear(bars);
                foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual![output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    [Fact]
    public async Task MixedRootsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new BollingerBands(3), new BollingerBandsWidth(3), new BollingerBandsPercentB(3), new NarrowSidewaysChannel(3, 3), new Cci(3), new Wwma(3)];
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1, i % 11 - 5d, 1)).ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var indicator in roots) foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual![output].ToArray());
        }
        bars[0] = new(default, 0, 1, -1, double.MaxValue, 1);
        bars[1] = bars[0];
        bars[^1] = new(default, 0, 1, -1, 0, double.NaN);
        var referenceError = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
        var actualError = await Record.ExceptionAsync(() => builder.BuildAsync());
        Assert.IsType<ArgumentOutOfRangeException>(referenceError);
        Assert.Equal(referenceError!.Message, actualError!.Message);
        Assert.Equal(referenceError.GetType(), actualError.GetType());
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
    }

    [Fact]
    public async Task ExplicitAverageRetainsComponentFallback()
    {
        IIndicator indicator = new BollingerBands(3, 1, new Wma(3));
        Assert.False(ValuesBarExecution.SupportsOwned([indicator]));
        await CompareValueOrError(indicator);
    }

    [Theory]
    [InlineData(0d)]
    [InlineData(-2d)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task MultiplierBoundariesRetainBitsOrFallbackErrors(double multiplier)
    {
        foreach (IIndicator indicator in new IIndicator[] { new BollingerBands(3, multiplier), new BollingerBandsPercentB(3, multiplier), new NarrowSidewaysChannel(3, multiplier) })
        {
            Assert.Equal(indicator is NarrowSidewaysChannel || double.IsFinite(multiplier), ValuesBarExecution.SupportsOwned([indicator]));
            await CompareValueOrError(indicator);
        }
    }

    [Fact]
    public async Task HugeWindowsRetainZeroStartup()
    {
        var bars = new[] { 1d, 3d, 5d, 7d }.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (var family in Enumerable.Range(0, 4))
        {
            var indicator = Create(family, int.MaxValue);
            using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
            foreach (var output in indicator.Outputs) Bits(new double[bars.Length], actual[output].ToArray());
        }
    }

    [Fact]
    public async Task DefaultComponentIsPublishedOnlyWhenConfiguredAndExplicitAveragesRetainFallback()
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool configureComponent in new[] { false, true })
        {
            var bands = new BollingerBands(3);
            var component = bands.Components[0];
            var bars = Enumerable.Range(0, 17).Select(i => new Bar(default, 0, 1, -1, i - 8d, 1)).ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(configureComponent ? [bands, component] : [bands]).ConfigureHistory(history);
            using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Builder().BuildAsync();
            foreach (var output in bands.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
            if (configureComponent) Bits(expected[component].ToArray(), actual[component].ToArray());
            else
            {
                Assert.Throws<KeyNotFoundException>(() => expected[component].ToArray());
                Assert.Throws<KeyNotFoundException>(() => actual[component].ToArray());
            }
        }
        foreach (IIndicator indicator in new IIndicator[] { new BollingerBands(3, 2, new Sma(5)), new NarrowSidewaysChannel(3, 2, new Wma(3)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([indicator]));
            await CompareValueOrError(indicator);
        }
    }

    private static async Task CompareValueOrError(IIndicator indicator)
    {
        var bars = new[] { -double.Epsilon, double.Epsilon, 0d, double.MaxValue, -double.MaxValue, 2d, -1d }
            .Select(v => new Bar(default, 0, v, v, v, 1)).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var referenceError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(referenceError?.GetType(), actualError?.GetType());
                Assert.Equal(referenceError?.Message, actualError?.Message);
                if (referenceError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected); Assert.NotNull(actual);
                foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
