using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class HikkakeSharedExecutionTests
{
    private static IndicatorBase Create(int kind, int period) => kind == 0 ? new HikkakeCandle() : new ModifiedHikkakeCandle(period);

    private static Bar B(int i, double high, double low, double close) => new(DateTime.UnixEpoch.AddMinutes(i), close, high, low, close, i + 1);
    private static Bar[] Data(int count, int kind) => Enumerable.Range(0, count).Select(i =>
    {
        var (h,l,c) = (10d,0d,5d);
        int phase = i % 20;
        if (kind == 0)
            (h,l,c) = phase switch { 15 => (8,2,5), 16 => (7,1,4), 17 => (10,1,9), _ => (h,l,c) };
        else
            (h,l,c) = phase switch { 14 => (8,2,4), 15 => (7,3,5), 16 => (6,2,4), 17 => (10,2,9), _ => (h,l,c) };
        return i / 20 % 2 == 0 ? B(i,h,l,c) : B(i,-l,-h,-c);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task PartitionBoundariesRetainExactDecisionsAndOwnedHistory(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int period in new[] { 1, 3, 5, 63 })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, period);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 5) { Assert.Contains(100d, expected); Assert.Contains(-100d, expected); Assert.Contains(200d, expected); Assert.Contains(-200d, expected); }
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
    [InlineData(0)] [InlineData(1)]
    public async Task StartupExtremesAndThresholdsMatchIndependentReference(int kind)
    {
        foreach (int period in new[] { 1, 5, int.MaxValue - 5 })
        foreach (int count in new[] { 0, 1, 2, 3, 4, 5, 6, 9, 10, 11, 65 })
            await CheckReference(Data(count, kind), Create(kind, period));
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
        var roots = new IndicatorBase[] { Create(0, 3), Create(1, 1), Create(1, 5), Create(1, 63), new LadderBottomCandle(), new RollingPriceSum(7) };
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
    [InlineData(0)] [InlineData(1)]
    public async Task PendingConfirmationSurvivesWorkerBoundaryAndExpiresAfterThreeBars(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var owner = Create(kind, 5);
            foreach (int boundary in new[] { owner.WarmupBars, 125, 250, 500, 875 })
            foreach (int delay in new[] { 1, 2, 3, 4 })
            foreach (int sign in new[] { 1, -1 })
            {
                // Delay four cannot form early enough for the first visible row.
                if (boundary == owner.WarmupBars && delay == 4) continue;
                var bars = Enumerable.Range(0, 1001).Select(i => B(i,10,0,5)).ToArray();
                int f = boundary - delay;
                if (kind == 0) { bars[f-1] = B(f-1,8,2,5); bars[f] = B(f,7,1,4); }
                else { bars[f-2] = B(f-2,8,2,4); bars[f-1] = B(f-1,7,3,5); bars[f] = B(f,6,2,4); }
                double insideHigh = kind == 0 ? 8 : 7, breakoutLow = kind == 0 ? 1 : 2;
                for (int i=f+1; i<boundary; i++) bars[i] = B(i,insideHigh,breakoutLow,insideHigh);
                bars[boundary] = B(boundary,10,breakoutLow,9);
                if (sign < 0) bars = bars.Select(b => new Bar(b.Time,-b.Open,-b.Low,-b.High,-b.Close,b.Volume)).ToArray();
                var expected = HikkakeReference.Evaluate(bars, kind == 0 ? 0 : 5).ToArray();
                Assert.Equal(delay <= 3 ? sign * 200d : 0d, expected[boundary]);
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
    [InlineData(0)] [InlineData(1)]
    public async Task NewFormationOverridesPendingConfirmationAcrossWorkerBoundary(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int boundary in new[] { 125, 250, 500, 875 })
            foreach (int sign in new[] { 1, -1 })
            {
                var bars = Enumerable.Range(0, 1001).Select(i => B(i,10,0,5)).ToArray();
                int f = boundary - (kind == 0 ? 2 : 3);
                if (kind == 0)
                {
                    bars[f-1] = B(f-1,8,2,5); bars[f] = B(f,7,1,4);
                    bars[f+1] = B(f+1,6,2,5); bars[f+2] = B(f+2,9,3,8.5);
                    bars[f+3] = B(f+3,9,0,1);
                }
                else
                {
                    bars[f-2] = B(f-2,8,2,4); bars[f-1] = B(f-1,7,3,6); bars[f] = B(f,6,2,4);
                    bars[f+1] = B(f+1,5,3,4); bars[f+2] = B(f+2,4.5,3.5,4);
                    bars[f+3] = B(f+3,9,4,8); bars[f+4] = B(f+4,9,0,2);
                }
                if (sign < 0) bars = bars.Select(b => new Bar(b.Time,-b.Open,-b.Low,-b.High,-b.Close,b.Volume)).ToArray();
                var expected = HikkakeReference.Evaluate(bars, kind == 0 ? 0 : 5).ToArray();
                Assert.Equal(sign * 100d, expected[f]);
                Assert.Equal(sign * -100d, expected[boundary]);
                Assert.Equal(sign * -200d, expected[boundary+1]);
                foreach (int workers in new[] { 1, 2, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CheckOwned(bars, Create(kind, 5), expected, mode);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    private static async Task CheckReference(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Hikkake reference", bars, [expected], 0));
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
