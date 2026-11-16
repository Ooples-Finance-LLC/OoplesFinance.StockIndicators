using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ThreeStarsBlockExecutionTests
{
    private static Bar[] Data(int count, int pattern = 0)
    {
        int[] starts = [12, 62, 126, 254, 509, 766, 1021, 2046, 3070, 4093, 8190];
        var random = new Random(313717);
        return Enumerable.Range(0, count).Select(i =>
        {
            double open = 10, high = 20, low = 0, close = 9;
            foreach (int start in starts)
            {
                if (i == start) (open, high, low, close) = (15, 16, 5, 11);
                if (i == start + 1) (open, high, low, close) = (12, 13, 6, 9);
                if (i == start + 2) (open, high, low, close) = (10, 10.1, 9.4, 9.5);
            }
            if ((pattern == 1 && i < 512) || (pattern == 2 && i >= 512))
                (open, high, low, close) = (10, 20 - i / 2048d, i / 2048d, 9);
            if (pattern == 3)
            {
                // Finite inverted OHLC is accepted; never assume candle ordering.
                if (i % 7 == 0)
                    (open, high, low, close) = (random.Next(-100, 101) / 8d, random.Next(-100, 101) / 8d,
                        random.Next(-100, 101) / 8d, random.Next(-100, 101) / 8d);
                if (i % 257 == 29) (open, high, low, close) = (double.MaxValue, -double.MaxValue, double.Epsilon, -0d);
                if (i % 257 == 30) (open, high, low, close) = (-double.MaxValue, double.MaxValue, -double.MaxValue, double.Epsilon);
                if (i % 257 == 31) (open, high, low, close) = (double.Epsilon, 2 * double.Epsilon, -double.Epsilon, -double.Epsilon);
                if (i % 257 == 32) (open, high, low, close) = (-0d, 0, -0d, 0);
            }
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), open, high, low, close, i + 1);
        }).ToArray();
    }

    private static double[] Reference(ThreeStarsInSouthCandle owner, Bar[] bars)
    {
        var expected = ExhaustionCandleReference.Evaluate(bars, ExhaustionCandleKind.ThreeStarsInSouth,
            owner.LongPeriod, owner.ShortPeriod, owner.ShadowPeriod, 0, 0).ToArray();
        var state = (IIndicatorState)owner.CreateState()!;
        Assert.Equal(expected, bars.Select(bar => state.Update(in bar)).ToArray());
        return expected;
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(2, 5, 3)]
    [InlineData(10, 10, 10)]
    [InlineData(64, 17, 31)]
    public async Task SparseReplayDenseEscapeAndWorkerSeamsMatchIndependentReference(int longPeriod, int shortPeriod, int shadowPeriod)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int pattern in new[] { 0, 1, 2, 3 })
            {
                var owner = new ThreeStarsInSouthCandle(longPeriod, shortPeriod, shadowPeriod);
                var bars = Data(8193, pattern);
                var expected = Reference(owner, bars);
                if (pattern == 0) Assert.Contains(100d, expected.Skip(128));
                foreach (int workers in new[] { 1, 2, 8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CheckOwned(owner, bars, expected, mode);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task StartupSizeGuardAndLargePeriodsRetainOriginalResults()
    {
        foreach (int count in new[] { 0, 1, 2, 12, 63, 64, 65, 1000, 8191, 8192, 8193, 10000 })
        {
            var owner = new ThreeStarsInSouthCandle();
            var bars = Data(count);
            var expected = Reference(owner, bars);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                await CheckOwned(owner, bars, expected, mode);
        }
        foreach (var owner in new[] { new ThreeStarsInSouthCandle(65, 1, 1),
            new ThreeStarsInSouthCandle(1, 65, 1), new ThreeStarsInSouthCandle(1, 1, 65),
            new ThreeStarsInSouthCandle(int.MaxValue - 2, 1, 1) })
        {
            var bars = Data(8193);
            var expected = Reference(owner, bars);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                await CheckOwned(owner, bars, expected, mode);
        }
    }

    [Theory]
    [InlineData(-1000)] [InlineData(-1074)] [InlineData(900)]
    public async Task ScaledPositivePatternsRetainExactOriginalArithmetic(int exponent)
    {
        var bars = Data(8193).Select(bar => new Bar(bar.Time, Math.ScaleB(bar.Open, exponent),
            Math.ScaleB(bar.High, exponent), Math.ScaleB(bar.Low, exponent), Math.ScaleB(bar.Close, exponent), bar.Volume)).ToArray();
        var owner = new ThreeStarsInSouthCandle();
        var expected = Reference(owner, bars);
        if (exponent != -1074) Assert.Contains(100d, expected);
        await CheckOwned(owner, bars, expected, IndicatorHistoryMode.Full);
    }

    [Fact]
    public async Task NecessaryPredicateBoundariesAndExactThresholdTiesRemainUnchanged()
    {
        var bars = Data(8193);
        int sequence = 0;
        // Mutations include equality and either adjacent value, so strict and
        // inclusive raw predicates are covered without reimplementing them.
        foreach (int edge in Enumerable.Range(0, 9))
        foreach (int side in new[] { -1, 0, 1 })
        {
            int start = 128 + sequence++ * 256;
            double Adjacent(double value) => side < 0 ? Math.BitDecrement(value)
                : side > 0 ? Math.BitIncrement(value) : value;
            Bar a = new(default, 15, 16, 5, 11, 1);
            Bar b = new(default, 12, 13, 6, 9, 1);
            Bar c = new(default, 10, 10.1, 9.4, 9.5, 1);
            if (edge == 0) a = new(default, 15, 16, 5, Adjacent(15), 1);
            if (edge == 1) b = new(default, 12, 13, 6, Adjacent(12), 1);
            if (edge == 2) c = new(default, 10, 10.1, 9.4, Adjacent(10), 1);
            if (edge == 3) b = new(default, Adjacent(a.Close), 13, 6, 9, 1);
            if (edge == 4) b = new(default, Adjacent(a.High), 13, 6, 9, 1);
            if (edge == 5) b = new(default, 12, 13, Adjacent(a.Close), 9, 1);
            if (edge == 6) b = new(default, 12, 13, Adjacent(a.Low), 9, 1);
            if (edge == 7) c = new(default, 10, 10.1, Adjacent(b.Low), 9.5, 1);
            if (edge == 8) c = new(default, 10, Adjacent(b.High), 9.4, 9.5, 1);
            bars[start] = a; bars[start + 1] = b; bars[start + 2] = c;
        }
        // With unit periods, the third body equals the preceding body exactly;
        // its strict short-body condition must reject the tie.
        bars[8062] = new(default, 10, 11, 0, 9, 1);
        bars[8063] = new(default, 15, 16, 5, 11, 1);
        bars[8064] = new(default, 12, 13, 6, 9, 1);
        bars[8065] = new(default, 12, 12.1, 8.9, 9, 1);
        var owner = new ThreeStarsInSouthCandle(1, 1, 1);
        var expected = Reference(owner, bars);
        Assert.Equal(0d, expected[8065]);
        await CheckOwned(owner, bars, expected, IndicatorHistoryMode.Full);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task ZeroBlocksSeedsAndTailsStillRejectEveryNonfiniteField(int field)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (int invalidIndex in new[] { 0, 63, 64, 1023, 1024, 8191, 8192 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 2, 0, 1, i)).ToArray();
                var owner = new ThreeStarsInSouthCandle();
                var original = bars[invalidIndex];
                double[] values = [original.Open, original.High, original.Low, original.Close, original.Volume];
                values[field] = field % 2 == 0 ? double.NaN : double.PositiveInfinity;
                bars[invalidIndex] = new(original.Time, values[0], values[1], values[2], values[3], values[4]);
                var scalarError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                    new StockIndicatorBuilder().ConfigureSource(Bars.From(bars, bar => bar)).ConfigureIndicators(owner).BuildAsync());
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
                var actualError = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                Assert.Equal(scalarError.Message, actualError.Message);
                bars[invalidIndex] = original;
                using var run = await builder.BuildAsync();
                Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task SignedZeroHistoryAndCancellationRetainPreviouslyOwnedResults()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            i % 2 == 0 ? -0d : 0d, 0, -0d, 0, -0d)).ToArray();
        var owner = new ThreeStarsInSouthCandle();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.BuildAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
        int row = 0;
        await foreach (var snapshot in run)
        {
            Assert.Equal(BitConverter.DoubleToInt64Bits(bars[row].Open), BitConverter.DoubleToInt64Bits(snapshot.Bar.Open));
            Assert.Equal(BitConverter.DoubleToInt64Bits(bars[row].Low), BitConverter.DoubleToInt64Bits(snapshot.Bar.Low));
            Assert.Equal(BitConverter.DoubleToInt64Bits(bars[row].Volume), BitConverter.DoubleToInt64Bits(snapshot.Bar.Volume));
            row++;
        }
        Assert.Equal(bars.Length, row);
    }

    [Fact]
    public async Task SharedExhaustionConsumersRemainCorrectInMixedRoots()
    {
        var bars = Data(8193, 3);
        IndicatorBase[] roots = [new ThreeStarsInSouthCandle(), new StalledCandle(), new AdvanceBlockCandle()];
        var expected = roots.Select(owner => ExhaustionCandleReference.Evaluate(bars,
            owner is ThreeStarsInSouthCandle ? ExhaustionCandleKind.ThreeStarsInSouth
                : owner is StalledCandle ? ExhaustionCandleKind.Stalled : ExhaustionCandleKind.AdvanceBlock,
            10, owner is AdvanceBlockCandle ? 0 : 10, 10, owner is ThreeStarsInSouthCandle ? 0 : 5,
            owner is AdvanceBlockCandle ? 5 : 0).ToArray()).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(roots.Concat([roots[0]]).ToArray()).BuildAsync();
        for (int i = 0; i < roots.Length; i++) Assert.Equal(expected[i], run[roots[i]].ToArray());
    }

    private static async Task CheckOwned(ThreeStarsInSouthCandle owner, Bar[] bars, double[] expected, IndicatorHistoryMode mode)
    {
        var input = bars.ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
            .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
        Array.Clear(input);
        run.Dispose();
        Assert.Equal(expected, run[owner].ToArray());
        if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
        if (mode == IndicatorHistoryMode.Full)
        {
            int row = 0;
            await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
    }
}
