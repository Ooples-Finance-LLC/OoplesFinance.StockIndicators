using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SequenceParallelExecutionTests
{
    private static IndicatorBase Create(int kind, int period) => kind switch
    {
        0 => new MatchingLowCandle(period), 1 => new StickSandwichCandle(period),
        2 => new ThreeLineStrikeCandle(period), _ => new ThreeOutsideCandle()
    };
    private static Bar[] Data(int count, int kind = 0) => Enumerable.Range(0, count).Select(i =>
    {
        int phase = i % 16;
        Bar b = new(default, 4, 10, 0, 6, 1);
        if (kind == 0 && phase is 14 or 15) b = new(default, 8, 10, 0, 4, 1);
        if (kind == 1)
        {
            if (phase is 13 or 15) b = new(default, 8, 10, 0, 4, 1);
            if (phase == 14) b = new(default, 6, 10, 5, 8, 1);
        }
        if (kind == 2)
        {
            if (phase == 12) b = new(default, 2, 10, 0, 4, 1);
            if (phase == 13) b = new(default, 3, 10, 0, 6, 1);
            if (phase == 14) b = new(default, 5, 12, 0, 10, 1);
            if (phase == 15) b = new(default, 11, 12, 0, 1, 1);
        }
        if (kind == 3)
        {
            if (phase == 13) b = new(default, 8, 10, 0, 4, 1);
            if (phase == 14) b = new(default, 3, 10, 0, 9, 1);
            if (phase == 15) b = new(default, 11, 12, 0, 10, 1);
        }
        if (kind >= 2 && i / 16 % 2 != 0) b = new(default, -b.Open, -b.Low, -b.High, -b.Close, 1);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Open, b.High, b.Low, b.Close, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task WorkerAndPartitionBoundariesMatchOriginalState(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int period in kind == 3 ? new[] { 1 } : new[] { 1, 5, 63 })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                // Force exact wide history across several worker seed boundaries.
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, period); var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 5 || kind == 3)
                {
                    Assert.Contains(100d, expected); Assert.Contains(0d, expected);
                    if (kind >= 2) Assert.Contains(-100d, expected);
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
