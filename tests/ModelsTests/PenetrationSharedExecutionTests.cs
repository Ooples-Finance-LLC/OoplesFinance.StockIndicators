using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class PenetrationSharedExecutionTests
{
    private static IndicatorBase Create(bool piercing, int period, double penetration = .5) =>
        piercing ? new PiercingLineCandle(period) : new DarkCloudCoverCandle(period, penetration);

    private static Bar[] Data(int count, bool piercing) => Enumerable.Range(0, count).Select(i =>
    {
        var (o, h, l, c) = (4d, 10d, 0d, 6d);
        if (i % 16 == 14) (o, h, l, c) = (8, 10, 0, 0);
        if (i % 16 == 15) (o, h, l, c) = (-1, 7, -2, 6);
        if (!piercing) (o, h, l, c) = (-o, -l, -h, -c);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task PartitionBoundariesRetainExactDecisionsAndOwnedHistory(bool piercing)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int period in new[] { 1, 3, 10, 63 })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, piercing);
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(piercing, period);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 10) { Assert.Contains(piercing ? 100d : -100d, expected); Assert.Contains(0d, expected); }
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
    [InlineData(true)] [InlineData(false)]
    public async Task StartupExtremesAndPenetrationBoundariesMatchIndependentReference(bool piercing)
    {
        foreach (int period in new[] { 1, 10, int.MaxValue - 1 })
        foreach (int count in new[] { 0, 1, 10, 11, 12, 65 })
            await CheckReference(Data(count, piercing), Create(piercing, period));
        foreach (double penetration in new[] { -0d, double.Epsilon, .25, .5, .75, 1, double.MaxValue })
            await CheckReference(Data(513, piercing), Create(piercing, 10, penetration));
        var random = new Random(837);
        double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1020));
        await CheckReference(Enumerable.Range(0, 129).Select(_ => new Bar(default, Next(), Next(), Next(), Next(), 1)).ToArray(), Create(piercing, 3));
        foreach (double scale in new[] { double.Epsilon, double.MaxValue / 32, 1e-200, -0d })
            await CheckReference(Data(65, piercing).Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, 1)).ToArray(), Create(piercing, 3));
    }

    [Fact]
    public async Task MixedRootsPreserveValidationCancellationAndIdentity()
    {
        var bars = Data(1001, true);
        var roots = new IndicatorBase[] { Create(true, 3), Create(false, 10, .25), Create(false, 63, 1), new ThreeInsideCandle(), new RollingPriceSum(7) };
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
            rule.Check(new IndicatorValidationContext("Penetration reference", bars, [expected], 0));
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
