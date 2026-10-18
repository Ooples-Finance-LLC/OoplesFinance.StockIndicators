using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedPriceProjectionTests
{
    public static IEnumerable<object[]> Configurations => from operation in Enumerable.Range(0, 4)
        from length in new[] { 0, 1, 20, int.MaxValue } select new object[] { operation, length };

    private static IndicatorBase Create(int operation, int length) => operation switch
    {
        0 => new MedianPrice(length),
        1 => new TypicalPrice(length),
        2 => new WeightedClose(length),
        _ => new FullTypicalPrice(length)
    };

    [Theory, MemberData(nameof(Configurations))]
    public async Task GeneratedProjectionsPreserveEvaluatorBitsHistoryAndWarmupMetadata(int operation, int length)
    {
        foreach (int count in new[] { 0, 1, 8193 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                10 + i % 17 / 16d, 20 + i % 23 / 8d, 5 + i % 7 / 4d, 15 + i % 19 / 8d, i)).ToArray();
            var original = bars.ToArray();
            var indicator = Create(operation, length);
            using var expected = await Builder(bars, indicator, IndicatorHistoryMode.Full).ConfigureBehavior(_ => { }).BuildAsync();
            using var full = await Builder(bars, indicator, IndicatorHistoryMode.Full).BuildAsync();
            using var latest = await Builder(bars, indicator, IndicatorHistoryMode.LatestOnly).BuildAsync();
            Assert.False(((IndicatorRun)full).HasLegacyRuntime);
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
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ExceptionalFiniteValuesInputErrorsAndComposedFallbackKeepEvaluatorContracts(int operation)
    {
        double[] inputs = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1, Math.BitIncrement(1d)];
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default,
            inputs[i % 8], inputs[(i + 1) % 8], inputs[(i + 3) % 8], inputs[(i + 5) % 8], 1)).ToArray();
        var indicator = Create(operation, 3);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = Builder(bars, indicator, history);
            using var expected = await Builder(bars, indicator, history).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await builder.BuildAsync();
            Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
            var last = bars[^1];
            bars[^1] = new Bar(default, 0, 1, 0, 0, double.NaN);
            var expectedError = await Record.ExceptionAsync(() => Builder(bars, indicator, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(() => builder.BuildAsync());
            Assert.NotNull(expectedError);
            Assert.NotNull(actualError);
            Assert.Equal(expectedError.GetType(), actualError.GetType());
            Assert.Equal(expectedError.Message, actualError.Message);
            Assert.Null(builder.LastExecution);
            bars[^1] = last;
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        indicator.Of(new Sma(3));
        Assert.False(ValuesBarExecution.IsPointwise(indicator));
        var composed = Builder(bars, indicator, IndicatorHistoryMode.Full);
        var ordinary = Builder(bars, indicator, IndicatorHistoryMode.Full).ConfigureBehavior(_ => { });
        var expectedCompositionError = await Assert.ThrowsAsync<InvalidOperationException>(() => ordinary.BuildAsync());
        var actualCompositionError = await Assert.ThrowsAsync<InvalidOperationException>(() => composed.BuildAsync());
        Assert.Equal(expectedCompositionError.Message, actualCompositionError.Message);
        Assert.Null(composed.LastExecution);
    }

    [Fact]
    public async Task MixedRequestsAndLaterLegacyBuildPreserveOwnedData()
    {
        var bars = Enumerable.Range(0, 40).Select(i => new Bar(default, i, i + 2, i - 1, i + 1, 1)).ToArray();
        var indicators = new IIndicator[] { new MedianPrice(3), new TypicalPrice(3), new CandleArithmetic(CandleArithmeticOperation.Add) };
        using var expected = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators)
            .ConfigureBehavior(_ => { }).BuildAsync();
        using var actual = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
        foreach (var indicator in indicators) Bits(expected[indicator].ToArray(), actual[indicator].ToArray());
        var builder = Builder(bars, new WeightedClose(3), IndicatorHistoryMode.Full);
        using var saved = await builder.BuildAsync();
        var snapshot = saved.Latest;
        Array.Clear(bars);
        SeriesHandle median = default;
        using var legacy = builder.ConfigureIndicators(catalog => median = catalog.MedianPrice(3)).Build();
        legacy.Start();
        Bits(expected[indicators[0]].ToArray(), legacy.GetSeries(median).ToArray());
        Assert.Equal(40d, snapshot.Bar.Close);
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator indicator, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
