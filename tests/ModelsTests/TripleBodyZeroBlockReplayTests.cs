using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TripleBodyZeroBlockReplayTests
{
    private static IndicatorBase Create(int kind, int longPeriod, int shortPeriod) => kind switch
    {
        1 => new TwoCrowsCandle(longPeriod),
        2 => new UpsideGapTwoCrowsCandle(longPeriod, shortPeriod),
        _ => new UniqueThreeRiverCandle(longPeriod, shortPeriod)
    };

    private static Bar[] Pattern(int kind) => kind switch
    {
        1 => [new(default, 100, 121, 99, 120, 1), new(default, 130, 131, 124, 125, 1), new(default, 128, 129, 109, 110, 1)],
        2 => [new(default, 100, 121, 99, 120, 1), new(default, 131, 132, 129, 130, 1), new(default, 140, 141, 124, 125, 1)],
        _ => [new(default, 120, 121, 99, 100, 1), new(default, 119, 120, 90, 105, 1), new(default, 95, 97, 94, 96, 1)]
    };

    private static Bar[] Data(int count, int kind, bool dense = false)
    {
        var pattern = Pattern(kind);
        var bars = Enumerable.Range(0, count).Select(i =>
        {
            var value = dense ? pattern[i % 3] : new Bar(default, 100, 102, 98, 101, 1);
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), value.Open, value.High, value.Low, value.Close, i + 1);
        }).ToArray();
        if (!dense)
        {
            // Isolated geometry across zero runs, block edges, and worker tails.
            foreach (int target in new[] { 14, 31, 32, 33, 63, 64, 65, 127, 128, 129, count / 2 - 1, count / 2, count / 2 + 1, count - 1 })
                if (target >= 2 && target < count)
                    for (int j = 0; j < 3; j++)
                    {
                        int index = target - 2 + j;
                        var value = pattern[j];
                        bars[index] = new(bars[index].Time, value.Open, value.High, value.Low, value.Close, index + 1);
                    }
        }
        return bars;
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ZeroPositiveTransitionsAndUnequalHalosMatchOriginalAndIndependentReference(int kind)
    {
        foreach (var (longPeriod, shortPeriod) in new[] { (1, 2), (2, 1), (10, 10), (31, 64), (64, 31) })
        foreach (int count in new[] { 0, 1, 31, 32, 33, 127, 128, 129, 511, 512, 513, 1001 })
            await Check(Data(count, kind), Create(kind, longPeriod, shortPeriod));
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task DenseEscapeAndWideFiniteReplayRetainEverySignal(int kind)
    {
        foreach (var (longPeriod, shortPeriod) in new[] { (1, 2), (2, 1), (10, 10), (31, 64), (64, 31) })
        {
            var bars = Data(1025, kind, dense: true);
            var owner = Create(kind, longPeriod, shortPeriod);
            var original = (IIndicatorState)owner.CreateState();
            Assert.Contains(bars.Select(b => original.Update(b)), value => value != 0);
            await Check(bars, owner);
            foreach (int index in new[] { 31, 32, 33, 511, 512, 513, 1000 })
                bars[index] = new(bars[index].Time, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, index + 1);
            bars[255] = new(bars[255].Time, -0d, double.Epsilon, -double.Epsilon, 0d, 1);
            await Check(bars, owner);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task ZeroBlocksStillValidateAllFieldsAndRecoverAfterCancellation(int kind)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var bars = Enumerable.Repeat(new Bar(default, 5, 10, 0, 5, 1), 1025).ToArray();
            foreach (int workers in new[] { 1, 2, 8 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var owner = Create(kind, 10, 10);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                foreach (int index in new[] { 0, 31, 32, 33, 511, 512, 700, 1024 })
                {
                    var original = bars[index];
                    bars[index] = new(original.Time, original.Open, original.High, original.Low, original.Close, double.NaN);
                    await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                    bars[index] = original;
                }
                using var run = await builder.BuildAsync();
                Assert.All(run[owner].ToArray(), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task ThreeInsideAndPeriodsBeyondGuardKeepTheirOriginalContracts()
    {
        var bars = Data(129, 2, dense: true);
        await Check(bars, new ThreeInsideCandle(10, 10));
        foreach (int period in new[] { 65, int.MaxValue - 2 })
        foreach (int kind in new[] { 1, 2, 3 })
            await Check(bars, Create(kind, period, 1));
    }

    private static async Task Check(Bar[] bars, IndicatorBase owner)
    {
        var original = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => original.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Triple-body zero-block independent reference", bars, [expected], 0));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 2, 8 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input); run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
