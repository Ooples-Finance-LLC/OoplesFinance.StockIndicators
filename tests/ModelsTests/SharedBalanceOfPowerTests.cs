using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedBalanceOfPowerTests
{
    [Fact]
    public async Task PeriodOnePreservesFirstNegativeZeroSignalNormalization()
    {
        var bars = new[]
        {
            new Bar(default, double.Epsilon, double.MaxValue, 0, 0, 1),
            new Bar(default, double.Epsilon, double.MaxValue, 0, 0, 1),
            new Bar(default, 0, 1, 0, 1, 1)
        };
        var power = new BalanceOfPower(1);
        using var expected = await Build(bars, [power]).ConfigureBehavior(_ => { }).BuildAsync();
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var actual = await Build(bars, [power]).ConfigureHistory(history).BuildAsync();
            Compare(expected, actual, power);
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(actual[power][0]));
            Assert.Equal(0, BitConverter.DoubleToInt64Bits(actual[power.BopSignal][0]));
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(actual[power.BopSignal][1]));
        }
    }

    [Theory]
    [InlineData(-1)] [InlineData(1)] [InlineData(3)] [InlineData(14)] [InlineData(int.MaxValue)]
    public async Task BothOutputsRetainEvaluatorBitsStartupAndOwnedHistory(int period)
    {
        foreach (int count in new[] { 0, 1, 8193 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 10 + i % 11,
                30 + i % 7, 5 + i % 3, 12 + i % 17, 1)).ToArray();
            var indicator = new BalanceOfPower(period);
            Assert.True(ValuesBarExecution.SupportsOwned([indicator]));
            using var expected = await Build(bars, [indicator]).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = Build(bars, [indicator]).ConfigureHistory(history);
                using var actual = await builder.BuildAsync();
                if (history == IndicatorHistoryMode.Full)
                    Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                Compare(expected, actual, indicator);
                if (count > 0) Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                if (history == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(count, index);
                }
            }
        }
    }

    [Fact]
    public async Task ExtremeValuesOverflowInputPrecedenceAndMixedRootsPreserveContracts()
    {
        double[] values = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1, Math.BitIncrement(1d)];
        foreach (bool mixed in new[] { false, true })
        foreach (bool invalid in new[] { false, true })
        foreach (bool overflow in new[] { false, true })
        {
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, values[i % 8],
                values[i % 8], values[(i + 1) % 8], values[(i + 1) % 8], 1)).ToArray();
            if (overflow) bars[0] = new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 1);
            if (invalid) bars[^1] = new Bar(default, 0, 1, 0, 0, double.NaN);
            var power = new BalanceOfPower(3);
            IIndicator[] roots = mixed ? [power, new FirstValueEma(3)] : [power];
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                IIndicatorRun? expected = null, actual = null;
                var expectedError = await Record.ExceptionAsync(async () =>
                    expected = await Build(bars, roots).ConfigureHistory(history).ConfigureBehavior(_ => { }).BuildAsync());
                var builder = Build(bars, roots).ConfigureHistory(history);
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
                        foreach (var root in roots) Compare(expected!, actual!, root);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ExplicitComponentsCancellationGpuAndSnapshotsKeepTheirContracts()
    {
        var bars = Enumerable.Range(0, 41).Select(i => new Bar(default, i, i + 3, i - 1, i + 1, 1)).ToArray();
        foreach (IMovingAverage average in new IMovingAverage[] { new Sma(), new Ema(), new Wma() })
        {
            var indicator = new BalanceOfPower(3, average);
            Assert.False(ValuesBarExecution.SupportsOwned([indicator]));
            using var expected = await Build(bars, [indicator]).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Build(bars, [indicator]).BuildAsync();
            Compare(expected, actual, indicator);
        }
        var power = new BalanceOfPower(3);
        var builder = Build(bars, [power]);
        using var saved = await builder.BuildAsync();
        var snapshot = saved.Latest;
        var values = saved[power].ToArray();
        Array.Clear(bars);
        Assert.Equal(41, snapshot.Bar.Close);
        Assert.Equal(values, saved[power].ToArray());
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.Null(builder.LastExecution);
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator[] roots) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots);
    private static void Compare(IIndicatorRun expected, IIndicatorRun actual, IIndicator indicator)
    {
        foreach (var output in indicator.Outputs)
            Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
}
