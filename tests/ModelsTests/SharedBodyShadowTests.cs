using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedBodyShadowTests : IDisposable
{
    private readonly int _priorDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedBodyShadowTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Fact]
    public async Task EachPatternPreservesExactWindowsAndResetAcrossWideAndSubnormalBars()
    {
        double[] points = [-double.MaxValue, -10, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, 10, double.MaxValue];
        var bars = (from open in points from close in points select new Bar(default, open, double.MaxValue, -double.MaxValue, close, 1))
            .Concat(points.Select(v => new Bar(default, 0, double.Epsilon, 0, v, 1)))
            .Concat(Enumerable.Range(0, 40).Select(i => new Bar(default, 4, 10, 0, i % 3 == 0 ? 4 : 6, 1))).ToArray();
        foreach (int period in new[] { 1, 3, 10 })
        foreach (var kind in Enum.GetValues<BodyShadowCandleKind>())
        {
            var indicator = Create(kind, period);
            var state = new BodyShadowCandleState(period, kind);
            var first = bars.Select(b => state.Update(in b)).ToArray();
            state.Reset();
            Assert.Equal(first, bars.Select(b => state.Update(in b)).ToArray());
            foreach (var rule in ((IIndicatorValidationContract)indicator).ValidationRules)
                rule.Check(new IndicatorValidationContext("selective-body-shadow-sums", bars, [first], 0));
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
                Assert.Equal(first, run[indicator].ToArray());
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public async Task ParallelWindowsPreservePatternStartupBitsAndOwnership(int kind)
    {
        foreach (int count in new[] { 8191, 8192, 8193 })
            await Compare(Fixture(count), [Create((BodyShadowCandleKind)kind, 10)]);
    }

    [Fact]
    public async Task MixedWindowsAndExtremePeriodsPreserveExactEvictionAndSerialFallback()
    {
        IIndicator[] indicators = Enum.GetValues<BodyShadowCandleKind>()
            .Select((kind, i) => (IIndicator)Create(kind, 1 + 3 * i)).Append(new RollingPriceSum(7)).ToArray();
        await Compare(Fixture(8193), indicators);
        foreach (int period in new[] { 1, 4095, 4096, int.MaxValue })
            await Compare(Fixture(8193), [new LongLineCandle(period)]);
        await Compare([], indicators);
        await Compare(Fixture(1), indicators);
    }

    [Fact]
    public async Task WorkerOverlapPreservesInputPrecedenceCancellationAndSourcedFallback()
    {
        var bars = Fixture(8193);
        IIndicator[] indicators = [new HighWaveCandle(10), new ShortLineCandle(17), new RollingPriceSum(2)];
        // A raw error in a captured worker tail must precede an earlier sum overflow.
        bars[0] = bars[1] = new Bar(default, 1, 2, 0, double.MaxValue, 1);
        foreach (int field in Enumerable.Range(0, 5))
        {
            bars[1019] = new Bar(default, field == 0 ? double.NaN : 0, field == 1 ? double.NaN : 1,
                field == 2 ? double.NaN : 0, field == 3 ? double.NaN : 1, field == 4 ? double.NaN : 1);
            await Compare(bars, indicators, expectError: true);
        }
        bars[1019] = new Bar(default, 1, 2, 0, 1, 1);
        await Compare(bars, indicators, expectError: true);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var builder = Build(Fixture(8193), indicators, IndicatorHistoryMode.Full);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        Assert.Null(builder.LastExecution);
        await Compare(Fixture(41), [new MarubozuCandle(3).Of(new Sma(2))]);
    }

    private static IndicatorBase Create(BodyShadowCandleKind kind, int period) => kind switch
    {
        BodyShadowCandleKind.Marubozu => new MarubozuCandle(period),
        BodyShadowCandleKind.ClosingMarubozu => new ClosingMarubozuCandle(period),
        BodyShadowCandleKind.BeltHold => new BeltHoldCandle(period),
        BodyShadowCandleKind.SpinningTop => new SpinningTopCandle(period),
        BodyShadowCandleKind.HighWave => new HighWaveCandle(period),
        BodyShadowCandleKind.LongLine => new LongLineCandle(period),
        _ => new ShortLineCandle(period)
    };
    private static Bar[] Fixture(int count)
    {
        Bar[] patterns = [new(default, 1, 5, .5, 4.5, 1), new(default, 4.5, 5, .5, 1, 1),
            new(default, 4, 8, 1, 5, 1), new(default, 5, 8, 1, 4, 1),
            new(default, 4, 6, 3.5, 5, 1), new(default, 1, 7, 0, 4, 1), new(default, 0, 0, 0, 0, 1)];
        return Enumerable.Range(0, count).Select(i => i % 11 < 10
            ? new Bar(default, 4, 10, 0, 6, 1) : patterns[i / 11 % patterns.Length]).ToArray();
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator[] indicators, IndicatorHistoryMode history) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).ConfigureHistory(history);
    private static async Task Compare(Bar[] bars, IIndicator[] indicators, bool expectError = false)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () =>
                expected = await Build(bars, indicators, history).ConfigureBehavior(_ => { }).BuildAsync());
            var actualError = await Record.ExceptionAsync(async () => actual = await Build(bars, indicators, history).BuildAsync());
            using (expected) using (actual)
            {
                if (expectError)
                {
                    Assert.NotNull(expectedError); Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    continue;
                }
                Assert.Null(expectedError); Assert.Null(actualError);
                foreach (var indicator in indicators)
                    Assert.Equal(expected![indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual![indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                var original = bars.ToArray(); Array.Clear(bars);
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
}
