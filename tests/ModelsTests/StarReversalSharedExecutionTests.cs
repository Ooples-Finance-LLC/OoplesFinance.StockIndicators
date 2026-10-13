using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class StarReversalSharedExecutionTests
{
    private static IndicatorBase Create(int kind, int period, int shortPeriod = 10, int dojiPeriod = 10, double penetration = .3) => kind switch
    {
        0 => new MorningStarCandle(period, shortPeriod, penetration),
        1 => new EveningStarCandle(period, shortPeriod, penetration),
        2 => new MorningDojiStarCandle(period, shortPeriod, dojiPeriod, penetration),
        3 => new EveningDojiStarCandle(period, shortPeriod, dojiPeriod, penetration),
        _ => new AbandonedBabyCandle(period, shortPeriod, dojiPeriod, penetration)
    };

    private static Bar[] Data(int count, int kind) => Enumerable.Range(0, count).Select(i =>
    {
        var (o, h, l, c) = (4d, 10d, 0d, 6d);
        if (i % 16 == 13) (o, h, l, c) = (8, 9, 1, 2);
        if (i % 16 == 14) (o, h, l, c) = (0, .5, -.5, 0);
        if (i % 16 == 15) (o, h, l, c) = (1.5, 7, 1, 6);
        if (kind is 1 or 3 || kind == 4 && i / 16 % 2 != 0) (o, h, l, c) = (-o, -l, -h, -c);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task PartitionBoundariesRetainExactDecisionsAndOwnedHistory(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (period, shortPeriod, dojiPeriod) in new[] { (1, 3, 5), (3, 1, 7), (10, 10, 10), (5, 63, 3), (3, 5, 63) })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, period, shortPeriod, dojiPeriod);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 10) { Assert.Contains(kind is 1 or 3 ? -100d : 100d, expected); if (kind == 4) Assert.Contains(-100d, expected); Assert.Contains(0d, expected); }
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
    public async Task StartupExtremesAndPenetrationBoundariesMatchIndependentReference(int kind)
    {
        foreach (int period in new[] { 1, 10, int.MaxValue - 2 })
        foreach (int shortPeriod in new[] { 1, 10, int.MaxValue - 2 })
        foreach (int count in new[] { 0, 1, 11, 12, 13, 65 })
            await CheckReference(Data(count, kind), Create(kind, period, shortPeriod, shortPeriod));
        foreach (double penetration in new[] { -0d, double.Epsilon, .25, .5, .75, 1, double.MaxValue })
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
        var bars = Data(1001, 4);
        var roots = new IndicatorBase[] { Create(0, 3), Create(1, 10, 3), Create(2, 63, 1, 3), Create(3, 3, 10, 63), Create(4, 10), new PiercingLineCandle(), new RollingPriceSum(7) };
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

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task ParallelExecutionPreservesExactPointThreePenetration(int kind)
    {
        // Binary .3 is slightly below 3/10: exact 5*.3 is below 1.5,
        // whereas the native rounded product is 1.5 and does not match.
        var bars = Enumerable.Range(0, 1001).Select(i =>
        {
            var (o, h, l, c) = (4d, 10d, 0d, 6d);
            if (i % 16 == 13) (o, h, l, c) = (7, 12, 2, 2);
            if (i % 16 == 14) (o, h, l, c) = (0, 1, -1, 1);
            if (i % 16 == 15) (o, h, l, c) = (1.25, 6, 1.125, 3.5);
            if (kind is 1 or 3) (o, h, l, c) = (-o, -l, -h, -c);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, 1);
        }).ToArray();
        var owner = Create(kind, 10);
        var expected = StarReversalReference.Evaluate(bars, 10, 10, kind < 2 ? 0 : 10, .3,
            kind == 4 ? null : kind is 0 or 2, kind == 4).ToArray();
        Assert.Equal(kind is 1 or 3 ? -100d : 100d, expected[15]);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars, owner, expected, mode);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static async Task CheckReference(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Star reversal reference", bars, [expected], 0));
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
