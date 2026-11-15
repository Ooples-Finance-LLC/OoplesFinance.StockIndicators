using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CandleIntegerWindowTests
{
    private static Bar[] Data(int count, bool neck, int exponent = 0)
    {
        var bars = Enumerable.Range(0, count).Select(i =>
        {
            int phase = i % 16;
            double open = 4, close = 6;
            if (neck && phase == 14) { open = 8; close = 1; }
            if (neck && phase == 15) { open = -2; close = 0; }
            if (!neck && phase >= 14) { open = 8; close = 4; }
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), Math.ScaleB(open, exponent),
                Math.ScaleB(10, exponent), 0, Math.ScaleB(close, exponent), i);
        }).ToArray();
        return bars;
    }

    private static IndicatorBase Owner(bool neck, int body = 10, int range = 5) =>
        neck ? new OnNeckCandle(body, range) : new MatchingLowCandle(range);

    private static double[] Original(IndicatorBase owner, Bar[] bars)
    {
        var state = (IIndicatorState)owner.CreateState()!;
        return bars.Select(b => state.Update(in b)).ToArray();
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, -1074)] [InlineData(true, -1074)]
    [InlineData(false, 990)] [InlineData(true, 990)]
    public void CertifiedBlocksMatchIndependentFractionReference(bool neck, int exponent)
    {
        var bars = Data(321, neck, exponent);
        var owner = Owner(neck);
        var expected = neck
            ? NeckCandleReference.Evaluate(bars, 10, 5, NeckCandleKind.OnNeck)
            : SequenceCandleReference.Evaluate(bars, 5, 6, SequenceCandleKind.MatchingLow);
        double[] actual = new double[bars.Length];
        Assert.True(ValuesBarExecution.TryCandleIntegerBlock(bars, 0, owner,
            new long[5 * (bars.Length + 1)], actual));
        Assert.Equal(expected, actual);
        Assert.Contains(neck ? -100d : 100d, actual);
        Assert.Equal(Original(owner, bars), actual);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BlockAndWorkerBoundariesPreserveHistoryAndFallback(bool neck)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 2, 8 })
            foreach (bool wide in new[] { false, true })
            foreach (int count in new[] { 513, 1001, 10000 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var bars = Data(count, neck);
                if (wide)
                    foreach (int index in new[] { 0, 254, 255, 256, 511, count - 1 }.Distinct())
                        bars[index] = new(bars[index].Time, double.Epsilon, double.MaxValue,
                            -double.MaxValue, -double.Epsilon, index);
                var owner = Owner(neck);
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
    [InlineData(false)] [InlineData(true)]
    public async Task AsymmetricAndLargePeriodsRetainOriginalBehavior(bool neck)
    {
        foreach (var (body, range) in new[] { (1, 1), (1, 64), (64, 1), (64, 64), (65, 65), (1000, 999) })
        {
            var bars = Data(1001, neck);
            var owner = Owner(neck, body, range);
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
