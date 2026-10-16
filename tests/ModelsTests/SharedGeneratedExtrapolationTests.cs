using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedExtrapolationTests
{
    public static IEnumerable<object[]> Cases =>
        from triple in new[] { false, true }
        from period in new[] { -3, 1, 3, 20, int.MaxValue }
        select new object[] { triple, period };

    private static IndicatorBase Create(bool triple, int period) => triple ? new Tema(period) : new Dema(period);

    [Theory, MemberData(nameof(Cases))]
    public async Task StagedRoundingOwnershipAndExceptionalValuesMatchLegacy(bool triple, int period)
    {
        foreach (var values in new double[][]
        {
            [], [1], [-0d, 0d, -double.Epsilon, 0d, double.Epsilon],
            [double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 0],
            Enumerable.Range(0, 1025).Select(i => 100 + (i * 13 % 101) / 8d).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select((v, i) => new Bar(default, i, i + 1, i - 1, v, 1)).ToArray();
            var indicator = Create(triple, period);
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
                if (history == IndicatorHistoryMode.Full) Assert.False(((IndicatorRun)actual!).HasLegacyRuntime);
                Bits(expected![indicator].ToArray(), actual![indicator].ToArray());
                if (bars.Length > 0)
                {
                    Assert.Equal(expected.Latest.Bar, actual.Latest.Bar);
                    Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                }
                actual.Dispose();
                Array.Clear(bars);
                Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    [Fact]
    public async Task MixedRootsRepeatedBuildsAndInvalidTailPreserveStateAndValidationPriority()
    {
        IIndicator[] roots = [new Dema(3), new Tema(7), new Ema(5), new Wwma(3)];
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1, i % 11 - 5d, 1)).ToArray();
        StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators([.. roots, roots[0]]);
        using var expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync();
        var builder = Builder();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            using var actual = await builder.BuildAsync();
            foreach (var indicator in roots) Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
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

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
