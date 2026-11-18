using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class WiderUltimateExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(7, 14, 28, false)]
    [InlineData(7, 14, 28, true)]
    [InlineData(1, 1, 1, true)]
    [InlineData(14, 7, 14, true)]
    public async Task HighPrecisionWorkersPreserveExactOutputsAndOwnedSnapshots(int a, int b, int c, bool zero)
    {
        // HLC has a 2^-46 common grid, with scaled magnitudes above 2^53.
        // Every worker contains both this fine grid and the larger high values;
        // the old 2^38 rolling-sum certificate cannot accept these regions.
        var bars = Enumerable.Range(0, 8193).Select(i =>
        {
            double low = 80 + Math.ScaleB(i % 97, -46);
            double high = 160 + Math.ScaleB(i % 53, -45);
            double close = 100 + Math.ScaleB(i % 71, -46);
            // Valid finite inputs also allow signed pressure and inverted OHLC.
            if (i % 257 < 31) (high, low, close) = (low, high, -close);
            if (i % 1024 >= 960) high = low = close = 0;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), 123, high, low, close, i + 1);
        }).ToArray();
        await Check(bars, new WindowUltimateOscillator(a, b, c, zero));
    }

    [Theory]
    [InlineData(14411518807585586d)]
    [InlineData(14411518807585588d)]
    public async Task AdjacentRepresentablePricesStraddleTheIntegerBoundWithoutChangingResults(double maximum)
    {
        // For longest period 80, floor(2^61 / 160) is 14411518807585587.
        // Its double conversion rounds UP to the second input. A price of one
        // in every region fixes grid zero, so the two cases straddle the guard.
        var bars = Enumerable.Range(0, 8193).Select(i =>
            new Bar(default, 0, maximum, 1, i % 3 == 0 ? 1 : maximum - 2, 1)).ToArray();
        await Check(bars, new WindowUltimateOscillator(7, 14, 80, true));
    }

    [Fact]
    public void LongWindowGuardUsesTheExactIntegerBoundary()
    {
        var guard = typeof(ValuesBarExecution).GetMethod("FitsUltimateLongWindows",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(guard);
        bool Accept(double maximum, int period) => (bool)guard.Invoke(null, [maximum, period])!;
        Assert.True(Accept(14411518807585586d, 80));
        Assert.False(Accept(14411518807585588d, 80));
        Assert.True(Accept(Math.ScaleB(1, 60), 1));
        Assert.False(Accept(Math.BitIncrement(Math.ScaleB(1, 60)), 1));
        Assert.False(Accept(double.PositiveInfinity, 80));
    }

    [Fact]
    public async Task HighPrecisionInvalidInputsPreserveFirstErrorAndCancellation()
    {
        var bars = Enumerable.Range(0, 8193).Select(i =>
            new Bar(default, 1, 160, 80 + Math.ScaleB(i % 97, -46), 100, 1)).ToArray();
        bars[2049] = new Bar(default, 1, 160, 80, 100, double.NaN);
        bars[6145] = new Bar(default, double.PositiveInfinity, 160, 80, 100, 1);
        var indicator = new WindowUltimateOscillator();
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Build(bars, indicator).ConfigureHistory(history).BuildAsync(cancellation.Token));
        }
    }

    private static async Task Check(Bar[] bars, WindowUltimateOscillator indicator)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            var expectedValue = expected[indicator.Value].ToArray();
            var expectedDefined = expected[indicator.IsDefined].ToArray();
            // Independently evaluate complete windows at publication startup,
            // chunk/worker boundaries and the end, without replaying 8193 full
            // rational windows for every history-mode comparison.
            foreach (int row in new[] { indicator.LongPeriod, 1023, 1024, 4095, 4096, bars.Length - 1 })
            {
                var window = bars.AsSpan(row - indicator.LongPeriod, indicator.LongPeriod + 1).ToArray();
                double? reference = TrueRangeRatioReference.Ultimate(window,
                    [indicator.ShortPeriod, indicator.MiddlePeriod, indicator.LongPeriod], indicator.ZeroRangeContribution)[^1];
                Assert.Equal(BitConverter.DoubleToInt64Bits(reference ?? 0),
                    BitConverter.DoubleToInt64Bits(expectedValue[row]));
                Assert.Equal(reference.HasValue ? 1d : 0d, expectedDefined[row]);
            }
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var actual = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input);
                actual.Dispose();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
