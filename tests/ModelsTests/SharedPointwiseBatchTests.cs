using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedPointwiseBatchTests : IDisposable
{
    private readonly int _previousDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedPointwiseBatchTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;

    [Fact]
    public async Task EveryPointwiseKernelPreservesBitsPresenceAndSnapshotsInOneBatch()
    {
        IIndicator[] indicators = Enum.GetValues<PriceCircularOperation>().Select(v => (IIndicator)new PriceCircularTransform(v))
            .Concat(Enum.GetValues<PriceTranscendentalOperation>().Select(v => (IIndicator)new PriceTranscendentalTransform(v)))
            .Concat(Enum.GetValues<PriceRoundingOperation>().Select(v => (IIndicator)new PriceRoundingTransform(v)))
            .Concat(Enum.GetValues<CandleArithmeticOperation>().Select(v => (IIndicator)new CandleArithmetic(v)))
            .Concat(new IIndicator[] { new MedianPrice(1), new TypicalPrice(1), new WeightedClose(1), new FullTypicalPrice(1),
                new DojiCandle(), new BullishCandle(), new BearishCandle(), new DragonflyDojiCandle(), new GravestoneDojiCandle() }).ToArray();
        Assert.True(ValuesBarExecution.SupportsOwned(indicators));
        double[] values = [-0d, 0d, -double.Epsilon, double.Epsilon, -1, 1, Math.BitIncrement(1), Math.BitDecrement(-1), -.25, .75];
        foreach (int count in new[] { 0, 1, 63, 64, 65, 8191, 8192, 8193 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, i % 3 == 0 ? -0d : i % 3 == 1 ? 2 : -2,
                2, -2, values[i % values.Length], 1)).ToArray();
            await Compare(bars, indicators);
        }
    }

    [Fact]
    public async Task BatchPreservesIndicatorThenIndexFailureOrderingAndInputPrecedence()
    {
        var exp = new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential);
        var multiply = new CandleArithmetic(CandleArithmeticOperation.Multiply, CandlePriceField.Open, CandlePriceField.Close);
        foreach (var indicators in new IIndicator[][] { [exp, multiply], [multiply, exp] })
        {
            var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 8193).ToArray();
            bars[1] = new Bar(default, double.MaxValue, 2, 0, 2, 1);
            bars[4097] = new Bar(default, 1, 2, 0, 1000, 1);
            await Compare(bars, indicators, expectError: true);
            foreach (int field in Enumerable.Range(0, 5))
            {
                bars[^1] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                    field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
                await Compare(bars, indicators, expectError: true);
            }
            bars[1] = bars[4097] = bars[^1] = bars[0];
            await Compare(bars, indicators);
        }
    }

    [Fact]
    public async Task BatchPreservesDuplicatesCancellationAndSourceFallback()
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 2, 0, .5, 1), 1025).ToArray();
        var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
        var cosine = new PriceCircularTransform(PriceCircularOperation.Cosine);
        await Compare(bars, [asin, asin]);
        await Compare(bars, [asin, cosine, asin]);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var builder = Build(bars, [asin, cosine], IndicatorHistoryMode.Full);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        Assert.Null(builder.LastExecution);
        IIndicator[] sourced = [asin.Of(new Sma(3)), cosine];
        Assert.False(ValuesBarExecution.SupportsOwned(sourced));
        await Compare(bars, sourced, shared: false);
    }

    private static async Task Compare(Bar[] bars, IIndicator[] indicators, bool shared = true, bool expectError = false)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () =>
                expected = await Build(bars, indicators, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await Build(bars, indicators, history).BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.True(expectError, expectedError.ToString());
                    Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    continue;
                }
                Assert.False(expectError);
                Assert.Null(actualError);
                if (shared && history == IndicatorHistoryMode.Full) Assert.False(((IndicatorRun)actual!).HasLegacyRuntime);
                foreach (var indicator in indicators)
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected![output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual![output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                var original = bars.ToArray();
                Array.Clear(bars);
                if (history == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in actual!) Assert.Equal(original[index++], snapshot.Bar);
                    Assert.Equal(original.Length, index);
                }
                else if (original.Length > 0) Assert.Equal(original[^1], actual!.Latest.Bar);
                original.CopyTo(bars, 0);
            }
        }
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator[] indicators, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
}
