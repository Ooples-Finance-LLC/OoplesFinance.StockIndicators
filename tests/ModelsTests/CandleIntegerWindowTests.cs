using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CandleIntegerWindowTests
{
    private static Bar[] FamilyData(int count, int kind, int exponent = 0) =>
        Enumerable.Range(0, count).Select(i =>
        {
            int phase = i % 16;
            double open = 4, high = 10, low = 0, close = 6;
            if (kind == 0 && phase >= 14) { open = 8; close = 4; }
            if (kind == 1)
            {
                if (phase is 13 or 15) { open = 8; close = 4; }
                if (phase == 14) { open = 6; close = 8; low = 5; }
            }
            if (kind == 2)
            {
                if (phase == 12) { open = 2; close = 4; }
                if (phase == 13) { open = 3; close = 6; }
                if (phase == 14) { open = 5; close = 10; high = 12; }
                if (phase == 15) { open = 11; close = 1; high = 12; }
                if (i / 16 % 2 != 0) (open, high, low, close) = (-open, -low, -high, -close);
            }
            if (kind >= 3)
            {
                if (phase == 14) { open = 8; close = 1; }
                if (phase == 15) { open = -2; close = kind == 3 ? 0 : kind == 4 ? 1 : 3; }
            }
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), Math.ScaleB(open, exponent),
                Math.ScaleB(high, exponent), Math.ScaleB(low, exponent), Math.ScaleB(close, exponent), i);
        }).ToArray();

    private static Bar[] Data(int count, bool neck, int exponent = 0) => FamilyData(count, neck ? 3 : 0, exponent);

    private static IndicatorBase FamilyOwner(int kind, int body = 10, int range = 5) => kind switch
    {
        0 => new MatchingLowCandle(range), 1 => new StickSandwichCandle(range),
        2 => new ThreeLineStrikeCandle(range), 3 => new OnNeckCandle(body, range),
        4 => new InNeckCandle(body, range), _ => new ThrustingCandle(body, range)
    };

    private static IndicatorBase Owner(bool neck, int body = 10, int range = 5) => FamilyOwner(neck ? 3 : 0, body, range);

    private static double[] Original(IndicatorBase owner, Bar[] bars)
    {
        var state = (IIndicatorState)owner.CreateState()!;
        return bars.Select(b => state.Update(in b)).ToArray();
    }

    [Theory]
    [InlineData(0, 0)] [InlineData(1, 0)] [InlineData(2, 0)] [InlineData(3, 0)] [InlineData(4, 0)] [InlineData(5, 0)]
    [InlineData(0, -1074)] [InlineData(1, -1074)] [InlineData(2, -1074)] [InlineData(3, -1074)] [InlineData(4, -1074)] [InlineData(5, -1074)]
    [InlineData(0, -1024)] [InlineData(1, -1024)] [InlineData(2, -1024)] [InlineData(3, -1024)] [InlineData(4, -1024)] [InlineData(5, -1024)]
    [InlineData(0, -1023)] [InlineData(1, -1023)] [InlineData(2, -1023)] [InlineData(3, -1023)] [InlineData(4, -1023)] [InlineData(5, -1023)]
    [InlineData(0, 990)] [InlineData(1, 990)] [InlineData(2, 990)] [InlineData(3, 990)] [InlineData(4, 990)] [InlineData(5, 990)]
    [InlineData(0, 1020)] [InlineData(1, 1020)] [InlineData(2, 1020)] [InlineData(3, 1020)] [InlineData(4, 1020)] [InlineData(5, 1020)]
    public void CertifiedBlocksMatchIndependentFractionReference(int kind, int exponent)
    {
        var bars = FamilyData(323, kind, exponent);
        var owner = FamilyOwner(kind);
        var expected = kind < 3
            ? SequenceCandleReference.Evaluate(bars, 5, owner.WarmupBars, (SequenceCandleKind)kind)
            : NeckCandleReference.Evaluate(bars, 10, 5, (NeckCandleKind)(kind - 3));
        double[] actual = new double[bars.Length];
        Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner,
            new long[5 * (bars.Length + 1)], actual));
        Assert.Equal(expected, actual);
        Assert.Contains(kind < 3 ? 100d : -100d, actual);
        if (kind == 2) Assert.Contains(-100d, actual);
        Assert.Equal(Original(owner, bars), actual);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task BlockAndWorkerBoundariesPreserveHistoryAndFallback(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 2, 8 })
            foreach (bool wide in new[] { false, true })
            foreach (int count in new[] { 511, 512, 513, 1023, 1024, 10000 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var bars = FamilyData(count, kind);
                if (wide)
                    foreach (int index in new[] { 0, 254, 255, 256, 511, count - 1 }.Where(i => i < count).Distinct())
                        bars[index] = new(bars[index].Time, double.Epsilon, double.MaxValue,
                            -double.MaxValue, -double.Epsilon, index);
                var owner = FamilyOwner(kind);
                var expected = Original(owner, bars);
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    var input = bars.ToArray();
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                        .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                    Array.Clear(input);
                    run.Dispose();
                    Assert.Equal(expected, run[owner].ToArray());
                    Assert.Equal(bars[^1], run.Latest.Bar);
                    if (mode == IndicatorHistoryMode.Full)
                    {
                        int i = 0;
                        await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                        Assert.Equal(bars.Length, i);
                    }
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task AsymmetricAndLargePeriodsRetainOriginalBehavior(int kind)
    {
        var periods = kind < 3
            ? new[] { (0, 1), (0, 5), (0, 63), (0, 64), (0, 65), (0, 999) }
            : new[] { (1, 1), (1, 64), (64, 1), (64, 64), (65, 65), (1000, 999) };
        foreach (var (body, range) in periods)
        {
            var bars = FamilyData(1001, kind);
            var owner = FamilyOwner(kind, body, range);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(owner).BuildAsync();
            Assert.Equal(Original(owner, bars), run[owner].ToArray());
        }
    }

    [Fact]
    public void ExactThresholdEqualityAndAdjacentValuesKeepStrictness()
    {
        foreach (bool neck in new[] { false, true })
        foreach (int step in new[] { -1, 0, 1 })
        {
            double boundary = neck ? 0.5 : 4.5;
            double close = step < 0 ? Math.BitDecrement(boundary) : step > 0 ? Math.BitIncrement(boundary) : boundary;
            Bar[] bars = [new(default, 4, 10, 0, 6, 0), new(default, 8, 10, 0, neck ? 5 : 4, 0),
                new(default, neck ? -2 : 8, 10, 0, close, 0)];
            var owner = Owner(neck, 1, 1);
            double[] output = new double[3];
            Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner, new long[20], output));
            Assert.Equal(step > 0 ? 0d : neck ? -100d : 100d, output[2]);
            Assert.Equal(Original(owner, bars), output);
            if (neck)
            {
                bars[1] = new(default, 8, 10, 0, 6, 0); // body exactly equals preceding mean
                Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner, new long[20], output));
                Assert.Equal(0d, output[2]);
            }
            bars[0] = new(default, 4, -1, 1, 6, 0); // finite inverted range retains its signed threshold
            Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner, new long[20], output));
            Assert.Equal(Original(owner, bars), output);
        }
    }

    [Fact]
    public void StickSandwichUsesTheMiddleCandlesPriorRangeWindow()
    {
        foreach (double close in new[] { Math.BitDecrement(4.5), 4.5, Math.BitIncrement(4.5) })
        {
            Bar[] bars = [new(default, 4, 10, 0, 6, 0), new(default, 8, 10, 0, 4, 0),
                new(default, 6, 10, 5, 8, 0), new(default, 8, 10, 0, close, 0)];
            var owner = new StickSandwichCandle(1);
            double[] output = new double[bars.Length];
            Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner, new long[25], output));
            Assert.Equal(close <= 4.5 ? 100d : 0d, output[3]);
            Assert.Equal(Original(owner, bars), output);
        }
    }

    [Fact]
    public void ThreeLineStrikeRetainsInclusiveBodyEdgesAndBothSigns()
    {
        foreach (bool reverse in new[] { false, true })
        foreach (int edge in new[] { 0, 1, 2 })
        {
            Bar[] bars = [new(default, 4, 10, 0, 6, 0), new(default, 2, 10, 0, 4, 0),
                new(default, edge == 1 ? Math.BitIncrement(6) : 6, 10, 0, 7, 0),
                new(default, edge == 2 ? Math.BitIncrement(9) : 9, 10, 0, 10, 0),
                new(default, 11, 12, 0, 1, 0)];
            if (reverse)
                bars = bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume)).ToArray();
            var owner = new ThreeLineStrikeCandle(1);
            double[] output = new double[bars.Length];
            Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner, new long[30], output));
            Assert.Equal(edge == 0 ? reverse ? -100d : 100d : 0d, output[4]);
            Assert.Equal(Original(owner, bars), output);
        }
    }

    [Fact]
    public void ThrustingKeepsStrictToleranceAndInclusiveMidpoint()
    {
        foreach (double close in new[] { Math.BitDecrement(1.5), 1.5, Math.BitIncrement(1.5), 4.5, Math.BitIncrement(4.5) })
        {
            Bar[] bars = [new(default, 4, 10, 0, 6, 0), new(default, 8, 10, 0, 1, 0),
                new(default, -2, 10, 0, close, 0)];
            var owner = new ThrustingCandle(1, 1);
            double[] output = new double[bars.Length];
            Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner, new long[20], output));
            Assert.Equal(close > 1.5 && close <= 4.5 ? -100d : 0d, output[2]);
            Assert.Equal(Original(owner, bars), output);
        }
    }

    [Fact]
    public void WideGridAndOverflowRejectBeforeWritingOutputs()
    {
        var bars = Data(321, false);
        bars[128] = new(default, double.Epsilon, double.MaxValue, -double.MaxValue, 1, 0);
        double[] output = Enumerable.Repeat(123d, bars.Length).ToArray();
        Assert.False(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, Owner(false),
            new long[5 * (bars.Length + 1)], output));
        Assert.All(output, value => Assert.Equal(123d, value));
        // All prices individually fit the grid, but their accumulated ranges do not.
        bars = Enumerable.Repeat(new Bar(default, 1, Math.ScaleB(1, 59), 0, 0, 0), 321).ToArray();
        Assert.False(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, Owner(false),
            new long[5 * (bars.Length + 1)], output));
        Assert.All(output, value => Assert.Equal(123d, value));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidLaterFieldAndCancellationKeepOriginalContract(bool neck)
    {
        var bars = Data(1001, neck);
        var owner = Owner(neck);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        var valid = bars[700];
        bars[700] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[700] = valid;
        using var run = await builder.BuildAsync();
        Assert.Equal(Original(owner, bars), run[owner].ToArray());
    }
}
