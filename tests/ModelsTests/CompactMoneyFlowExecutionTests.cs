using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CompactMoneyFlowExecutionTests
{
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);

    [Theory]
    [InlineData(2, false, 0)] [InlineData(3, true, 0)] [InlineData(3, false, 4)]
    [InlineData(14, true, 2)] [InlineData(2, true, int.MaxValue)] [InlineData(int.MaxValue, false, 0)]
    public void ExactFlowsPreserveSignedVolumeWideProductsThresholdsAndReset(int period, bool minimum, int suppressed)
    {
        double[] prices = [1, 2, 1, 2, 1, 1, 3, 0, -1, -2, double.MaxValue, -double.MaxValue,
            double.Epsilon, -double.Epsilon, 1, Math.BitIncrement(1d), 1];
        double[] volumes = [1, -1, 0, double.MaxValue, double.Epsilon];
        var bars = prices.Concat(Enumerable.Repeat(1d, 20)).Select((v, i) =>
            new Bar(default, v, v, v, v, volumes[i % volumes.Length])).ToArray();
        var expected = WindowMoneyFlowReference.Calculate(bars, period, minimum, suppressed);
        var state = (IMultiOutputState)new WindowMoneyFlowIndex(period, minimum, suppressed).CreateState();
        Span<double> output = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
            }
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CompactProductsPreserveFullMantissasAndExtremeExponentEvictions(bool minimum)
    {
        var random = new Random(927);
        var bars = Enumerable.Range(0, 600).Select(i =>
        {
            double price = Math.ScaleB(1 + random.NextDouble(), (i % 6) switch
            {
                0 => 1000, 1 => -1070, 2 => 500, 3 => -500, _ => 0
            });
            double volume = Math.ScaleB(1 + random.NextDouble(), (i % 5) switch
            {
                0 => 1000, 1 => -1070, 2 => -500, _ => 0
            });
            if (i % 7 == 0) volume = -volume;
            if (i % 11 == 0) volume = 0;
            return new Bar(default, price, price, price, price, volume);
        }).ToArray();
        var expected = WindowMoneyFlowReference.Calculate(bars, 7, minimum, 0);
        var state = (IMultiOutputState)new WindowMoneyFlowIndex(7, minimum).CreateState();
        Span<double> output = stackalloc double[2];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                Assert.Equal(expected[i].HasValue ? 1d : 0d, output[1]);
            }
        }
    }

    [Fact]
    public void ExactlyZeroPositiveFlowRetainsPositiveZeroWithNegativeTotal()
    {
        var state = (IMultiOutputState)new WindowMoneyFlowIndex(2).CreateState();
        Span<double> output = stackalloc double[2];
        foreach (var bar in new[] { new Bar(default, 2, 2, 2, 2, 1),
            new Bar(default, 1, 1, 1, 1, -1), new Bar(default, 0, 0, 0, 0, 1) })
            state.Update(in bar, output);
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[0]));
        Assert.Equal(1d, output[1]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WarmedOrdinaryFlowWindowsAllocateNoPerBarObjects(bool minimum)
    {
        var state = (IMultiOutputState)new WindowMoneyFlowIndex(14, minimum).CreateState();
        Span<double> output = stackalloc double[2];
        for (int i = 0; i < 100; i++)
        {
            double value = 10 + (i * 13 % 101) / 8d;
            var bar = new Bar(default, value, value + 1, value - 1, value, i % 13 + 1);
            state.Update(in bar, output);
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        state.Reset();
        for (int i = 0; i < 1000; i++)
        {
            double value = 10 + (i * 13 % 101) / 8d;
            var bar = new Bar(default, value, value + 1, value - 1, value, i % 13 + 1);
            state.Update(in bar, output);
        }
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(output[0]));
        Assert.Equal(1d, output[1]);
    }

    [Theory]
    [InlineData(511, 14, 0)] [InlineData(512, 14, 0)] [InlineData(513, 14, 0)]
    [InlineData(4097, 14, 250)] [InlineData(4097, 14, 1024)] [InlineData(4097, 14, 4090)]
    [InlineData(513, 2, int.MaxValue)] [InlineData(17, int.MaxValue, 0)]
    public async Task WorkersPreserveGlobalSuppressionAndOwnedOutputs(int count, int period, int suppressed)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i =>
            {
                double value = 10 + (i * 13 % 101) / 8d;
                return new Bar(default, value, value + 1, value - 1, value, i % 13 + 1);
            }).ToArray();
            bars[count / 2] = new Bar(default, 0, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue);
            bars[count / 2 + 1] = new Bar(default, 0, double.Epsilon, double.Epsilon, double.Epsilon, double.Epsilon);
            foreach (bool minimum in new[] { false, true })
            {
                var indicator = new WindowMoneyFlowIndex(period, minimum, suppressed);
                using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
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
                        Assert.Equal(count, row);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)] [InlineData(false, 250)] [InlineData(true, 512)]
    [InlineData(true, int.MaxValue)]
    public async Task CertifiedProductsPreserveRoundedTypicalPricesSignedVolumesAndSuppression(bool minimum, int suppressed)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int period in new[] { 2, 14, 128 })
            foreach (var (scale, volumeScale) in new[] { (-1074, 0), (-1023, 0), (-900, 0), (0, 0), (900, 0),
                (0, -1074), (0, -1023), (0, 900) })
            foreach (int sign in new[] { -1, 1 })
            {
                var bars = Enumerable.Range(0, 513).Select(i =>
                {
                    double value = 10 + (i * 13 % 101) / 8d;
                    return new Bar(default, 123, Math.ScaleB(value + 1 + i % 5 / 16d, scale),
                        Math.ScaleB(value - 1 - i % 7 / 16d, scale), Math.ScaleB(value, scale), Math.ScaleB(sign * (i % 13 + 1), volumeScale));
                }).ToArray();
                var indicator = new WindowMoneyFlowIndex(period, minimum, suppressed);
                using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
                foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
                {
                    using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                    foreach (var output in indicator.Outputs)
                        Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                            actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task CertifiedFullMantissaProductsRoundOnce()
    {
        var random = new Random(718);
        var bars = Enumerable.Range(0, 513).Select(_ =>
        {
            double price = 1 + random.NextDouble();
            return new Bar(default, 123, price, price, price, 1 + random.NextDouble());
        }).ToArray();
        foreach (bool minimum in new[] { false, true })
        {
            var indicator = new WindowMoneyFlowIndex(7, minimum);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var actual = await Build(bars, indicator).ConfigureHistory(history).BuildAsync();
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
    }

    [Fact]
    public async Task CertifiedTotalThresholdAndSignedZeroMatchExactState()
    {
        foreach (double volume in new[] { Math.BitDecrement(1d), 1d, Math.BitIncrement(1d), -1d })
        foreach (bool minimum in new[] { false, true })
        {
            var bars = new[] { new Bar(default, 7, 0, 0, 0, volume),
                new Bar(default, 7, 1, 1, 1, volume), new Bar(default, 7, 0, 0, 0, volume) };
            var indicator = new WindowMoneyFlowIndex(2, minimum);
            using var expected = await Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync();
            using var actual = await Build(bars, indicator).BuildAsync();
            foreach (var output in indicator.Outputs)
                Assert.Equal(expected[output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                    actual[output].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
        var zeroBars = new[] { new Bar(default, 2, 2, 2, 2, 1),
            new Bar(default, 1, 1, 1, 1, -1), new Bar(default, 0, 0, 0, 0, 1) };
        var zeroIndicator = new WindowMoneyFlowIndex(2);
        using var zeroRun = await Build(zeroBars, zeroIndicator).BuildAsync();
        Assert.Equal(0L, BitConverter.DoubleToInt64Bits(zeroRun[zeroIndicator.Value][2]));
    }

    [Fact]
    public async Task NonfiniteOutputsDoNotMaskLaterInvalidInputOrCancellation()
    {
        var bars = Enumerable.Repeat(new Bar(default, 1, 1, 1, 1, 1), 4097).ToArray();
        bars[1] = new Bar(default, 2, 2, 2, 2, 1);
        bars[2] = new Bar(default, 1, 1, 1, 1, -2);
        var indicator = new WindowMoneyFlowIndex(2);
        foreach (bool invalidInput in new[] { false, true })
        {
            if (invalidInput) bars[^1] = new Bar(default, 1, 1, 1, 1, double.NaN);
            var expected = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureBehavior(_ => { }).BuildAsync());
            Assert.NotNull(expected);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var actual = await Record.ExceptionAsync(() => Build(bars, indicator).ConfigureHistory(history).BuildAsync());
                Assert.NotNull(actual);
                Assert.Equal(expected.GetType(), actual.GetType());
                Assert.Equal(expected.Message, actual.Message);
            }
        }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Build(bars, indicator).BuildAsync(cancellation.Token));
    }
}
