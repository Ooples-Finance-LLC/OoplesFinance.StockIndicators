using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class MultiBarCandleSharedExecutionTests
{
    private static IndicatorBase Create(int kind, int period, int shortPeriod = 10, double penetration = .5) => kind switch
    {
        0 => new RisingFallingThreeMethodsCandle(period, shortPeriod),
        1 => new MatHoldCandle(period, shortPeriod, penetration),
        2 => new BreakawayCandle(period), 3 => new ConcealingBabySwallowCandle(period),
        _ => new LadderBottomCandle(period)
    };

    private static readonly (double O, double H, double L, double C)[][] Patterns =
    [
        [(2,10,0,8),(7,10,0,6),(6,10,0,5),(5,10,0,4),(5,11,1,10)],
        [(2,10,0,8),(11,12,2,10),(9,11,1,7),(8,10,0,6),(7,14,4,13)],
        [(10,12,2,4),(2,3,-1,0),(0,2,-2,1),(0,1,-3,-2),(-1,5,-2,3)],
        [(10,10,0,0),(8,8,-2,-2),(-3,0,-10,-5),(1,1,-11,-11)],
        [(9,10,0,5),(8,10,0,4),(7,10,0,3),(6,8,-2,2),(7,10,0,9)]
    ];

    private static Bar[] Data(int count, int kind) => Enumerable.Range(0, count).Select(i =>
    {
        var (o, h, l, c) = (4d, 10d, 0d, 6d);
        var pattern = Patterns[kind];
        int phase = i % 20 - (20 - pattern.Length);
        if (phase >= 0) (o, h, l, c) = pattern[phase];
        if (i / 20 % 2 != 0) (o, h, l, c) = (-o, -l, -h, -c);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task PartitionBoundariesRetainExactDecisionsAndOwnedHistory(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (period, shortPeriod) in new[] { (1, 3), (3, 1), (10, 10), (5, 63), (63, 5) })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, period, shortPeriod);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 10) { Assert.Contains(100d, expected); if (kind is 0 or 2) Assert.Contains(-100d, expected); Assert.Contains(0d, expected); }
                foreach (int workers in new[] { 1, 2, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CheckOwned(bars, owner, expected, mode);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task StartupExtremesAndThresholdsMatchIndependentReference(int kind)
    {
        foreach (int period in new[] { 1, 10, kind == 3 ? int.MaxValue - 3 : int.MaxValue - 4 })
        foreach (int shortPeriod in kind < 2 ? new[] { 1, 10, int.MaxValue - 4 } : new[] { 10 })
        foreach (int count in new[] { 0, 1, 2, 3, 4, 12, 13, 14, 15, 65 })
            await CheckReference(Data(count, kind), Create(kind, period, shortPeriod));
        await CheckReference(Data(513, kind), Create(kind, 10));
        if (kind == 1)
            foreach (double penetration in new[] { -0d, double.Epsilon, .3, .5, 1, double.MaxValue })
                await CheckReference(Data(513, kind), Create(kind, 10, penetration: penetration));
        var random = new Random(837);
        double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1020));
        await CheckReference(Enumerable.Range(0, 129).Select(_ => new Bar(default, Next(), Next(), Next(), Next(), 1)).ToArray(), Create(kind, 3));
        foreach (double scale in new[] { double.Epsilon, double.MaxValue / 32, 1e-200, -0d })
            await CheckReference(Data(65, kind).Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, 1)).ToArray(), Create(kind, 3));
    }

    [Fact]
    public async Task MixedRootsPreserveValidationCancellationAndIdentity()
    {
        var bars = Data(1001, 1);
        var roots = new IndicatorBase[] { Create(0, 3, 63), Create(1, 63, 3, .3), Create(2, 3), Create(3, 10), Create(4, 63), new AdvanceBlockCandle(), new RollingPriceSum(7) };
        var expected = roots.Select(r => { var s = (IIndicatorState)r.CreateState(); return bars.Select(b => s.Update(b)).ToArray(); }).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var valid = bars[124]; bars[124] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[124] = valid;
            using var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            for (int i = 0; i < roots.Length; i++) Assert.Equal(expected[i], run[roots[i]].ToArray());
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
    }

    private static async Task CheckReference(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Multi-bar candle reference", bars, [expected], 0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars, owner, expected, mode);
    }

    private static async Task CheckOwned(Bar[] bars, IndicatorBase owner, double[] expected, IndicatorHistoryMode mode)
    {
        var input = bars.ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
        Array.Clear(input); run.Dispose(); Assert.Equal(expected, run[owner].ToArray());
        if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
        if (mode == IndicatorHistoryMode.Full)
        {
            int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
    }
}
