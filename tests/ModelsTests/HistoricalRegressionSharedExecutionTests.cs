using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class HistoricalRegressionSharedExecutionTests
{
    [Theory]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(20, 19)] [InlineData(20, 20)]
    [InlineData(20, 511)] [InlineData(20, 512)] [InlineData(20, 513)] [InlineData(20, 1025)]
    [InlineData(4096, 8193)] [InlineData(4097, 8194)] [InlineData(int.MaxValue, 33)]
    public async Task GlobalCoordinatesAndFinalOverlaySurviveWorkerBoundaries(int period, int count)
    {
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i, i + 1, i - 1, i % 127 < 67 ? 3 : (i * 13 % 101) / 8d, i + 2)).ToArray();
        await Check(bars, period);
    }

    [Theory]
    [InlineData(-1074)] [InlineData(-1022)] [InlineData(0)] [InlineData(990)]
    public async Task CertifiedGridPreservesGlobalRoundingAndFinalLine(int grid)
    {
        var bars = Enumerable.Range(0, 513).Select(i =>
            new Bar(default, 0, 0, 0, Math.ScaleB(i % 17 - 8, grid), 0)).ToArray();
        await Check(bars, 20);
    }

    [Fact]
    public async Task UncertifiedGridsReplayWithOriginalGlobalCoordinates()
    {
        var random = new Random(1872);
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0,
            Math.ScaleB(random.NextDouble() - .5, i % 3 == 0 ? 700 : -700), 0)).ToArray();
        await Check(bars, 20);
    }

    [Fact]
    public async Task LargeCenteredMomentsKeepExactGlobalInterceptProducts()
    {
        const int period = 4096;
        long limit = (1L << 30) / period;
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 0, 0,
            (i % 8192 < 4096 ? 1 : -1) * (limit - i % 3), 0)).ToArray();
        await Check(bars, period);
    }

    [Fact]
    public async Task UnpublishedRollingEndpointCannotRejectFiniteFinalOverlay()
    {
        var bars = Enumerable.Range(0, 513).Select(i =>
            new Bar(default, 0, 0, 0, i < 10 ? 0 : double.MaxValue, 0)).ToArray();
        using var rolling = new WindowRegressionStatistics.State(20);
        var values = new double[10];
        for (int i = 0; i < 20; i++) rolling.Update(bars[i], values);
        Assert.True(double.IsPositiveInfinity(values[4]));
        await Check(bars, 20);
    }

    [Fact]
    public async Task DuplicateRootsAndMixedRootsKeepTheirCompletedOverlay()
    {
        var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0, i % 17, 0)).ToArray();
        var indicator = new RegressionWithFinalLine(20);
        using var expected = await Build(bars, indicator).BuildAsync();
        foreach (var additional in new IIndicator[] { indicator, new Sma(3) })
        {
            using var run = await Build(bars, indicator).ConfigureIndicators(additional).BuildAsync();
            for (int slot = 0; slot < 10; slot++)
                Assert.Equal(expected[indicator.Outputs[slot]].ToArray(), run[indicator.Outputs[slot]].ToArray());
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(8)]
    public async Task InputsPrecedeSlotMajorOverflowAndCancellationRecovers(int workers)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = workers;
            var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, 0, 0), 1025).ToArray();
            bars[0] = new(default, 0, 0, 0, double.MaxValue / 2, 0);
            bars[1] = new(default, 0, 0, 0, -double.MaxValue / 2, 0);
            bars[^2] = new(default, 0, 0, 0, -double.MaxValue, 0);
            bars[^1] = new(default, 0, 0, 0, double.MaxValue, double.NaN);
            var indicator = new RegressionWithFinalLine(2);
            var builder = Build(bars, indicator);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = new(default, 0, 0, 0, double.MaxValue, 0);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(0, error.OutputSlot); Assert.Equal(1024, error.BarIndex);
            bars[^2] = bars[^1] = new(default, 0, 0, 0, 0, 0);
            error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(1, error.OutputSlot); Assert.Equal(1, error.BarIndex);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            Array.Fill(bars, new Bar(default, 0, 0, 0, 3, 0));
            using var recovered = await builder.BuildAsync();
            Assert.All(recovered[indicator.Slope].ToArray(), x => Assert.Equal(0d, x));
            Assert.Equal(new[] { 3d, 3d }, recovered[indicator.Line].ToArray()[^2..]);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static async Task Check(Bar[] bars, int period)
    {
        // The original historical writer retains whole-prefix coordinates and
        // independent completion; rational/native reference fixtures also run.
        var expected = Enumerable.Range(0, 10).Select(_ => new double[bars.Length]).ToArray();
        using var writer = new RegressionWithFinalLine.Writer(period, bars.Length);
        var scratch = new double[10];
        for (int i = 0; i < bars.Length; i++) writer.Append(bars[i], i, expected, scratch);
        writer.Complete(expected, default);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                var indicator = new RegressionWithFinalLine(period);
                using var run = await Build(input, indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input); run.Dispose();
                for (int slot = 0; slot < 10; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                        run[indicator.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(bars.Length, index);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, RegressionWithFinalLine indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
}
