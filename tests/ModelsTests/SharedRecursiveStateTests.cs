using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedRecursiveStateTests
{
    [Theory]
    [InlineData(-3)] [InlineData(0)] [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(int.MaxValue)]
    public async Task RecursiveStatesPreserveAllOutputsStartupAndOwnedHistory(int period)
    {
        foreach (int count in new[] { 0, 1, 65, 1025 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0,
                1 + (i % 11) / 4d, -(i % 7) / 8d, (i % 9 - 4) / 4d, i % 5 - 2)).ToArray();
            IIndicator[] indicators = [new Ema(period), new Adl(period)];
            foreach (var indicator in indicators) await Compare(bars, [indicator]);
            await Compare(bars, indicators);
            Assert.True(ValuesBarExecution.SupportsOwned(indicators));
            using var saved = await Builder(bars, indicators, IndicatorHistoryMode.Full).BuildAsync();
            var expected = indicators.SelectMany(i => i.Outputs).Select(o => saved[o].ToArray()).ToArray();
            var original = bars.ToArray(); Array.Clear(bars);
            using var later = await Builder(bars, indicators, IndicatorHistoryMode.Full).BuildAsync();
            int slot = 0;
            foreach (var output in indicators.SelectMany(i => i.Outputs)) Bits(expected[slot++], saved[output].ToArray());
            int index = 0;
            await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        }
    }

    [Fact]
    public async Task RecursiveStatesPreserveSubnormalSignsWideValuesAndInputPrecedence()
    {
        var tiny = new[] { new Bar(default, 0, 1, 0, 0, double.Epsilon),
            new Bar(default, 0, 1, 0, 1, double.Epsilon), new Bar(default, 0, 1, 0, 0, 0) };
        await Compare(tiny, [new Adl(3)]);
        var adl = new Adl(3);
        using var result = await Builder(tiny, [adl], IndicatorHistoryMode.LatestOnly).BuildAsync();
        // RocBankValue.Publish canonicalizes a rounded zero; EMA publishes its
        // exact mean directly and therefore retains the negative underflow sign.
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(result[adl.Outputs[1]][1]));
        var ema = new Ema(3);
        var underflow = new[] { new Bar(default, 0, 1, 0, -double.Epsilon, 1), new Bar(default, 0, 1, 0, 0, 1) };
        await Compare(underflow, [ema]);
        using var mean = await Builder(underflow, [ema], IndicatorHistoryMode.LatestOnly).BuildAsync();
        Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(mean[ema][1]));
        double[] prices = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1];
        var bars = prices.Select(v => new Bar(default, 0, double.Epsilon, 0, v, double.MaxValue)).ToArray();
        foreach (int period in new[] { 1, 3, int.MaxValue })
        {
            await Compare(bars, [new Ema(period)]);
            await Compare(bars, [new Adl(period)]);
            var invalid = bars.ToArray(); invalid[^1] = new Bar(default, 0, 1, 0, 1, double.NaN);
            await Compare(invalid, [new Ema(period), new Adl(period)]);
        }
    }

    [Fact]
    public async Task RecursiveStatesRetainCancellationCompositionAndGpuRejection()
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, 1, 1), 65).ToArray();
        IIndicator[] indicators = [new Ema(3), new Adl(3)];
        foreach (var indicator in indicators)
        {
            var builder = Builder(bars, [indicator], IndicatorHistoryMode.Full);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancelled.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        await Compare(bars, [new Ema(3).Of(new Sma(3))]);
    }

    private static async Task Compare(Bar[] bars, IIndicator[] indicators)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var builder = Builder(bars, indicators, history);
            var expectedError = await Record.ExceptionAsync(async () => expected =
                await Builder(bars, indicators, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError); Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message); Assert.Null(builder.LastExecution);
                }
                else
                {
                    Assert.Null(actualError);
                    foreach (var output in indicators.SelectMany(i => i.Outputs)) Bits(expected![output].ToArray(), actual![output].ToArray());
                    if (bars.Length > 0) Assert.Equal(expected!.Latest.IsWarmedUp, actual!.Latest.IsWarmedUp);
                }
            }
        }
    }
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator[] indicators, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
    private static void Bits(double[] expected, double[] actual) => Assert.Equal(
        expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
