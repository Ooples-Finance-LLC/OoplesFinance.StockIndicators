using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class FixedGridCmfExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, MoneyFlowWithDetails indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(1, false)] [InlineData(20, false)] [InlineData(20, true)]
    public async Task IntegerWindowsAndWideFallbackPreserveEveryPublicationAndSnapshot(int period, bool wide)
    {
        var bars = Enumerable.Range(0, 8193).Select(i =>
        {
            double low = 80 + (i % 101) / 1024d;
            double high = 82 + (i % 79) / 1024d;
            double close = 81 + (i % 71) / 1024d;
            if (i % 31 == 0) (high, low) = (low, high);
            if (i % 97 == 0) high = low;
            double volume = i % 23 == 0 ? 0 : (i % 7 - 3) * 1001d;
            if (wide) volume = Math.ScaleB(i % 2 == 0 ? 1 : -1, i % 3 == 0 ? -500 : 500);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), 123, high, low, close, volume);
        }).ToArray();
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var indicator = new MoneyFlowWithDetails(period);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (int row in new[] { period - 1, 1023, 1024, 4095, 4096, bars.Length - 1 })
                CheckIndependentWindow(bars, row, period, expected[indicator.Cmf].ToArray()[row],
                    expected[indicator.CmfIsDefined].ToArray()[row]);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var actual = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input); actual.Dispose();
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

    [Fact]
    public async Task ExactDispatchThresholdAndLargestEnabledPeriodPreserveSerialAndWorkerResults()
    {
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(default, 0, 3, 0, i % 3, i % 7 - 3)).ToArray();
        var indicator = new MoneyFlowWithDetails(4096);
        using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
        CheckIndependentWindow(bars, bars.Length - 1, 4096, expected[indicator.Cmf].ToArray()[^1],
            expected[indicator.CmfIsDefined].ToArray()[^1]);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(114)] [InlineData(115)]
    public async Task SignedWordBoundRejectsTheFirstOverflowingWindow(int volumePower)
    {
        // Row zero forces grid zero. With P4096 the coefficient widths are
        // 115/116 bits, straddling the 127-bit signed-sum certificate. After
        // row zero expires, the second case has total volume exactly 2^127;
        // accidentally admitting it would wrap the denominator, not the flow.
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(default, 0, 1, 0, i % 2 == 0 ? 1 : .5,
                i == 0 ? 1 : Math.ScaleB(1, volumePower))).ToArray();
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            // A single region retains row zero's fine grid for the whole call.
            CpuParallelSettings.MaxDegreeOfParallelism = 1;
            var indicator = new MoneyFlowWithDetails(4096);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(BitConverter.DoubleToInt64Bits(.5),
                    BitConverter.DoubleToInt64Bits(actual[indicator.Cmf].ToArray()[^1]));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task SubnormalFlowAndNegativeZeroCmfRetainTheirDifferentZeroContracts()
    {
        var bars = Enumerable.Range(0, 8193).Select(i =>
            new Bar(default, 0, 1, -1, i % 20 == 0 ? -double.Epsilon : 0, 1)).ToArray();
        var indicator = new MoneyFlowWithDetails(20);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
            var flow = actual[indicator.Flow].ToArray();
            var cmf = actual[indicator.Cmf].ToArray();
            var defined = actual[indicator.CmfIsDefined].ToArray();
            Assert.Equal(BitConverter.DoubleToInt64Bits(-double.Epsilon), BitConverter.DoubleToInt64Bits(flow[0]));
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(flow[1]));
            for (int i = 19; i < bars.Length; i++)
            {
                Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(cmf[i]));
                Assert.Equal(1d, defined[i]);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OverflowRemainsBehindAllInputValidationAndCancellation(bool invalidInput)
    {
        var bars = Enumerable.Repeat(new Bar(default, 0, 1, 0, .5, 1), 8193).ToArray();
        // Finite multiplier three, but the independently rounded flow overflows.
        bars[3] = new Bar(default, 0, 1, 0, 2, double.MaxValue);
        if (invalidInput) bars[^1] = new Bar(default, 0, 1, 0, .5, double.NaN);
        var indicator = new MoneyFlowWithDetails(20);
        var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
        Assert.NotNull(expected);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
            Assert.NotNull(actual);
            Assert.Equal(expected.GetType(), actual.GetType());
            Assert.Equal(expected.Message, actual.Message);
            if (!invalidInput)
            {
                var failure = Assert.IsType<IndicatorOutputException>(actual);
                Assert.Equal(1, failure.OutputSlot);
                Assert.Equal(3, failure.BarIndex);
            }
            using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                Build(bars, indicator).ConfigureHistory(history).BuildAsync(cancellation.Token));
        }
    }

    private static void CheckIndependentWindow(Bar[] bars, int row, int period, double actual, double defined)
    {
        ReferenceFraction flows = new(0), volume = new(0);
        for (int i = row - period + 1; i <= row; i++)
        {
            var bar = bars[i];
            var range = ReferenceFraction.FromDouble(bar.High) - ReferenceFraction.FromDouble(bar.Low);
            var balance = ReferenceFraction.FromDouble(bar.Close) * new ReferenceFraction(2)
                - ReferenceFraction.FromDouble(bar.High) - ReferenceFraction.FromDouble(bar.Low);
            // The window sums published, once-rounded flows, not the original
            // rational products and not rounded multiplier times volume.
            double flow = range.Sign == 0 ? 0 :
                (balance * ReferenceFraction.FromDouble(bar.Volume) / range).ToDouble();
            flows += ReferenceFraction.FromDouble(flow);
            volume += ReferenceFraction.FromDouble(bar.Volume);
        }
        Assert.Equal(volume.Sign == 0 ? 0d : 1d, defined);
        double expected = volume.Sign == 0 ? 0 : (flows / volume).ToDouble();
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
    }
}
