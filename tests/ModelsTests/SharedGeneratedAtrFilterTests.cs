using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedAtrFilterTests
{
    public static IEnumerable<object[]> Cases =>
        (from family in Enumerable.Range(0, 6)
         from period in new[] { int.MinValue, -3, 1, 2, 3, 20, 4096 }
         select new object[] { family, period, 5d })
        .Concat(new[] { 0d, -1d, double.Epsilon, double.MaxValue, -double.MaxValue }
            .Select(cap => new object[] { 1, 3, cap }));

    private static IIndicator Create(int family, int period, double cap = 5) => family switch
    {
        0 => new AtrFilteredEma(period),
        1 => new AtrFilteredExponentialMovingAverage(period, min: cap),
        2 => new AtrFilteredExponentialMovingAverage(5, atrLength: period, min: cap),
        3 => new AtrFilteredExponentialMovingAverage(5, stdDevLength: period, min: cap),
        4 => new AtrFilteredExponentialMovingAverage(5, lbLength: period, min: cap),
        _ => new AtrFilteredExponentialMovingAverage(period, period, period, period, cap)
    };

    [Theory, MemberData(nameof(Cases))]
    public async Task StagedRoundingOwnershipAndExceptionalValuesMatchLegacy(int family, int period, double cap)
    {
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            [double.Epsilon, double.MaxValue, 1, 2, 3, 4, 5, 6, 7],
            [1, -1, 0, 0, 2, -2, 0, 3, -3],
            // A negative maximum cap grows the legacy extended exponent by about
            // 1024 bits per bar. Keep extreme-cap coverage beyond every default
            // window without multiplying that stress by the long-history case.
            Enumerable.Range(0, Math.Abs(cap) == double.MaxValue ? 65 : 1025)
                .Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select(v => new Bar(default, v / 4, Math.Abs(v), -Math.Abs(v) / 2, v, 1)).ToArray();
            var indicator = Create(family, period, cap);
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
        IIndicator[] roots = [.. Enumerable.Range(0, 6).Select(family => Create(family, family + 2)), new Wwma(3)];
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
    public async Task NonfiniteCapsRetainLegacyBehaviorIncludingEmptyInput()
    {
        IIndicator[] indicators = [new AtrFilteredExponentialMovingAverage(min: double.NaN), new AtrFilteredExponentialMovingAverage(min: double.PositiveInfinity), new AtrFilteredExponentialMovingAverage(min: double.NegativeInfinity)];
        foreach (var count in new[] { 0, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1, i % 11 - 5d, 1)).ToArray();
            foreach (var indicator in indicators)
            {
                Assert.False(ValuesBarExecution.SupportsOwned([indicator]));
                StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
                IIndicatorRun? expected = null, actual = null;
                try
                {
                    var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                    var actualError = await Record.ExceptionAsync(async () => actual = await Builder().BuildAsync());
                    Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                    Assert.Equal(expectedError?.Message, actualError?.Message);
                    if (expectedError is null)
                    {
                        if (bars.Length > 0) Assert.True(((IndicatorRun)actual!).HasLegacyRuntime);
                        foreach (var output in indicator.Outputs) Bits(expected![output].ToArray(), actual![output].ToArray());
                    }
                }
                finally { expected?.Dispose(); actual?.Dispose(); }
            }
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
