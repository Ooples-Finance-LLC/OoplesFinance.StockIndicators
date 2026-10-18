using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedLaggedChangeTests : IDisposable
{
    private readonly int _priorDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedLaggedChangeTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Theory]
    [InlineData(PriceChangeKind.Difference)] [InlineData(PriceChangeKind.Fraction)]
    [InlineData(PriceChangeKind.Percent)] [InlineData(PriceChangeKind.Ratio)]
    [InlineData(PriceChangeKind.RatioPercent)] [InlineData(PriceChangeKind.Gain)] [InlineData(PriceChangeKind.Loss)]
    public async Task LaggedKernelsPreserveStartupWorkersAndLargePeriods(PriceChangeKind kind)
    {
        foreach (int count in new[] { 0, 1, 65, 8193 })
        foreach (int period in new[] { 1, 20, 4095, 4096, int.MaxValue })
            await Compare(InputBars(count), [new LaggedPriceChange(period, kind)]);
    }

    [Fact]
    public async Task MixedLaggedKernelsPreserveSnapshotsCancellationAndOrderedFailures()
    {
        var bars = InputBars(10001);
        IIndicator[] indicators = [new LaggedPriceChange(3, PriceChangeKind.Percent),
            new LaggedPriceChange(20, PriceChangeKind.Loss), new Wma(13), new HighestHigh(7)];
        await Compare(bars, indicators);
        using var saved = await Builder(bars, indicators, IndicatorHistoryMode.Full).BuildAsync();
        var original = bars.ToArray(); Array.Clear(bars);
        int index = 0;
        await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Builder(bars, indicators, IndicatorHistoryMode.Full).BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => Builder(bars, [indicators[0]], IndicatorHistoryMode.Full)
            .ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        bars = InputBars(10001);
        bars[0] = new Bar(default, 0, 1, 0, double.MaxValue, 1);
        bars[1] = new Bar(default, 0, 1, 0, -double.MaxValue, 1);
        await Compare(bars, [new LaggedPriceChange(1), new LaggedPriceChange(2, PriceChangeKind.Percent)]);
        bars[^1] = new Bar(default, 0, 1, 0, 1, double.NaN);
        await Compare(bars, [new LaggedPriceChange(1), new Wma(3)]);
    }

    [Fact]
    public void ArithmeticMatchesTheOriginalExactAccumulatorForAllKinds()
    {
        var random = new Random(69173); var bytes = new byte[8];
        double Finite() { random.NextBytes(bytes); return BitConverter.Int64BitsToDouble(BitConverter.ToInt64(bytes) & ~0x0010000000000000L); }
        double[] special = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1, -1, Math.BitIncrement(1)];
        foreach (var kind in Enum.GetValues<PriceChangeKind>())
        {
            foreach (double current in special) foreach (double previous in special) Check(current, previous, kind);
            for (int i = 0; i < 2048; i++) Check(Finite(), Finite(), kind);
        }
    }

    private static void Check(double current, double previous, PriceChangeKind kind)
    {
        var sum = new ExactMeanAccumulator(); double expected;
        if (kind is PriceChangeKind.Gain or PriceChangeKind.Loss)
        {
            bool gain = kind == PriceChangeKind.Gain;
            if (gain ? current <= previous : current >= previous) expected = 0;
            else { sum.Add(current, gain ? 1 : -1); sum.Add(previous, gain ? -1 : 1); expected = sum.Mean(1); }
        }
        else if (kind != PriceChangeKind.Difference && previous == 0) expected = 0;
        else
        {
            int scale = kind is PriceChangeKind.Percent or PriceChangeKind.RatioPercent ? 100 : 1;
            sum.Add(current, scale);
            if (kind is PriceChangeKind.Difference or PriceChangeKind.Fraction or PriceChangeKind.Percent) sum.Add(previous, -scale);
            var denominator = new ExactMeanAccumulator(); denominator.Add(previous);
            expected = kind == PriceChangeKind.Difference ? sum.Mean(1) : sum.Ratio(denominator);
        }
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(LaggedPriceChange.Calculate(current, previous, kind)));
    }
    private static Bar[] InputBars(int count) => Enumerable.Range(0, count).Select(i => new Bar(default, 0, 2, -2, (i % 37 - 18) / 8d, 1)).ToArray();
    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator[] indicators, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
    private static async Task Compare(Bar[] bars, IIndicator[] indicators)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () => expected = await Builder(bars, indicators, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await Builder(bars, indicators, history).BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError); Assert.Equal(expectedError.GetType(), actualError.GetType()); Assert.Equal(expectedError.Message, actualError.Message);
                }
                else
                {
                    Assert.Null(actualError);
                    foreach (var output in indicators.SelectMany(i => i.Outputs)) Assert.Equal(
                        expected![output].ToArray().Select(BitConverter.DoubleToInt64Bits), actual![output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
    }
}
