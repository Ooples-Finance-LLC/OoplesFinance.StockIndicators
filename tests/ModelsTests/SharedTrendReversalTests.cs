using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedTrendReversalTests : IDisposable
{
    private readonly int _priorDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedTrendReversalTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Fact]
    public async Task EachPatternPreservesExactWindowsAndResetAcrossWideAndSubnormalBars()
    {
        double[] points = [-double.MaxValue, -10, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, 10, double.MaxValue];
        var bars = (from open in points from close in points select new Bar(default, open, double.MaxValue, -double.MaxValue, close, 1))
            .Concat(points.Select(v => new Bar(default, 0, double.Epsilon, 0, v, 1)))
            .Concat(Fixture(257)).ToArray();
        foreach (int period in new[] { 1, 3, 10 })
        foreach (decimal fraction in new[] { 0m, .1m, 1m })
        foreach (var kind in Enumerable.Range(0, 6))
        {
            var indicator = Create(kind, period, fraction);
            var state = (IIndicatorState)indicator.CreateState()!;
            var first = bars.Select(b => state.Update(in b)).ToArray();
            state.Reset();
            Assert.Equal(first, bars.Select(b => state.Update(in b)).ToArray());
            foreach (var rule in ((IIndicatorValidationContract)indicator).ValidationRules)
                rule.Check(new IndicatorValidationContext("trend-reversal-window", bars, [first], 0));
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
    [InlineData(4)] [InlineData(5)]
    public async Task ParallelWindowsPreservePatternStartupBitsAndOwnership(int kind)
    {
        foreach (int count in new[] { 8191, 8192, 8193 })
        foreach (decimal fraction in new[] { 0m, .1m, 1m })
            await Compare(Fixture(count), [Create(kind, 10, fraction)]);
    }

    [Fact]
    public async Task MixedWindowsAndExtremePeriodsPreserveExactEvictionAndSerialFallback()
    {
        IIndicator[] indicators = Enumerable.Range(0, 6)
            .Select((kind, i) => (IIndicator)Create(kind, 1 + 3 * i)).Append(new RollingPriceSum(7)).ToArray();
        await Compare(Fixture(8193), indicators);
        foreach (int period in new[] { 1, 4094, 4095, int.MaxValue - 2, int.MaxValue - 1, int.MaxValue })
        {
            await Compare(Fixture(8193), [new BullishAbandonedBabyPattern(trendPeriod: period),
                new TrendStarPattern(trendPeriod: period)]);
            await Compare(Fixture(41), [new BearishAbandonedBabyPattern(longPeriod: period),
                new TrendStarPattern(period: period)]);
        }
        await Compare([], indicators);
        await Compare(Fixture(1), indicators);
    }

    [Fact]
    public async Task WorkerOverlapPreservesInputPrecedenceCancellationAndSourcedFallback()
    {
        var bars = Fixture(8193);
        IIndicator[] indicators = [new BullishAbandonedBabyPattern(10), new TrendStarPattern(trendPeriod: 17), new RollingPriceSum(2)];
        // A raw error in a captured worker tail must precede an earlier sum overflow.
        bars[0] = bars[1] = new Bar(default, 1, 2, 0, double.MaxValue, 1);
        foreach (int field in Enumerable.Range(0, 6))
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
        await Compare(Fixture(41), [new BullishAbandonedBabyPattern(3).Of(new Sma(2))]);
    }

    private static IndicatorBase Create(int kind, int period, decimal fraction = .1m) => kind switch
    {
        0 => new BullishAbandonedBabyPattern(period, period, .75m, fraction),
        1 => new BearishAbandonedBabyPattern(period, period, .25m, fraction),
        _ => new TrendStarPattern((TrendStarKind)(kind - 2), period, period, .25m, .75m, fraction, fraction)
    };
    private static Bar[] Fixture(int count)
    {
        var patterns = new List<Bar>();
        foreach (bool mirror in new[] { false, true })
        {
            for (int i = 30; i >= 0; i--)
                Add(new Bar(default, 62 + 2 * i, 63 + 2 * i, 60 + 2 * i, 61 + 2 * i, 1), mirror);
            Add(new Bar(default, 60, 61, 49, 50, 1), mirror);
            Add(new Bar(default, 45, 46, 44, 45, 1), mirror);
            Add(new Bar(default, 50, 62, 49, 60, 1), mirror);
            for (int i = 30; i >= 0; i--)
                Add(new Bar(default, 6.75 + .5 * i, 7 + .5 * i, 6.25 + .5 * i, 6.5 + .5 * i, 1), mirror);
            Add(new Bar(default, 6, 6.5, 3.75, 4, 1), mirror);
            Add(new Bar(default, 3, 3.25, 2.75, 3, 1), mirror);
            Add(new Bar(default, 3.5, 5.25, 3.375, 5, 1), mirror);
        }
        return Enumerable.Range(0, count).Select(i => patterns[i % patterns.Count]).ToArray();
        void Add(Bar bar, bool mirror) => patterns.Add(mirror
            ? new Bar(default, -bar.Open, -bar.Low, -bar.High, -bar.Close, 1) : bar);
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
