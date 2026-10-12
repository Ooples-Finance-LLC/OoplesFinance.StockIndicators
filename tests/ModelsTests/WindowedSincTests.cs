using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class WindowedSincTests
{
    [Theory]
    [InlineData(14)] [InlineData(20)]
    public void OrdinarySincStateReusesCompactExactStorageAfterWarmupAndReset(int period)
    {
        var state = (IIndicatorState)new WindowedSinc(period).CreateState();
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 1, 0,
            (i * 17 % 97 - 48) / 8d, 1)).ToArray();
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length]);
        state.Reset();
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) state.Update(in bars[i % bars.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Theory]
    [InlineData(SincWindow.Rectangular)]
    [InlineData(SincWindow.Hann)]
    [InlineData(SincWindow.Hamming)]
    [InlineData(SincWindow.Blackman)]
    [InlineData(SincWindow.BlackmanHarris)]
    public void CoefficientsStartupOrientationAndResetMatchSnapshot(SincWindow window)
    {
        double[] values = [-0d, 0d, double.Epsilon, -double.Epsilon, double.MaxValue / 16, -double.MaxValue / 16, 1, -.125];
        var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, 0, values[i % values.Length], 1)).ToArray();
        foreach (int taps in new[] { window == SincWindow.Rectangular ? 1 : window == SincWindow.Hann ? 3 : 2, 7, 20, 21, 129 })
        {
            var state = (IIndicatorState)new WindowedSinc(14, taps, window).CreateState();
            var expected = WindowedSincSnapshot.Calculate(bars, 14, taps, window);
            for (int pass = 0; pass < 2; pass++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]), BitConverter.DoubleToInt64Bits(state.Update(bars[i])));
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(511)]
    [InlineData(512)]
    [InlineData(513)]
    [InlineData(8193)]
    public async Task BuilderPreservesWorkerBoundariesAndOwnership(int count)
    {
        var prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, 0, (i * 17 % 97 - 48) / 8d, 1)).ToArray();
            var expected = WindowedSincSnapshot.Calculate(bars);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray(); var indicator = new WindowedSinc();
                using var run = await Build(input, indicator, mode).BuildAsync();
                Array.Clear(input);
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (mode == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(count, index);
                }
                else if (count > 0) Assert.Equal(bars[^1], run.Latest.Bar);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task OverflowInputPrecedenceCancellationAndGpuRejectionMatchContracts()
    {
        var weights = WindowedSincSnapshot.CreateWeights(3, 9, SincWindow.Rectangular, out _);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 1, 0,
            i < weights.Length ? Math.Sign(weights[i]) * double.MaxValue : 1, 1)).ToArray();
        var indicator = new WindowedSinc(3, 9, SincWindow.Rectangular);
        Assert.Throws<OverflowException>(() => WindowedSincSnapshot.Calculate(bars, 3, 9, SincWindow.Rectangular));
        for (int invalid = 0; invalid < 2; invalid++)
        {
            if (invalid == 1) bars[^1] = new Bar(default, 0, 1, 0, 1, double.NaN);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var ordinary = await Record.ExceptionAsync(() => Build(bars, indicator, mode).ConfigureBehavior(_ => { }).BuildAsync());
                var shared = await Record.ExceptionAsync(() => Build(bars, indicator, mode).BuildAsync());
                Assert.NotNull(ordinary); Assert.NotNull(shared);
                Assert.Equal(ordinary.GetType(), shared.GetType());
                Assert.Equal(ordinary.Message, shared.Message);
            }
        }
        var builder = Build([new Bar(default, 0, 1, 0, 1, 1)], indicator, IndicatorHistoryMode.Full);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    [Fact]
    public void UndefinedConfigurationsRejectAtConstruction()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowedSinc(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowedSinc(taps: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowedSinc(taps: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowedSinc(taps: 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WindowedSinc(window: (SincWindow)5));
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator indicator, IndicatorHistoryMode mode) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).ConfigureHistory(mode);
}
