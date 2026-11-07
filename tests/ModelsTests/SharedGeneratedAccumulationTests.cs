using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAccumulationTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 4)
        from period in (family == 2 ? new[] { 1 } : family == 3 ? new[] { -3, 1, 3, 20, 4096 } : new[] { -3, 1, 3, 20, int.MaxValue })
        select new object[] { family, period };

    private static IIndicator Create(int family, int period) => family switch { 0 => new AccumulationDistributionLine(period), 1 => new EaseOfMovement(period), 2 => new WilliamsAccumulationDistribution(), _ => new SmoothedWilliamsAccumulationDistribution(period) };

    [Theory, MemberData(nameof(Cases))]
    public async Task StagedRoundingOwnershipAndExceptionalValuesMatchLegacy(int family, int period)
    {
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            Enumerable.Range(0, 1025).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (bool extremeVolume in new[] { false, true })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select((v, i) => new Bar(default, 0, Math.Abs(v), -Math.Abs(v) / 2, v,
                extremeVolume ? (i % 4) switch { 0 => 0, 1 or 2 => double.MaxValue, _ => double.Epsilon } : i % 7)).ToArray();
            var indicator = Create(family, period);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Builder();
                var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) { Assert.Null(builder.LastExecution); continue; }
                Assert.NotNull(expected);
                Assert.NotNull(actual);
                if (history == IndicatorHistoryMode.Full) Assert.False(((IndicatorRun)actual!).HasLegacyRuntime);
                foreach (var output in indicator.Outputs) Bits(expected![output].ToArray(), actual![output].ToArray());
                if (bars.Length > 0)
                {
                    Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                actual.Dispose();
                Array.Clear(bars);
                foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    [Fact]
    public async Task MixedRootsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new AccumulationDistributionLine(3), new EaseOfMovement(7), new WilliamsAccumulationDistribution(), new SmoothedWilliamsAccumulationDistribution(3), new Wwma(3)];
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1, i % 11 - 5d, 1)).ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var indicator in roots) foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
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
    public async Task CustomVolumeComponentsRetainGraphExecution()
    {
        foreach (IIndicator indicator in new IIndicator[] { new AccumulationDistributionLine(7, new Wma(3)), new EaseOfMovement(7, 1000, new Wma(3)), new EaseOfMovement(7, 1000, new Wma(3), new Ema(3)), new SmoothedWilliamsAccumulationDistribution(7, new Ema(3)) })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([indicator]));
            var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1, i % 11 - 5d, 1)).ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
            using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Builder().BuildAsync();
            Assert.True(((IndicatorRun)actual).HasLegacyRuntime);
            foreach (var output in indicator.Outputs) Bits(expected[output].ToArray(), actual[output].ToArray());
        }
    }

    [Theory]
    [InlineData(-double.MaxValue)]
    [InlineData(-1d)]
    [InlineData(0d)]
    [InlineData(double.Epsilon)]
    [InlineData(double.MaxValue)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public async Task EaseDivisorPreservesBitsAndLegacyValidation(double divisor)
    {
        var indicator = new EaseOfMovement(3, divisor);
        Assert.Equal(double.IsFinite(divisor), ValuesBarExecution.SupportsOwned([indicator]));
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, i % 7, -(i % 11), 0, i % 3 == 0 ? double.Epsilon : i % 5)).ToArray();
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var actualError = await Record.ExceptionAsync(async () => actual = await Builder().BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is null)
                    foreach (var output in indicator.Outputs) Bits(expected![output].ToArray(), actual![output].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
