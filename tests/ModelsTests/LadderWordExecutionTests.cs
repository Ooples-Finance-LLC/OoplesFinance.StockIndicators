using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class LadderWordExecutionTests
{
    private static Bar[] Data(int count)
    {
        var bars = Enumerable.Range(0, count).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 105, 95, 99, i + 1)).ToArray();
        foreach (int end in Enumerable.Range(0, count / 257).Select(i => i * 257 + 256)
            .Concat(new[] { count / 2 - 1, count / 2 + 4, count - 1 }))
        {
            if (end < 4 || end >= count) continue;
            for (int j = 0; j < 4; j++)
            {
                int row = end - 4 + j;
                double open = 110 - 2 * j;
                bars[row] = new(bars[row].Time, open, j == 3 ? 110 : 105, 95, open - 2, row + 1);
            }
            bars[end] = new(bars[end].Time, 105, 112, 102, 111, end + 1);
        }
        return bars;
    }

    private static async Task<double[]> Check(Bar[] bars, int period, params int[] workers)
    {
        var owner = new LadderBottomCandle(period);
        // This reference independently sums exact rational ranges for each
        // threshold; it does not use either rolling implementation.
        var expected = ExtendedReversalReference.Evaluate(bars, period, ExtendedReversalKind.LadderBottom).ToArray();
        var original = (IIndicatorState)owner.CreateState();
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
            bars.Select(b => BitConverter.DoubleToInt64Bits(original.Update(in b))));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int degree in workers)
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = degree;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input);
                run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
        return expected;
    }

    [Theory]
    [InlineData(8191, 10)] [InlineData(8192, 1)] [InlineData(8192, 10)]
    [InlineData(8192, 64)] [InlineData(8192, 65)] [InlineData(10003, 64)]
    public async Task DispatchPeriodEdgesAndCapturedWorkerHalosMatchReference(int count, int period)
    {
        var expected = await Check(Data(count), period, 1, 2, 8);
        Assert.Contains(100d, expected);
    }

    [Theory]
    [InlineData(1)] [InlineData(64)]
    public async Task StrictThresholdAndAdjacentUlpsRemainExact(int period)
    {
        foreach (int direction in new[] { -1, 0, 1 })
        {
            var bars = Enumerable.Range(0, 8192).Select(i =>
                new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 105, 95, 99, 1)).ToArray();
            for (int j = 0; j < 4; j++)
            {
                int row = bars.Length - 5 + j;
                double high = j != 3 || direction == 0 ? 105
                    : direction > 0 ? Math.BitIncrement(105d) : Math.BitDecrement(105d);
                bars[row] = new(bars[row].Time, 110 - 2 * j, high, 95, 108 - 2 * j, 1);
            }
            bars[^1] = new(bars[^1].Time, 105, 110, 95, 106, 1);
            var expected = await Check(bars, period, 1, 2);
            Assert.Equal(direction > 0 ? 100d : 0d, expected[^1]);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(64)]
    public async Task FirstEligibleBarPublishesPositiveSignal(int period)
    {
        var bars = Data(8192);
        int end = period + 4;
        for (int j = 0; j < 4; j++)
        {
            int row = end - 4 + j;
            double open = 110 - 2 * j;
            bars[row] = new(bars[row].Time, open, j == 3 ? 110 : 105, 95, open - 2, 1);
        }
        bars[end] = new(bars[end].Time, 105, 112, 102, 111, 1);
        var expected = await Check(bars, period, 1, 2, 8);
        Assert.All(expected.Take(end), value => Assert.Equal(0d, value));
        Assert.Equal(100d, expected[end]);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task FiveBinadesStressSignedSumAndWideShadow(bool negativeRanges, bool negativeShadow)
    {
        double largest = Math.BitDecrement(1024d);
        double high = negativeRanges ? 32 : largest;
        double low = negativeRanges ? largest : 32;
        var bars = Enumerable.Range(0, 8192).Select(i =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), 128, high, low, 64, 1)).ToArray();
        const int end = 68;
        for (int j = 0; j < 3; j++)
        {
            int row = end - 4 + j;
            double open = 512d / (1 << j);
            bars[row] = new(bars[row].Time, open, high, low, open / 2, 1);
        }
        double dOpen = negativeShadow ? 512 : 64;
        double dHigh = negativeShadow ? 32 : 512;
        bars[end - 1] = new(bars[end - 1].Time, dOpen, dHigh, low, dOpen / 2, 1);
        bars[end] = new(bars[end].Time, negativeShadow ? 768 : 128, largest, low,
            negativeShadow ? 896 : 768, 1);

        // The first open fixes base exponent 1028: every coordinate stays
        // within exactly five positive normal binades, so fallback cannot hide
        // the word arithmetic under test. The prior P64 range sum is 96.875%
        // of Int64's magnitude limit and the shadow product exceeds that limit.
        int[] exponents = bars.SelectMany(b => new[] { b.Open, b.High, b.Low, b.Close })
            .Select(value => (int)(BitConverter.DoubleToUInt64Bits(value) >> 52)).Distinct().Order().ToArray();
        Assert.Equal(new[] { 1028, 1029, 1030, 1031, 1032 }, exponents);
        Int128 rangeMagnitude = ((Int128)1 << 57) - 16 - ((Int128)1 << 52);
        Int128 priorSum = (negativeRanges ? -rangeMagnitude : rangeMagnitude) * 64;
        Assert.True(Int128.Abs(priorSum) > (Int128)long.MaxValue * 95 / 100);
        Assert.True(Int128.Abs(priorSum) < long.MaxValue);
        Int128 weightedShadow = (Int128)(dHigh - dOpen) * ((Int128)1 << 47) * 640;
        Assert.True(Int128.Abs(weightedShadow) > long.MaxValue);
        Assert.Equal(!negativeShadow, weightedShadow > priorSum);

        var expected = await Check(bars, 64, 1, 2, 8);
        Assert.Equal(negativeShadow ? 0d : 100d, expected[end]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task FiniteOutOfGridCoordinatesReplayAndKeepLaterSignals(int kind)
    {
        double value = kind switch { 0 => 0, 1 => -100, 2 => double.Epsilon, 3 => double.MaxValue, _ => 4096 };
        // Include seeds, both sides of the worker seam and near-end fallback.
        // Separate runs ensure an earlier fallback cannot mask a later replay.
        foreach (int row in new[] { 0, 4095, 4096, 8000 })
        {
            var bars = Data(8192);
            var b = bars[row];
            bars[row] = kind == 4
                ? new(b.Time, b.Open, b.High, b.Low, value, b.Volume)
                : new(b.Time, b.Open, value, b.Low, b.Close, b.Volume);
            var expected = await Check(bars, 64, 1, 2, 8);
            Assert.Contains(100d, expected);
        }
    }

    [Fact]
    public async Task SignedRangesAndNegativeUpperShadowsMatchReference()
    {
        var bars = Data(8192);
        for (int row = 0; row < bars.Length; row += 7)
        {
            var b = bars[row];
            bars[row] = new(b.Time, b.Open, 90, 115, b.Close, -b.Volume);
        }
        await Check(bars, 1, 1, 2);
        await Check(bars, 64, 2, 8);
    }

    [Fact]
    public async Task AllFiveFieldsValidateAndCancelledOrInvalidBuildsRecover()
    {
        var bars = Data(8192);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var owner = new LadderBottomCandle(64);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                foreach (int row in new[] { 0, 4095, 4096, 8191 })
                for (int field = 0; field < 5; field++)
                {
                    var b = bars[row];
                    bars[row] = new(b.Time, field == 0 ? double.NaN : b.Open,
                        field == 1 ? double.PositiveInfinity : b.High,
                        field == 2 ? double.NegativeInfinity : b.Low,
                        field == 3 ? double.NaN : b.Close, field == 4 ? double.NaN : b.Volume);
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                    bars[row] = b;
                }
                using var run = await builder.BuildAsync();
                Assert.Equal(ExtendedReversalReference.Evaluate(bars, 64, ExtendedReversalKind.LadderBottom),
                    run[owner].ToArray());
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
