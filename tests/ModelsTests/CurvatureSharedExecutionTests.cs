using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CurvatureSharedExecutionTests
{
    [Theory]
    [InlineData(1, 2, 0)] [InlineData(1, 2, 513)] [InlineData(2, 2, 1)]
    [InlineData(2, 2, 513)] [InlineData(3, 7, 1025)]
    [InlineData(20, 20, 511)] [InlineData(20, 20, 512)] [InlineData(20, 20, 1025)]
    [InlineData(4096, 2, 8193)] [InlineData(4097, 20, 33)]
    [InlineData(int.MaxValue, int.MaxValue, 33)]
    public async Task RoundedStagesPreserveEveryColumnAndOwnedHistory(int inner, int outer, int count)
    {
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i, i + 1, i - 1, i % 127 < 67 ? 3 : (i * 13 % 101) / 8d, i + 2)).ToArray();
        await Check(bars, inner, outer);
    }

    [Fact]
    public async Task WideIntegerMomentsPromoteDemoteAndRejectOutsideTheirCertificate()
    {
        foreach (double magnitude in new[] { Math.BitDecrement(Math.ScaleB(1d, 63)), Math.ScaleB(1d, 63) })
        foreach (int scale in new[] { -1074, -63, 0, 900 })
        {
            // The first two-point fit produces +/-magnitude and +/-1 on the
            // common integer grid. Wide squared moments promote on the large
            // block and demote when it expires; equality at2^63 falls back.
            var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0,
                Math.ScaleB(i % 101 < 40 ? (i % 2 == 0 ? 0 : magnitude) : (i % 2 == 0 ? 0 : 1), scale), 0)).ToArray();
            await Check(bars, 2, 20);
        }
    }

    [Theory]
    [InlineData(3)] [InlineData(20)] [InlineData(256)] [InlineData(4096)]
    public async Task FullSignificandSlopesKeepIndependentRatioAndRootRounding(int period)
    {
        var bars = Enumerable.Range(0, Math.Max(513, period * 2 + 1)).Select(i =>
            new Bar(default, 0, 0, 0, 100 + (i * 13 % 101) / 8d, 0)).ToArray();
        await Check(bars, 20, period);
    }

    [Fact]
    public async Task WideFallbackAndSubnormalRoundingSurviveWorkerReplay()
    {
        foreach (int exponent in new[] { -1074, -1022, -500, 0, 700 })
        {
            var bars = Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0,
                Math.ScaleB((i * 13 % 101) - 50, exponent), 0)).ToArray();
            await Check(bars, 3, 20);
        }
        var random = new Random(845);
        await Check(Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0,
            i % 3 == 0 ? Math.ScaleB(random.NextDouble(), 700) : random.NextDouble(), 0)).ToArray(), 20, 7);
    }

    [Theory]
    [InlineData(1)] [InlineData(8)]
    public async Task InputThenIntermediateThenOuterValidationAndRecovery(int workers)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = workers;
            var bars = Enumerable.Repeat(new Bar(default, 0, 0, 0, 0, 0), 1025).ToArray();
            // First slopes are [0, h, -h,...]: outer slope overflows at row2.
            // A later first-stage overflow must be reported before that failure.
            double h = double.MaxValue * .75;
            bars[1] = new(default, 0, 0, 0, h, 0);
            bars[^2] = new(default, 0, 0, 0, -double.MaxValue, 0);
            bars[^1] = new(default, 0, 0, 0, double.MaxValue, double.NaN);
            var first = new WindowLinearRegression(2, WindowRegressionOutput.Slope);
            var last = new WindowRegressionStatistics(2); last.Of(first);
            var builder = Build(bars, last);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[^1] = new(default, 0, 0, 0, double.MaxValue, 0);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(typeof(WindowLinearRegression), error.IndicatorType);
            Assert.Equal(0, error.OutputSlot); Assert.Equal(1024, error.BarIndex);
            bars[^2] = bars[^1] = new(default, 0, 0, 0, 0, 0);
            error = await Assert.ThrowsAsync<IndicatorOutputException>(() => builder.BuildAsync());
            Assert.Equal(typeof(WindowRegressionStatistics), error.IndicatorType);
            Assert.Equal(0, error.OutputSlot); Assert.Equal(2, error.BarIndex);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            Array.Fill(bars, new Bar(default, 0, 0, 0, 3, 0));
            using var recovered = await builder.BuildAsync();
            Assert.All(recovered[last.Slope].ToArray(), x => Assert.Equal(0, x));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task DuplicateMixedAndNonSlopeRootsKeepGraphExecutionAndGpuRejects()
    {
        var bars = Enumerable.Range(0, 33).Select(i => new Bar(default, 0, 0, 0, i * i, 0)).ToArray();
        var first = new WindowLinearRegression(3, WindowRegressionOutput.Slope);
        var root = new WindowRegressionStatistics(7); root.Of(first);
        Assert.True(ValuesBarExecution.SupportsOwned([root]));
        Assert.False(ValuesBarExecution.SupportsOwned([root, root]));
        Assert.False(ValuesBarExecution.SupportsOwned([first, root]));
        var other = new WindowRegressionStatistics(7);
        other.Of(new WindowLinearRegression(3, WindowRegressionOutput.Endpoint));
        Assert.False(ValuesBarExecution.SupportsOwned([other]));
        var expected = new CustomIndicatorEngine(bars, _ => null).Compute(root);
        foreach (IIndicator[] roots in new[] { new IIndicator[] { root, root }, [first, root] })
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots).BuildAsync();
            for (int slot = 0; slot < 10; slot++)
                Assert.Equal(expected[slot], run[root.Outputs[slot]].ToArray());
        }
        var builder = Build(bars, root).ConfigureExecution(IndicatorExecutionBackend.Auto);
        using var automatic = await builder.BuildAsync();
        Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    [Fact]
    public async Task UnselectedInnerStatisticsDoNotRejectFiniteCurvature()
    {
        await Check(Enumerable.Range(0, 513).Select(i => new Bar(default, 0, 0, 0,
            i % 2 == 0 ? double.MaxValue * .75 : double.MaxValue * .5, 0)).ToArray(), 2, 20);
    }

    private static async Task Check(Bar[] bars, int inner, int outer)
    {
        var first = new WindowLinearRegression(inner, WindowRegressionOutput.Slope);
        var last = new WindowRegressionStatistics(outer); last.Of(first);
        // Original graph preserves rounding between stages and validates the
        // complete selected intermediate before any outer statistic.
        var engine = new CustomIndicatorEngine(bars, _ => null);
        var expected = engine.Compute(last);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                var builder = Build(input, last).ConfigureHistory(mode);
                using var run = await builder.BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
                Array.Clear(input); run.Dispose();
                for (int slot = 0; slot < 10; slot++)
                    Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),
                        run[last.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator root) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureExecution(IndicatorExecutionBackend.Cpu);
}
