using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ThreeStarsZeroBlockReplayTests
{
    private static Bar[] Data(int count, bool dense = false)
    {
        Bar[] pattern =
        [
            new(default, 100, 101, 90, 96, 1),
            new(default, 97, 98, 92, 95, 1),
            new(default, 95.5, 95.6, 95.35, 95.4, 1)
        ];
        var bars = Enumerable.Range(0, count).Select(i =>
        {
            var value = dense && i >= 80 ? pattern[(i - 80) % 3] : new Bar(default, 100, 105, 95, 99, 1);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value.Open, value.High, value.Low, value.Close, i + 1);
        }).ToArray();
        if (!dense)
        {
            // Exact geometry straddles block edges and each possible worker tail.
            foreach (int end in new[] { 80, 95, 96, 97, 127, 128, 129, count / 3 - 1, count / 3,
                count / 2 - 1, count / 2, count / 2 + 1, count - 1 })
            {
                if (end < 2 || end >= count) continue;
                for (int j = 0; j < pattern.Length; j++)
                {
                    int row = end - 2 + j;
                    var value = pattern[j];
                    bars[row] = new(bars[row].Time, value.Open, value.High, value.Low, value.Close, row + 1);
                }
            }
        }
        return bars;
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(10, 10, 10)]
    [InlineData(64, 31, 17)]
    [InlineData(7, 64, 3)]
    [InlineData(1, 2, 64)]
    [InlineData(64, 64, 64)]
    public async Task PositiveSparseAndDenseBlocksMatchOriginalAndIndependentReference(int longPeriod, int shortPeriod, int shadowPeriod)
    {
        var owner = new ThreeStarsInSouthCandle(longPeriod, shortPeriod, shadowPeriod);
        foreach (bool dense in new[] { false, true })
        {
            var bars = Data(8193, dense);
            var state = (IIndicatorState)owner.CreateState();
            Assert.Contains(bars.Select(b => state.Update(b)), value => value == 100);
            await Check(bars, owner, workers: dense ? 2 : 3);
        }
    }

    [Fact]
    public async Task TotalSizeGuardWorkerTailsAndUnsupportedPeriodsKeepOriginalResults()
    {
        // Even with many workers each region is smaller than 8192: eligibility
        // depends on the total source, while each region replays its own halo.
        foreach (int count in new[] { 0, 1, 31, 32, 33, 65, 66, 67, 1000, 8191, 8192, 8193 })
            await Check(Data(count), new ThreeStarsInSouthCandle(64, 31, 17), workers: 8);
        await Check(Data(8193), new ThreeStarsInSouthCandle(), workers: 1);
        foreach (var owner in new[]
        {
            new ThreeStarsInSouthCandle(65, 10, 10),
            new ThreeStarsInSouthCandle(10, 65, 10),
            new ThreeStarsInSouthCandle(10, 10, 65)
        })
            await Check(Data(8193, dense: true), owner, workers: 2);
        await Check(Data(129), new ThreeStarsInSouthCandle(int.MaxValue - 2, 1, 1), workers: 1);
    }

    [Fact]
    public async Task WideFiniteSignedZeroAndUnorderedBarsRetainExactThresholds()
    {
        var bars = Data(8193, dense: true);
        foreach (int row in new[] { 31, 32, 33, 4095, 4096, 4097, 8191 })
            bars[row] = new(bars[row].Time, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, row + 1);
        bars[127] = new(bars[127].Time, -0d, double.Epsilon, -double.Epsilon, 0d, 1);
        bars[128] = new(bars[128].Time, double.Epsilon, double.Epsilon, -double.Epsilon, -double.Epsilon, 1);
        // Validation requires finite fields; it does not impose OHLC ordering.
        bars[129] = new(bars[129].Time, 10, -20, 20, -10, 1);
        await Check(bars, new ThreeStarsInSouthCandle(64, 31, 17), workers: 2);
    }

    [Fact]
    public async Task ProvenZeroBlocksStillValidateEveryFieldAndRecoverFromCancellation()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 5, 10, 0, 5, 1)).ToArray();
        var owner = new ThreeStarsInSouthCandle();
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
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
        await Check(bars, owner, workers: 2);
    }

    [Fact]
    public async Task MixedExhaustionConsumersMatchTheirOriginalStates()
    {
        var bars = Data(8193, dense: true);
        IndicatorBase[] owners = [new ThreeStarsInSouthCandle(), new AdvanceBlockCandle(), new StalledCandle()];
        var expected = owners.Select(owner =>
        {
            var state = (IIndicatorState)owner.CreateState();
            return bars.Select(b => state.Update(b)).ToArray();
        }).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owners).BuildAsync();
        for (int i = 0; i < owners.Length; i++)
            Assert.Equal(expected[i].Select(BitConverter.DoubleToInt64Bits), run[owners[i]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    private static async Task Check(Bar[] bars, ThreeStarsInSouthCandle owner, int workers)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in owner.ValidationRules)
            rule.Check(new IndicatorValidationContext("South block independent reference", bars, [expected], 0));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = workers;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input);
                run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
