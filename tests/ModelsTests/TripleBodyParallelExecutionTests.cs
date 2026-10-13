using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TripleBodyParallelExecutionTests
{
    private static IndicatorBase Create(int kind, int period, int shortPeriod = 10) => kind switch
    {
        0 => new ThreeInsideCandle(period, shortPeriod), 1 => new TwoCrowsCandle(period),
        2 => new UpsideGapTwoCrowsCandle(period, shortPeriod), _ => new UniqueThreeRiverCandle(period, shortPeriod)
    };
    private static Bar[] Data(int count, int kind = 0) => Enumerable.Range(0, count).Select(i =>
    {
        int phase = i % 16;
        Bar b = new(default, 4, 30, 0, 6, 1);
        if (phase == 13) b = kind == 3 ? new(default, 9, 30, 0, 2, 1) : new(default, 2, 30, 0, 9, 1);
        if (phase == 14) b = kind switch
        {
            0 => new(default, 4, 30, 0, 6, 1),
            3 => new(default, 6, 30, -1, 4, 1),
            _ => new(default, 12, 30, 0, 10, 1)
        };
        if (phase == 15) b = kind switch
        {
            0 => new(default, 8, 30, -2, 1, 1),
            1 => new(default, 11, 30, -2, 5, 1),
            2 => new(default, 13, 30, -2, 9.5, 1),
            _ => new(default, 0, 30, -2, 1, 1)
        };
        if (kind == 0 && i / 16 % 2 != 0) b = new(default, -b.Open, -b.Low, -b.High, -b.Close, 1);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Open, b.High, b.Low, b.Close, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task WorkerAndPartitionBoundariesMatchOriginalState(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (body, shortPeriod) in new[] { (1, 3), (3, 1), (10, 10), (5, 63) })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                // Force exact wide history across several worker seed boundaries.
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, body, shortPeriod); var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (body == 10 && shortPeriod == 10)
                {
                    Assert.Contains(kind == 3 ? 100d : -100d, expected); Assert.Contains(0d, expected);
                    if (kind == 0) Assert.Contains(100d, expected);
                }
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
            var roots = new IndicatorBase[] { Create(0, 3, 10), Create(1, 10, 3), Create(2, 63, 1), Create(3, 1), new ThreeOutsideCandle(), new RollingPriceSum(7) };
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
