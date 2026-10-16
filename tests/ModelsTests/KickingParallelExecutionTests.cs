using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class KickingParallelExecutionTests
{
    private static IndicatorBase Create(int kind, int period, int shadowPeriod = 10) => kind switch
    {
        0 => new KickingCandle(period, shadowPeriod),
        _ => new KickingByLengthCandle(period, shadowPeriod)
    };
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
    {
        // Include both directions, exact length ties, longer current bodies,
        // and nonmatches in ordinary history before adding extreme seed bars.
        var b = i % 12 == 10 ? new Bar(default, 10, 10, 0, 0, 1)
            : i % 12 == 11 ? new Bar(default, 12, 22 + i / 24 % 2, 12, 22 + i / 24 % 2, 1)
            : new Bar(default, 4, 10, 0, 6, 1);
        return i / 12 % 2 == 0
            ? new Bar(DateTime.UnixEpoch.AddMinutes(i), b.Open, b.High, b.Low, b.Close, i + 1)
            : new Bar(DateTime.UnixEpoch.AddMinutes(i), -b.Open, -b.Low, -b.High, -b.Close, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task WorkerAndPartitionBoundariesMatchOriginalState(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (body, shadow) in new[] { (1, 3), (3, 1), (10, 10), (5, 63) })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count);
                // Force exact wide history across several worker seed boundaries.
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, body, shadow); var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (body == 10 && shadow == 10)
                {
                    Assert.Contains(-100d, expected); Assert.Contains(100d, expected); Assert.Contains(0d, expected);
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
            var roots = new IndicatorBase[] { Create(0, 3, 10), Create(1, 10, 3), Create(0, 63, 1), new OnNeckCandle(1, 3), new RollingPriceSum(7) };
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
