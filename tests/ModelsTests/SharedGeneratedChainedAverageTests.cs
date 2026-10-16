using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedChainedAverageTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 3)
        from period in (family == 2 ? new[] { int.MinValue, -3, 1, 2, 3, 20, 4096, int.MaxValue } : new[] { -3, 1, 2, 3, 20, 4096 })
        select new object[] { family, period };

    private static IIndicator Create(int family, int period) => family switch { 0 => new ZeroLowLagMovingAverage(period), 1 => new ZeroLagTripleExponentialMovingAverage(period), _ => new SlowSmoothedMovingAverage(period) };

    [Theory, MemberData(nameof(Cases))]
    public async Task StagedRoundingOwnershipAndExceptionalValuesMatchLegacy(int family, int period)
    {
        foreach (var values in new double[][]
        {
            [], [1], [4, 4, 1, 4, 1, 1, 4, 4, 0], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
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
            bool shared = family != 2 || period != int.MinValue;
            Assert.Equal(shared, ValuesBarExecution.SupportsOwned([indicator]));
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
                if (history == IndicatorHistoryMode.Full) Assert.Equal(!shared, ((IndicatorRun)actual!).HasLegacyRuntime);
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
        IIndicator[] roots = [new ZeroLowLagMovingAverage(3), new ZeroLagTripleExponentialMovingAverage(7), new SlowSmoothedMovingAverage(5), new Wwma(3)];
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
    public async Task CustomAverageComponentsRetainGraphExecution()
    {
        foreach (IIndicator indicator in new IIndicator[] { new SlowSmoothedMovingAverage(7, new Ema(3)) })
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

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
