using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class RegressionStatisticsSharedExecutionTests
{
    [Theory]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(2, 513)]
    [InlineData(20, 511)] [InlineData(20, 512)] [InlineData(20, 513)] [InlineData(20, 8193)]
    [InlineData(4096, 8193)] [InlineData(4097, 8194)] [InlineData(int.MaxValue, 33)]
    public async Task EveryColumnSurvivesPartitionBoundariesAndOwnedHistory(int period, int count)
    {
        var bars = Enumerable.Range(0, count).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1,
                i % 127 < 67 ? 3 : (i * 13 % 101) / 8d, i + 2)).ToArray();
        await Check(bars, period);
    }

    [Fact]
    public async Task NonDyadicWideAndSubnormalInputsPreserveExactWindowStatistics()
    {
        var random = new Random(7184);
        var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 0, 0,
            (i % 5) switch
            {
                0 => Math.ScaleB(1 + random.NextDouble(), 700),
                1 => -Math.ScaleB(1 + random.NextDouble(), -700),
                2 => i % 2 == 0 ? -0d : 0d,
                3 => double.Epsilon * (i % 7),
                _ => random.NextDouble() * 20 - 10
            }, 0)).ToArray();
        await Check(bars, 20);
    }

    [Theory]
    [InlineData(2)] [InlineData(20)] [InlineData(4096)]
    public async Task IntegerCertificateBoundaryAndFallbackPreserveEveryMoment(int period)
    {
        long limit = (1L << 30) / period;
        foreach (int excess in new[] { 0, 1 })
        {
            var bars = Enumerable.Range(0, Math.Max(513, 2 * period + 1)).Select(i =>
                new Bar(default, 0, 0, 0, (i % 2 == 0 ? 1 : -1) * (limit + excess - i % 5), 0)).ToArray();
            await Check(bars, period);
        }
    }

    [Theory]
    [InlineData(-1074)] [InlineData(-1070)] [InlineData(-1022)]
    [InlineData(-500)] [InlineData(0)] [InlineData(500)] [InlineData(990)]
    public async Task CertifiedGridPublishesOnFinalRoundingGrid(int grid)
    {
        var bars = Enumerable.Range(0, 513).Select(i =>
            new Bar(default, 0, 0, 0, Math.ScaleB(i % 17 - 8, grid), 0)).ToArray();
        await Check(bars, 3);
    }

    [Fact]
    public async Task CertifiedGridRetainsNegativeUnderflowZeroAndRejectsOverflow()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, 513).Select(i =>
                new Bar(default, 0, 0, 0, i % 3 == 0 ? double.Epsilon : 0, 0)).ToArray();
            var indicator = new WindowRegressionStatistics(3);
            using var run = await Build(bars, indicator).BuildAsync();
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(run[indicator.Slope][2]));
            double h = Math.ScaleB(1, 1023);
            Array.Fill(bars, new Bar(default, 0, 0, 0, h, 0));
            bars[1] = new(default, 0, 0, 0, -h, 0);
            var error = await Assert.ThrowsAsync<IndicatorOutputException>(() => Build(bars, indicator).BuildAsync());
            Assert.Equal(0, error.OutputSlot); Assert.Equal(1, error.BarIndex);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(1)] [InlineData(8)]
    public async Task AllInputValidationPrecedesSlotMajorOverflowAndRecovery(int workers)
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
            var indicator = new WindowRegressionStatistics(2);
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
            Assert.All(recovered[indicator.RSquaredIsDefined].ToArray(), x => Assert.Equal(0d, x));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static async Task Check(Bar[] bars, int period)
    {
        // Arithmetic already has a separate frozen-state/rational reference suite.
        // Serial state here isolates replay, column, capture and facade failures.
        var expected = Enumerable.Range(0, 10).Select(_ => new double[bars.Length]).ToArray();
        using var state = new WindowRegressionStatistics.State(period);
        var scratch = new double[10];
        for (int i = 0; i < bars.Length; i++)
        {
            state.Update(bars[i], scratch);
            for (int slot = 0; slot < 10; slot++) expected[slot][i] = scratch[slot];
        }
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                var indicator = new WindowRegressionStatistics(period);
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

    private static StockIndicatorBuilder Build(Bar[] bars, WindowRegressionStatistics indicator) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
}
