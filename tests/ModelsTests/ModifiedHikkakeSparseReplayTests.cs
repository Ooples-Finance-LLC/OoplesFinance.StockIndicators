using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ModifiedHikkakeSparseReplayTests
{
    private static Bar B(double high = 10, double low = 0, double close = 5) =>
        new(default, close, high, low, close, 1);

    private static Bar[] Quiet() => Enumerable.Repeat(B(), 1001).ToArray();

    private static void Formation(Bar[] bars, int row, int delay, double firstClose = 4)
    {
        bars[row - 2] = B(8, 2, firstClose);
        bars[row - 1] = B(7, 3, 5);
        bars[row] = B(6, 2, 4);
        for (int i = 1; i < delay; i++) bars[row + i] = B(7, 2, 7);
        bars[row + delay] = B(10, 2, 9);
    }

    private static Bar[] Mirror(Bar[] bars, int sign) => sign > 0 ? bars :
        bars.Select(b => new Bar(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume)).ToArray();

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task QuietGapsRetainWarmupAndStrictThreeBarConfirmation(int delay)
    {
        foreach (int sign in new[] { 1, -1 })
        {
            var bars = Quiet();
            Formation(bars, 7, delay);
            Formation(bars, 400, delay);
            bars = Mirror(bars, sign);
            var expected = HikkakeReference.Evaluate(bars, 5);
            Assert.Equal(sign * 100d, expected[400]);
            Assert.Equal(delay <= 3 ? sign * 200d : 0d, expected[400 + delay]);
            Assert.All(expected.Take(10), value => Assert.Equal(0d, value));
            if (delay == 3) Assert.Equal(sign * 200d, expected[10]);
            await Check(bars);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InclusiveNearBoundaryAndNextDoubleSurviveRingRestart(bool outside)
    {
        foreach (int sign in new[] { 1, -1 })
        {
            var bars = Quiet();
            // Five preceding ranges of ten give the exact inclusive distance two.
            Formation(bars, 400, 3, outside ? Math.BitIncrement(4d) : 4d);
            bars = Mirror(bars, sign);
            Assert.Equal(outside ? 0d : sign * 100d, HikkakeReference.Evaluate(bars, 5)[400]);
            await Check(bars);
        }
    }

    [Fact]
    public async Task GeometryNearMissStillConfirmsOriginalPendingDirection()
    {
        foreach (int sign in new[] { 1, -1 })
        {
            var bars = Quiet();
            const int row = 400;
            Formation(bars, row, 3);
            bars[row + 1] = B(5, 3, 3);
            bars[row + 2] = B(4.5, 3.5, 4);
            bars[row + 3] = B(9, 4, 8);
            bars = Mirror(bars, sign);
            // The opposite geometry fails its near condition. The original
            // bullish pending formation must confirm instead of being replaced.
            Assert.Equal(sign * 200d, HikkakeReference.Evaluate(bars, 5)[row + 3]);
            await Check(bars);
        }
    }

    [Fact]
    public async Task CapturedPrefixRetainsExtremeFiniteRangeAndSubnormalArithmetic()
    {
        foreach (int exponent in new[] { -1074, -1000, 0, 1000 })
        {
            var bars = Quiet();
            Formation(bars, 400, 3);
            Formation(bars, 700, 1);
            bars = bars.Select(b => new Bar(b.Time, Math.ScaleB(b.Open, exponent),
                Math.ScaleB(b.High, exponent), Math.ScaleB(b.Low, exponent),
                Math.ScaleB(b.Close, exponent), b.Volume)).ToArray();
            // This exact range exceeds binary64 but belongs to the replayed
            // near queue, and all five source fields remain finite.
            bars[395] = new(default, double.Epsilon, double.MaxValue, -double.MaxValue, -double.Epsilon, 1);
            await Check(bars);
        }
    }

    [Fact]
    public async Task QuietRowsStillValidateCancelAndRecover()
    {
        var bars = Quiet();
        Formation(bars, 700, 3);
        var indicator = new ModifiedHikkakeCandle();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        bars[333] = new(default, 5, 10, 0, 5, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[333] = B();
        await Check(bars);
    }

    private static async Task Check(Bar[] bars)
    {
        var indicator = new ModifiedHikkakeCandle();
        var expected = HikkakeReference.Evaluate(bars, 5).ToArray();
        var state = (IIndicatorState)indicator.CreateState();
        Assert.Equal(expected, bars.Select(b => state.Update(b)).ToArray());
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync();
                Array.Clear(input);
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (history != IndicatorHistoryMode.Full) continue;
                int index = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[index++], snapshot.Bar);
                Assert.Equal(bars.Length, index);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
