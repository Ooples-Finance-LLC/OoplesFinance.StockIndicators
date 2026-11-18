using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class BreakawayZeroBlockReplayTests
{
    private static Bar[] Data(int count, bool dense = false, bool mirror = false)
    {
        Bar[] pattern = [new(default, 20, 21, 9, 10, 1), new(default, 9, 10, 7, 8, 1), new(default, 8, 9, 6, 7.5, 1), new(default, 7, 8, 5, 6.5, 1), new(default, 8, 10, 7, 9.5, 1)];
        var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 10.2, 9.8, 9.9, i + 1)).ToArray();
        var targets = dense ? Enumerable.Range(0, count / 32).Select(i => i * 32 + 31)
            : new[] { 31, 64, 129, count / 8 + 1, count / 4 + 1, count / 2 + 1, count * 3 / 4 + 1, count - 1 };
        foreach (int end in targets)
            if (end >= 4 && end < count)
                for (int j = 0; j < pattern.Length; j++)
                {
                    int row = end - 4 + j;
                    var value = pattern[j];
                    bars[row] = new(bars[row].Time, value.Open, value.High, value.Low, value.Close, row + 1);
                }
        if (mirror)
            for (int i = 0; i < bars.Length; i++)
            {
                var b = bars[i];
                bars[i] = new(b.Time, -b.Open, -b.Low, -b.High, -b.Close, b.Volume);
            }
        return bars;
    }

    [Theory]
    [InlineData(1)] [InlineData(10)] [InlineData(64)]
    public async Task BothSignalDirectionsSparseReplayAndDenseEscapeMatchIndependentReference(int period)
    {
        foreach (bool dense in new[] { false, true })
        foreach (bool mirror in new[] { false, true })
        {
            var bars = Data(8193, dense, mirror);
            var owner = new BreakawayCandle(period);
            var state = (IIndicatorState)owner.CreateState();
            Assert.Contains(bars.Select(b => state.Update(b)), value => value == (mirror ? -100 : 100));
            await Check(bars, owner, workers: dense ? 2 : 8);
        }
    }

    [Fact]
    public async Task TotalSizeGuardSerialWorkersAndUnsupportedPeriodsRetainOriginalResults()
    {
        // Large eligible serial calls and small worker regions exercise both
        // gate outcomes. Eligibility uses total source length, not region size.
        foreach (int count in new[] { 0, 1, 31, 32, 33, 67, 68, 69, 1000, 8191, 8192, 8193 })
            await Check(Data(count), new BreakawayCandle(64), workers: 1);
        await Check(Data(8193, dense: true), new BreakawayCandle(65), workers: 2);
        await Check(Data(129), new BreakawayCandle(int.MaxValue - 4), workers: 1);
    }

    [Fact]
    public async Task FiniteExtremesSignedZerosAndUnorderedBarsKeepExactThresholds()
    {
        var bars = Data(8193, dense: true);
        foreach (int row in new[] { 31, 32, 33, 4095, 4096, 4097, 8191 })
            bars[row] = new(bars[row].Time, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, row + 1);
        bars[127] = new(bars[127].Time, -0d, double.Epsilon, -double.Epsilon, 0d, 1);
        bars[128] = new(bars[128].Time, double.Epsilon, double.Epsilon, -double.Epsilon, -double.Epsilon, 1);
        bars[129] = new(bars[129].Time, 10, -20, 20, -10, 1);
        await Check(bars, new BreakawayCandle(64), workers: 2);
    }

    [Fact]
    public async Task ProvenZeroBlocksStillValidateEveryFieldAndRecoverFromCancellation()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 5, 10, 0, 5, 1)).ToArray();
        var owner = new BreakawayCandle();
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
                    int row = positions[field];var original = bars[row];
                    bars[row] = new(original.Time, field == 0 ? double.NaN : original.Open,
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
    public async Task MixedReversalAndContinuationConsumersKeepTheirOriginalStates()
    {
        var bars = Data(8193, dense: true);
        IndicatorBase[] owners = [new BreakawayCandle(64), new ConcealingBabySwallowCandle(31), new LadderBottomCandle(1), new MatHoldCandle(31, 64), new ThreeStarsInSouthCandle(64, 31, 17), new RisingFallingThreeMethodsCandle(64, 31)];
        var expected = owners.Select(owner =>
        {
            var state = (IIndicatorState)owner.CreateState();return bars.Select(b => state.Update(b)).ToArray();
        }).ToArray();
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owners).BuildAsync();
        for (int i = 0; i < owners.Length; i++)
            Assert.Equal(expected[i].Select(BitConverter.DoubleToInt64Bits), run[owners[i]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }

    private static async Task Check(Bar[] bars, BreakawayCandle owner, int workers)
    {
        var original = (IIndicatorState)owner.CreateState();var expected = bars.Select(b => original.Update(b)).ToArray();
        foreach (var rule in owner.ValidationRules)
            rule.Check(new IndicatorValidationContext("Breakaway block independent reference", bars, [expected], 0));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = workers;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input);run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
