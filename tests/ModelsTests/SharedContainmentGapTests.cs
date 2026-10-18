using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedContainmentGapTests : IDisposable
{
    private readonly int _priorDop = AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism;
    public SharedContainmentGapTests() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = 8;
    public void Dispose() => AiDotNet.Tensors.Helpers.CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Fact]
    public async Task EachPatternPreservesExactWindowsAndResetAcrossWideAndSubnormalBars()
    {
        double[] points = [-double.MaxValue, -10, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, 10, double.MaxValue];
        var bars = (from open in points from close in points select new Bar(default, open, double.MaxValue, -double.MaxValue, close, 1))
            .Concat(points.Select(v => new Bar(default, 0, double.Epsilon, 0, v, 1)))
            .Concat(Enumerable.Range(0, 40).Select(i => new Bar(default, 4, 10, 0, i % 3 == 0 ? 4 : 6, 1))).ToArray();
        foreach (int period in new[] { 1, 3, 10 })
        foreach (bool shadows in new[] { false, true })
        foreach (var kind in Enumerable.Range(0, 5))
        {
            var indicator = Create(kind, period, shadows);
            var state = (IIndicatorState)indicator.CreateState()!;
            var first = bars.Select(b => state.Update(in b)).ToArray();
            state.Reset();
            Assert.Equal(first, bars.Select(b => state.Update(in b)).ToArray());
            foreach (var rule in ((IIndicatorValidationContract)indicator).ValidationRules)
                rule.Check(new IndicatorValidationContext("containment-gap-window", bars, [first], 0));
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
    [InlineData(4)]
    public async Task ParallelWindowsPreservePatternStartupBitsAndOwnership(int kind)
    {
        foreach (int count in new[] { 8191, 8192, 8193 })
        foreach (bool shadows in new[] { false, true })
            await Compare(Fixture(count), [Create(kind, 10, shadows)]);
    }

    [Fact]
    public async Task MixedWindowsAndExtremePeriodsPreserveExactEvictionAndSerialFallback()
    {
        IIndicator[] indicators = Enumerable.Range(0, 5)
            .Select((kind, i) => (IIndicator)Create(kind, 1 + 3 * i)).Append(new RollingPriceSum(7)).ToArray();
        await Compare(Fixture(8193), indicators);
        foreach (int period in new[] { 1, 4094, 4095, int.MaxValue - 2, int.MaxValue - 1, int.MaxValue })
            await Compare(Fixture(8193), [new BullishHaramiPattern(period), new DownsideTasukiGapPattern(period)]);
        await Compare([], indicators);
        await Compare(Fixture(1), indicators);
    }

    [Fact]
    public async Task WorkerOverlapPreservesInputPrecedenceCancellationAndSourcedFallback()
    {
        var bars = Fixture(8193);
        IIndicator[] indicators = [new BullishHaramiPattern(10), new DownsideTasukiGapPattern(17), new RollingPriceSum(2)];
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
        await Compare(Fixture(41), [new BullishHaramiPattern(3).Of(new Sma(2))]);
    }

    private static IndicatorBase Create(int kind, int period, bool shadows = false) => kind switch
    {
        0 => new StrictHaramiCandle(shadows),
        1 => new BullishHaramiPattern(period, shadows),
        2 => new BearishHaramiPattern(period, shadows),
        3 => new UpsideTasukiGapPattern(period),
        4 => new DownsideTasukiGapPattern(period),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
    private static Bar[] Fixture(int count)
    {
        var patterns = new List<Bar>();
        foreach (bool mirror in new[] { false, true })
        {
            for (int i = 0; i <= 20; i++)
                Add(new Bar(default, 60 - 2 * i, 80 + 2 * i, -i, 54 - 2 * i, 1), mirror);
            Add(new Bar(default, 15, 30, -4, 19, 1), mirror);
            for (int i = 20; i >= 0; i--)
                Add(new Bar(default, 10 - 2 * i, 14 - 2 * i, 9 - 2 * i, 13 - 2 * i, 1), mirror);
            Add(new Bar(default, 17, 22, 16, 21, 1), mirror);
            Add(new Bar(default, 19, 20, 14, 15, 1), mirror);
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
