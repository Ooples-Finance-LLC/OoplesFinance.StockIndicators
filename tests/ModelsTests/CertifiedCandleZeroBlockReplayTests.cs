using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CertifiedCandleZeroBlockReplayTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task SparseDensePositiveAndUnequalReplayWindowsMatchIndependentReference(int kind)
    {
        foreach (var (longPeriod, shortPeriod) in new[] { (1, 2), (10, 10), (64, 31), (31, 64) })
        foreach (bool dense in new[] { false, true })
        {
            var bars = CandleZeroBlockTestData.Data(8193, kind, dense);
            var owner = CandleZeroBlockTestData.Create(kind, longPeriod, shortPeriod);
            var original = (IIndicatorState)owner.CreateState();
            Assert.Contains(bars.Select(b => original.Update(b)), value => value != 0);
            await CandleZeroBlockTestData.Check(bars, owner, workers: dense ? 2 : 8);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task TotalLengthGuardAndUnsupportedPeriodsRetainOriginalRoutes(int kind)
    {
        foreach (int count in new[] { 0, 1, 31, 32, 33, 67, 68, 69, 1000, 8191, 8192, 8193 })
            await CandleZeroBlockTestData.Check(CandleZeroBlockTestData.Data(count, kind), CandleZeroBlockTestData.Create(kind, 64, 31), workers: 1);
        await CandleZeroBlockTestData.Check(CandleZeroBlockTestData.Data(8193, kind, dense: true), CandleZeroBlockTestData.Create(kind, 65, 10), workers: 2);
        if (kind == 0)
            await CandleZeroBlockTestData.Check(CandleZeroBlockTestData.Data(8193, kind), new MatHoldCandle(10, 65), workers: 2);
        await CandleZeroBlockTestData.Check(CandleZeroBlockTestData.Data(129, kind), CandleZeroBlockTestData.Create(kind, int.MaxValue - 4, 1), workers: 1);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task FiniteExtremesSignedZerosAndUnorderedBarsKeepExactThresholds(int kind)
    {
        var bars = CandleZeroBlockTestData.Data(8193, kind, dense: true);
        foreach (int row in new[] { 31, 32, 33, 4095, 4096, 4097, 8191 })
            bars[row] = new(bars[row].Time, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, row + 1);
        bars[127] = new(bars[127].Time, -0d, double.Epsilon, -double.Epsilon, 0d, 1);
        bars[128] = new(bars[128].Time, double.Epsilon, double.Epsilon, -double.Epsilon, -double.Epsilon, 1);
        bars[129] = new(bars[129].Time, 10, -20, 20, -10, 1);
        await CandleZeroBlockTestData.Check(bars, CandleZeroBlockTestData.Create(kind, 64, 31), workers: 2);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ZeroBlocksStillValidateAllFieldsAndRecoverFromCancellation(int kind)
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 5, 10, 0, 5, 1)).ToArray();
        var owner = CandleZeroBlockTestData.Create(kind);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                int[] positions = [0, 31, 32, 4096, 8192];
                for (int field = 0; field < positions.Length; field++)
                {
                    int row = positions[field];
                    var original = bars[row];
                    bars[row] = new(original.Time,
                        field == 0 ? double.NaN : original.Open,
                        field == 1 ? double.PositiveInfinity : original.High,
                        field == 2 ? double.NegativeInfinity : original.Low,
                        field == 3 ? double.NaN : original.Close,
                        field == 4 ? double.NaN : original.Volume);
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                    bars[row] = original;
                }
                using var run = await builder.BuildAsync();
                Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task MixedContinuationAndReversalConsumersKeepOriginalStates()
    {
        var bars = CandleZeroBlockTestData.Data(8193, 0, dense: true);
        IndicatorBase[] owners = [new MatHoldCandle(31, 64), new BreakawayCandle(64), new ConcealingBabySwallowCandle(), new LadderBottomCandle(), new RisingFallingThreeMethodsCandle(64, 31)];
        var expected = owners.Select(owner =>
        {
            var state = (IIndicatorState)owner.CreateState();
            return bars.Select(b => state.Update(b)).ToArray();
        }).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owners).BuildAsync();
        for (int i = 0; i < owners.Length; i++)
            Assert.Equal(expected[i].Select(BitConverter.DoubleToInt64Bits), run[owners[i]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    [Fact]
    public async Task PenetrationBoundariesKeepOriginalExactComparisons()
    {
        var bars = CandleZeroBlockTestData.Data(8193, 0, dense: true);
        foreach (double penetration in new[] { 0, double.Epsilon, .5, 1, double.MaxValue })
            await CandleZeroBlockTestData.Check(bars, new MatHoldCandle(10, 10, penetration), workers: 2);
    }
}
