using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class GapContinuationSharedExecutionTests
{
    private static IndicatorBase Create(int kind, int period) => kind switch
    {
        0 => new TristarCandle(period), 1 => new TasukiGapCandle(period),
        _ => new UpDownSideGapThreeMethodsCandle()
    };

    private static Bar[] Data(int count, int kind) => Enumerable.Range(0, count).Select(i =>
    {
        var (o, c) = (4d, 6d);
        if (i % 16 == 13) (o, c) = kind == 0 ? (5, 5) : (2, 4);
        if (i % 16 == 14) (o, c) = kind == 0 ? (3, 3) : (6, 8);
        if (i % 16 == 15) (o, c) = kind == 0 ? (4, 4) : kind == 1 ? (7, 5) : (7, 3);
        var (h, l) = (10d, 0d);
        if (i / 16 % 2 != 0) (o, h, l, c) = (-o, -l, -h, -c);
        return new Bar(DateTime.UnixEpoch.AddMinutes(i), o, h, l, c, i + 1);
    }).ToArray();

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task PartitionBoundariesRetainExactDecisionsAndOwnedHistory(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int period in new[] { 1, 3, 10, 63 })
            foreach (int count in new[] { 511, 512, 513, 1001, 10000 })
            {
                var bars = Data(count, kind);
                for (int i = 124; i < count; i += 125)
                    bars[i] = new(bars[i].Time, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, i);
                var owner = Create(kind, period);
                var state = (IIndicatorState)owner.CreateState();
                var expected = bars.Select(b => state.Update(b)).ToArray();
                if (period == 10) { Assert.Contains(100d, expected); Assert.Contains(-100d, expected); Assert.Contains(0d, expected); }
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
        foreach (int period in new[] { 1, 10, int.MaxValue - 2 })
        foreach (int count in new[] { 0, 1, 2, 3, 6, 7, 8, 11, 12, 13, 65 })
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
        var roots = new IndicatorBase[] { Create(0, 3), Create(0, 63), Create(1, 5), Create(1, 63), Create(2, 1), new AbandonedBabyCandle(), new RollingPriceSum(7) };
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
    [InlineData(-1, 100d)] [InlineData(0, 0d)] [InlineData(1, -100d)]
    public async Task TristarGapMustBeStrictAtAdjacentBinaryPrices(int step, double expected)
    {
        double middle = step < 0 ? Math.BitDecrement(5d) : step > 0 ? Math.BitIncrement(5d) : 5d;
        Bar[] bars = [new(default, 5, 10, 0, 5, 1), new(default, 5, 10, 0, 5, 1),
            new(default, middle, 10, 0, middle, 1), new(default, 5, 10, 0, 5, 1)];
        Assert.Equal(expected, GapContinuationReference.Evaluate(bars, 1, GapContinuationKind.Tristar)[^1]);
        await CheckReference(bars, new TristarCandle(1));
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void TristarNonfiniteDojiOperandsThrowBeforeChangingState(int position, bool close)
    {
        foreach (double invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            var state = (IIndicatorState)new TristarCandle(1).CreateState();
            Bar[] pattern = [new(default, 5, 10, 0, 5, 1), new(default, 5, 10, 0, 5, 1),
                new(default, 5, 10, 0, 5, 1), new(default, 5, 10, 0, 5, 1)];
            int index = position + 1;
            pattern[index] = new(default, close ? 5 : invalid, 10, 0, close ? invalid : 5, 1);
            for (int i = 0; i < 3; i++) Assert.Equal(0d, state.Update(pattern[i]));
            // Range accumulation accepts these finite high/low prices. With
            // no strict gap, the original Doji still rejects its nonfinite O/C.
            for (int repeat = 0; repeat < 2; repeat++)
            {
                var error = Assert.Throws<ArithmeticException>(() => state.Update(pattern[3]));
                Assert.Equal("An exact finite mean requires finite inputs.", error.Message);
            }
            if (position == 2)
            {
                Bar[] continuation = [new(default, 5, 10, 0, 5, 1),
                    new(default, 3, 10, 0, 3, 1), new(default, 4, 10, 0, 4, 1)];
                var valid = pattern[..3].Concat(continuation).ToArray();
                var expected = GapContinuationReference.Evaluate(valid, 1, GapContinuationKind.Tristar);
                for (int i = 0; i < continuation.Length; i++)
                    Assert.Equal(expected[i + 3], state.Update(continuation[i]));
                Assert.Equal(100d, expected[^1]);
            }
        }
    }

    [Fact]
    public void TristarInvalidRangeRetainsUpdateAndNextDequeueErrors()
    {
        var state = (IIndicatorState)new TristarCandle(1).CreateState();
        var bar = new Bar(default, 5, 10, 0, 5, 1);
        for (int i = 0; i < 3; i++) Assert.Equal(0d, state.Update(bar));
        var bad = new Bar(default, 5, double.NaN, 0, 5, 1);
        var error = Assert.Throws<ArithmeticException>(() => state.Update(bad));
        Assert.Equal("An exact finite mean requires finite inputs.", error.Message);
        // Update enqueues before accumulating, so the next valid bar still
        // rejects the retained invalid high when removing that history entry.
        Assert.Equal(error.Message, Assert.Throws<ArithmeticException>(() => state.Update(bar)).Message);
    }

    private static async Task CheckReference(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Gap continuation reference", bars, [expected], 0));
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
