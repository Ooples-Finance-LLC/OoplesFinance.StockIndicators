using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ContainmentParallelExecutionTests
{
    private static IndicatorBase Create(int kind, int period) => kind switch
    {
        0 => new HaramiCandle(period), 1 => new HaramiCrossCandle(period),
        2 => new HomingPigeonCandle(period), _ => new DojiStarCandle(period)
    };
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
    {
        double open = i % 12 == 10 ? 8 : i % 12 == 11 ? 4 : 4;
        double close = i % 12 == 10 ? 1 : i % 12 == 11 ? 3 : 6;
        if (i % 24 >= 12) (open, close) = (close, open);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), open, 10, 0, close, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task WorkerAndPartitionBoundariesMatchOriginalState(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count);
                // Force exact wide history across several worker seed boundaries.
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, 10); var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                foreach (int workers in new[] { 1, 2, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    var input = bars.ToArray();
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                        .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                    Array.Clear(input); run.Dispose(); Assert.Equal(expected, run[owner].ToArray());
                    Assert.Equal(bars[^1], run.Latest.Bar);
                    if (mode == IndicatorHistoryMode.Full)
                    {
                        int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                        Assert.Equal(count, row);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
    [Fact]
    public async Task MixedPeriodsAndOtherBoundedRootsRetainIdentityAndRecoverFromInvalidSeeds()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Data(1001);
            var roots = new IndicatorBase[] { Create(0, 3), Create(1, 10), Create(2, 63), Create(3, 1), new RollingPriceSum(7) };
            var expected = roots.Select(r => { var s = (IIndicatorState)r.CreateState(); return bars.Select(b => s.Update(b)).ToArray(); }).ToArray();
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var valid = bars[124]; bars[124] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                bars[124] = valid;
                using var run = await builder.BuildAsync();
                for (int i = 0; i < roots.Length; i++) Assert.Equal(expected[i], run[roots[i]].ToArray());
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
