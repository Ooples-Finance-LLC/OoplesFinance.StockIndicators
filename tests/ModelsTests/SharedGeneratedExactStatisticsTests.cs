using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedExactStatisticsTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 3)
        from period in new[] { -3, 1, 2, 3, 20, 4096 }
        select new object[] { family, period };

    private static IIndicator Create(int family, int period) => family switch { 0 => new Variance(period), 1 => new CoefficientOfVariation(period), _ => new StandardError(period) };

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
        IIndicator[] roots = [new Variance(3), new CoefficientOfVariation(7), new StandardError(5), new Wwma(3)];
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

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task HugePeriodsKeepStartupZerosWarmupAndRawValidation(int family)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1,
                i % 2 == 0 ? double.MaxValue : -double.MaxValue, 1)).ToArray();
            var reference = Create(family, count + 1);
            var huge = Create(family, int.MaxValue);
            StockIndicatorBuilder Builder(IIndicator indicator) => new StockIndicatorBuilder()
                .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
            using var expected = await Builder(reference).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Builder(huge).BuildAsync();
            Bits(expected[reference.Outputs[0]].ToArray(), actual[huge.Outputs[0]].ToArray());
            Assert.All(actual[huge.Outputs[0]].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
            if (history == IndicatorHistoryMode.Full) Assert.False(((IndicatorRun)actual).HasLegacyRuntime);
            if (count == 0) continue;
            Assert.False(actual.Latest.IsWarmedUp);
            bars[^1] = new(default, 0, 1, -1, 0, double.NaN);
            var error = await Record.ExceptionAsync(() => Builder(huge).BuildAsync());
            var expectedError = await Record.ExceptionAsync(() => Builder(reference).ConfigureBehavior(_ => { }).BuildAsync());
            Assert.IsType<ArgumentOutOfRangeException>(error);
            Assert.Equal(expectedError!.Message, error.Message);
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
