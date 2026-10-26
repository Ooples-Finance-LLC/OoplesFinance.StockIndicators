using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CrowSoldierSharedExecutionTests
{
    private static IndicatorBase Create(int kind, int period, int near = 5, int far = 5, int body = 10) => kind switch
    {
        0 => new ThreeBlackCrowsCandle(period), 1 => new IdenticalThreeCrowsCandle(period, near),
        _ => new ThreeWhiteSoldiersCandle(period, near, far, body)
    };

    private static Bar[] Data(int count, int kind) => Enumerable.Range(0, count).Select(i =>
    {
        var (o, h, l, c) = (4d, 10d, 0d, 6d);
        int phase = i % 16;
        if (phase == 12) (o, h, l, c) = (6, 10, 0, 8);
        if (phase >= 13)
        {
            int step = phase - 13;
            (o, c) = kind switch
            {
                0 => (9 - step, 5 - step),
                1 => (9 - 4 * step, 5 - 4 * step),
                _ => (1 + 4 * step, 7 + 4 * step)
            };
            (h, l) = kind == 2 ? (c, c - 10) : (c + 10, c);
        }
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task PartitionBoundariesRetainExactDecisionsAndOwnedHistory(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (period, near, far, body) in new[] { (10, 5, 5, 10), (1, 3, 7, 5), (7, 1, 3, 5), (3, 5, 63, 1), (3, 5, 1, 63), (63, 1, 3, 5) })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, period, near, far, body);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 10) { Assert.Contains(kind == 2 ? 100d : -100d, expected); Assert.Contains(0d, expected); }
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
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task StartupExtremesAndThresholdsMatchIndependentReference(int kind)
    {
        foreach (var (period, near, far, body) in new[] { (1, 3, 5, 7), (10, 5, 5, 10), (kind == 0 ? int.MaxValue - 3 : int.MaxValue - 2, 5, 5, 10), (10, int.MaxValue - 2, 5, 10), (10, 5, int.MaxValue - 2, 10), (10, 5, 5, int.MaxValue - 2) })
        foreach (int count in new[] { 0, 1, 2, 3, 11, 12, 13, 14, 65 })
            await CheckReference(Data(count, kind), Create(kind, period, near, far, body));
        await CheckReference(Data(513, kind), Create(kind, 10));
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
        var roots = new IndicatorBase[] { Create(0, 3), Create(1, 63, 1), Create(2, 3, 1, 63, 5), Create(2, 5, 63, 3, 1), new TristarCandle(), new RollingPriceSum(7) };
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

    [Fact]
    public void EqualAndUnequalRangesReplayAfterResetAgainstIndependentReference()
    {
        foreach (int kind in new[] { 0, 1, 2 })
        foreach (var (period, near, far) in new[] { (10, 5, 5), (3, 3, 3), (3, 1, 1), (3, 5, 7), (3, 7, 5) })
        {
            var owner = Create(kind, period, near, far);
            var state = (IIndicatorState)owner.CreateState();
            var bars = Data(513, kind);
            for (int i = 124; i < bars.Length; i += 125)
                bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
            var first = bars.Select(b => state.Update(b)).ToArray();
            foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
                rule.Check(new IndicatorValidationContext("CrowSoldier reset reference", bars, [first], 0));
            state.Reset();
            Assert.Equal(first, bars.Select(b => state.Update(b)).ToArray());
            state.Reset();
            Assert.Equal(0d, state.Update(bars[0]));
        }
    }

    private static async Task CheckReference(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Crow/soldier reference", bars, [expected], 0));
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
