using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedLeastSquaresTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 2)
        from period in new[] { int.MinValue, -3, 1, 2, 3, 20, 4096 }
        select new object[] { family, period };

    private static IIndicator Create(int family, int period) => family switch
    {
        0 => new SimplifiedLeastSquaresMovingAverage(period), _ => new LinearExtrapolation(period)
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
            Enumerable.Range(0, 1025).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select(v => new Bar(default, v / 4, Math.Abs(v), -Math.Abs(v) / 2, v, 1)).ToArray();
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
        IIndicator[] roots = [new SimplifiedLeastSquaresMovingAverage(2), new LinearExtrapolation(3), new Wwma(3)];
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
    public async Task VeryLargePeriodsUseOriginalWeightsAndUnfilledStartup()
    {
        const int period = int.MaxValue / 10;
        var bars = Enumerable.Range(0, 17).Select(i => new Bar(default, 0, 1, -1, i - 8d, 1)).ToArray();
        for (int family = 0; family < 2; family++)
        {
            var indicator = Create(family, period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.False(((IndicatorRun)run).HasLegacyRuntime);
            var expected = new double[bars.Length];
            if (family == 0)
                for (int i = 0; i < bars.Length; i++)
                {
                    var sum = new ExactMeanAccumulator();
                    for (int lag = 0; lag <= i; lag++) sum.Add(bars[i - lag].Close, 6L * (period - lag) - 2L * (period + 1L));
                    expected[i] = sum.Mean((long)period * (period + 1L));
                }
            foreach (var output in indicator.Outputs) Bits(expected, run[output].ToArray());
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(4096)]
    public void BoundedSimplifiedHistoryMatchesOriginalIncludingPreviewAndReset(int period)
    {
        var values = Enumerable.Range(0, 33).Select(i => i % 3 == 0 ? double.MaxValue / 16 : (i - 16) * double.Epsilon).ToArray();
        using var original = new SimplifiedLeastSquaresWindow(period);
        using var bounded = new SimplifiedLeastSquaresWindow(period, values.Length);
        for (int replay = 0; replay < 2; replay++)
        {
            original.Reset(); bounded.Reset();
            foreach (var value in values)
            {
                Assert.Equal(BitConverter.DoubleToInt64Bits(original.Next(-double.Epsilon, false)), BitConverter.DoubleToInt64Bits(bounded.Next(-double.Epsilon, false)));
                Assert.Equal(BitConverter.DoubleToInt64Bits(original.Next(value, true)), BitConverter.DoubleToInt64Bits(bounded.Next(value, true)));
            }
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
