using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedTrendScoreTests
{
    public static IEnumerable<object[]> Cases =>
        from family in Enumerable.Range(0, 2)
        from period in new[] { int.MinValue, -3, 0, 1, 10, 11, 12, 20, 513, 4096 }
        select new object[] { family, period };
    private static IIndicator Create(int family, int period) => family == 0 ? new ChandeTrendScore(period) : new TrendScore(period);

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
            Enumerable.Range(0, 1025).Select(i => (i * 13 % 101) / 8d - 6).ToArray(),
            Enumerable.Range(0, 65).Select(i => Math.ScaleB(i % 13 - 6d, i % 2 == 0 ? -1022 : 900)).ToArray()
        })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var bars = values.Select((v, i) => new Bar(default, v / 4, v, v, v, i % 7 == 0 ? 0 : i % 7 == 1 ? double.Epsilon : i % 7 == 2 ? double.MaxValue : i % 7 == 3 ? -7 : i % 7)).ToArray();
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
        IIndicator[] roots = [new ChandeTrendScore(13), new TrendScore(15), new Cci(3), new Wwma(3)];
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
    public async Task LargeValidWindowUsesBoundedStartupAndMaxValueRetainsFallback()
    {
        foreach (var family in new[] { 0, 1 })
        {
            Assert.False(ValuesBarExecution.SupportsOwned([Create(family, int.MaxValue)]));
            const int period = int.MaxValue - 1;
            var values = Enumerable.Range(0, 65).Select(i => i % 5 - 2d).ToArray();
            var bars = values.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
            var indicator = Create(family, period);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            var expected = values.Select((value, i) =>
            {
                long score = (value >= 0 ? 1L : -1L) * (period - 10L);
                for (int prior = 0; prior <= i - 11; prior++)
                    score += (value >= values[prior] ? 1 : -1) - (value >= 0 ? 1 : -1);
                return (double)score;
            }).ToArray();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
                Bits(expected, actual[indicator].ToArray());
            }
        }
    }

    public static IEnumerable<object[]> NumericalCases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ChandeTrendScore) || c.IndicatorType == typeof(TrendScore)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(NumericalCases))]
    public async Task ExistingIndependentNumericalAndSourceContractsRemainValid(IndicatorValidationCase testCase)
    {
        await new CountIndicatorNumericalTests().EveryDiscoveredCountConfigurationReceivesAllNumericalClasses(testCase);
        var source = new Sma(3);
        var indicator = ((IndicatorBase)testCase.Factory()).Of(source);
        Assert.False(ValuesBarExecution.SupportsOwned([source, indicator]));
        var bars = Enumerable.Range(0, 64).Select(i => new Bar(default, 4, 15 + i % 7, -5 - i % 3, 2 + i % 11, i % 4)).ToArray();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(source, indicator).ConfigureHistory(history);
            var expected = await Record.ExceptionAsync(() => Builder().ConfigureBehavior(_ => { }).BuildAsync());
            var actual = await Record.ExceptionAsync(() => Builder().BuildAsync());
            Assert.IsType<InvalidOperationException>(expected);
            Assert.Equal(expected!.GetType(), actual?.GetType());
            Assert.Equal(expected.Message, actual?.Message);
        }
    }

    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
