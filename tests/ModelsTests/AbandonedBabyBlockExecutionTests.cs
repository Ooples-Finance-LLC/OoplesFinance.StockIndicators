using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AbandonedBabyBlockExecutionTests
{
    private static Bar[] Data(int count, bool dense = false, bool stressed = false)
    {
        int[] starts = [12, 62, 125, 254, 509, 766, 1021, 2046, 3070, 4093];
        return Enumerable.Range(0, count).Select(i =>
        {
            double open = 4, high = 10, low = 0, close = 6;
            int part = -1, flip = 0;
            if (dense) { part = i % 3; flip = i / 3 % 2; }
            else
                for (int k = 0; k < starts.Length; k++)
                    if (i >= starts[k] && i < starts[k] + 3)
                    { part = i - starts[k]; flip = k % 2; break; }
            if (part == 0) (open, high, low, close) = (8, 9, 1, 2);
            if (part == 1) (open, high, low, close) = (0, .5, -.5, 0);
            if (part == 2) (open, high, low, close) = (1.5, 7, 1, 6);
            if (flip != 0) (open, high, low, close) = (-open, -low, -high, -close);
            if (stressed)
            {
                if (i == 29) (open, high, low, close) = (1, 2, -1, double.Epsilon);
                if (i == 30) (open, high, low, close) = (-double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue);
                if (i == 31) (open, high, low, close) = (double.Epsilon, 2 * double.Epsilon, -double.Epsilon, -double.Epsilon);
                if (i == 32) (open, high, low, close) = (-0d, 0, -0d, 0);
                if (i == 33) (open, high, low, close) = (1, -1, 1, -1);
            }
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), open, high, low, close, i + 1);
        }).ToArray();
    }

    [Theory]
    [InlineData(1, 3, 5)]
    [InlineData(3, 1, 7)]
    [InlineData(10, 10, 10)]
    [InlineData(64, 32, 16)]
    [InlineData(65, 32, 16)]
    public async Task SparseReplayDenseFallbackAndRegionEdgesMatchIndependentReference(int longPeriod, int shortPeriod, int dojiPeriod)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int count in new[] { 1024, 4097 })
            foreach (var (dense, stressed) in new[] { (false, false), (false, true), (true, false) })
            {
                var bars = Data(count, dense, stressed);
                var owner = new AbandonedBabyCandle(longPeriod, shortPeriod, dojiPeriod);
                var expected = Reference(owner, bars);
                Assert.Contains(100d, expected.Skip(128));
                Assert.Contains(-100d, expected.Skip(128));
                foreach (int workers in new[] { 1, 4, 8 })
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
    public async Task StartupAndUnboundedPeriodsRetainOriginalFallback()
    {
        foreach (int count in new[] { 0, 1, 2, 3, 12, 13, 14, 63, 64, 65, 127, 128, 129 })
        foreach (int period in new[] { 1, 64, 65, int.MaxValue - 2 })
        {
            var owner = new AbandonedBabyCandle(period, 3, 5);
            var bars = Data(count, stressed: true);
            var expected = Reference(owner, bars);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                await CheckOwned(owner, bars, expected, mode);
        }
    }

    [Fact]
    public async Task CertifiedZeroBlocksStillRejectInvalidVolumeAndAllowValidRebuild()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 4 })
            foreach (int invalidIndex in new[] { 63, 128, 255, 512, 1000 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var bars = Data(1024);
                var owner = new AbandonedBabyCandle();
                var expected = Reference(owner, bars);
                var original = bars[invalidIndex];
                bars[invalidIndex] = new(original.Time, original.Open, original.High, original.Low, original.Close, double.NaN);
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                bars[invalidIndex] = original;
                using var run = await builder.BuildAsync();
                Assert.Equal(expected, run[owner].ToArray());
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task OtherStarKindsAndCrowControlRemainCorrectInMixedRoots()
    {
        var bars = Data(1024);
        IndicatorBase[] roots = [new MorningStarCandle(), new EveningStarCandle(), new MorningDojiStarCandle(),
            new EveningDojiStarCandle(), new AbandonedBabyCandle(), new ThreeBlackCrowsCandle()];
        var expected = roots.Select(owner => Reference(owner, bars)).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(roots.Concat([roots[0]]).ToArray()).ConfigureHistory(mode).BuildAsync();
            for (int i = 0; i < roots.Length; i++) Assert.Equal(expected[i], run[roots[i]].ToArray());
        }
    }

    [Fact]
    public async Task CanceledReuseDoesNotChangePreviouslyOwnedSparseResults()
    {
        var bars = Data(4097, stressed: true);
        var owner = new AbandonedBabyCandle();
        var expected = Reference(owner, bars);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using var run = await builder.BuildAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        Assert.Equal(expected, run[owner].ToArray());
    }

    private static double[] Reference(IndicatorBase owner, Bar[] bars)
    {
        var state = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(bar => state.Update(bar)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Abandoned block independent reference", bars, [expected], 0));
        return expected;
    }

    private static async Task CheckOwned(IndicatorBase owner, Bar[] bars, double[] expected, IndicatorHistoryMode mode)
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
