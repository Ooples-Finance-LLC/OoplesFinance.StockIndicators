using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedGeneratedStateTests
{
    public static IEnumerable<object[]> Configurations => from operation in Enumerable.Range(0, 3)
        from period in new[] { 1, 3, 20, int.MaxValue } select new object[] { operation, period };
    private static IIndicator Create(int operation, int period) => operation switch
    {
        0 => new HighestHigh(period),
        1 => new LowestLow(period),
        _ => new TrueRange(period)
    };

    [Theory, MemberData(nameof(Configurations))]
    public async Task FiniteStatesMatchEvaluatorBitsStartupTiesAndSnapshots(int operation, int period)
    {
        double[] values = [-0d, 0d, 1, 1, -.25, 2, -2, .25, 1, -0d, 0d];
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                .5, values[i % values.Length], values[(i + 2) % values.Length], values[(i + 5) % values.Length], 1)).ToArray();
            var indicator = Create(operation, period);
            using var expected = await Builder(bars, indicator, IndicatorHistoryMode.Full).ConfigureBehavior(_ => { }).BuildAsync();
            using var full = await Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync();
            using var latest = await Builder(bars, indicator, IndicatorHistoryMode.LatestOnly).BuildAsync();
            Assert.False(((IndicatorRun)full).HasLegacyRuntime);
            var original = bars.ToArray();
            Array.Clear(bars);
            Bits(expected[indicator].ToArray(), full[indicator].ToArray());
            Bits(expected[indicator].ToArray(), latest[indicator].ToArray());
            int index = 0;
            await foreach (var snapshot in full) Assert.Equal(original[index++], snapshot.Bar);
            Assert.Equal(count, index);
            if (count > 0)
            {
                Assert.Equal(expected.Latest.IsWarmedUp, full.Latest.IsWarmedUp);
                Assert.Equal(expected.Latest.IsWarmedUp, latest.Latest.IsWarmedUp);
                Assert.Equal(original[^1], latest.Latest.Bar);
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ExtremeValuesAndInputBeforeOutputErrorsRetainTheirContracts(int operation)
    {
        double[] inputs = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1, -1];
        var bars = inputs.Select((v, i) => new Bar(default, v, v, inputs[(i + 1) % inputs.Length], v, 1)).ToArray();
        var indicator = Create(operation, 3);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        foreach (bool invalid in new[] { false, true })
        {
            var last = bars[^1];
            if (invalid) bars[^1] = new Bar(default, 0, 1, 0, 0, double.NaN);
            var ordinary = Builder(bars, indicator, history).ConfigureBehavior(_ => { });
            var builder = Builder(bars, indicator, history);
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () => expected = await ordinary.BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    Assert.Null(builder.LastExecution);
                }
                else
                {
                    Assert.Null(actualError);
                    Assert.NotNull(actual);
                    Bits(expected![indicator].ToArray(), actual[indicator].ToArray());
                }
            }
            bars[^1] = last;
        }
    }

    [Fact]
    public async Task MixedRootsNonpositivePeriodsAndRequiredGpuUseTheirQualifiedRoutes()
    {
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, i % 7, i % 13, i % 5, i % 11, 1)).ToArray();
        var roots = new[] { Create(0, 20), Create(1, 20), Create(2, 20), new FirstValueEma(3) };
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots)
            .ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots).ConfigureHistory(history);
            using var actual = await builder.BuildAsync();
            foreach (var indicator in roots) Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
        }
        foreach (int period in new[] { 0, -3 })
        foreach (int operation in new[] { 0, 1 })
        {
            var indicator = Create(operation, period);
            Assert.False(ValuesBarExecution.SupportsOwned(new[] { indicator }));
            using var ordinary = await Builder(bars, indicator, IndicatorHistoryMode.Full).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync();
            Bits(ordinary[indicator].ToArray(), actual[indicator].ToArray());
        }
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
