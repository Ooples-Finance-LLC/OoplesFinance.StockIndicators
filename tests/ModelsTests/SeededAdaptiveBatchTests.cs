using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SeededAdaptiveBatchTests
{
    private static Bar[] BarsOf(IEnumerable<double> prices) => prices.Select((p, i) =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i + 1, i - 1, p, i + 2)).ToArray();

    [Theory]
    [InlineData(false, false, true, false, 0L)]
    [InlineData(true, true, false, false, 0L)]
    [InlineData(false, false, false, true, 1L)]
    [InlineData(false, false, false, false, 0L)]
    public async Task AllFourOwnerColumnsMatchIndependentFractionsAcrossFullAndLatestHistory(
        bool startup, bool clamp, bool reset, bool fastFlat, long delay)
    {
        var bars = BarsOf(Enumerable.Range(0, 8193)
            .Select(i => i % 97 < 45 ? 100d : 100 + (i * 13 % 101) / 8d));
        await CheckReference(bars, new(20, 2, 30, startup, clamp, reset, fastFlat, delay), true);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public async Task ZeroEfficiencyFromOscillationDoesNotBecomeAFlatWindow(bool reset, bool fastFlat)
    {
        double[] pattern = [0, 10, 0, 0, 0, 0, 0, 0];
        var bars = BarsOf(Enumerable.Range(0, 8192).Select(i => pattern[i % pattern.Length]));
        var indicator = new SeededAdaptiveAverage(2, resetFlat: reset, fastFlat: fastFlat);
        await CheckReference(bars, indicator);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(indicator).BuildAsync();
        var average = run[indicator.Average].ToArray();
        var efficiency = run[indicator.Efficiency].ToArray();
        Assert.Equal(0, efficiency[2]); // Endpoints equal, but two nonzero changes.
        Assert.NotEqual(0, average[2]);
        Assert.Equal(fastFlat ? 1d : 0d, efficiency[4]); // Truly flat at rows2..4.
        if (reset) Assert.Equal(0, average[4]);
    }

    [Theory]
    [InlineData(-1074)] [InlineData(970)]
    public async Task SubnormalAndPositiveIntegerGridsKeepEveryOutputBit(int grid)
    {
        var bars = BarsOf(Enumerable.Range(0, 8193).Select(i => Math.ScaleB(i % 13 - 6d, grid)));
        await CheckReference(bars, new(3, 30, 2, true, true, false, true));
    }

    [Theory]
    [InlineData(16777216)] [InlineData(16777217)]
    public async Task InclusiveFloatPrefixLimitAndItsFallbackKeepExactRounding(int totalChange)
    {
        var prices = Enumerable.Repeat(1d, 8192).ToArray();
        // Odd first price keeps the common grid at zero, so these really
        // exercise total=2^24 and total=2^24+1 after grid normalization.
        prices[^1] += totalChange;
        await CheckReference(BarsOf(prices), new(20));
    }

    [Fact]
    public async Task ExtremeDifferencesAndWideExponentSpansUseOriginalFallback()
    {
        double[] prices = [double.MaxValue, -double.MaxValue, double.MaxValue / 2,
            double.Epsilon, -double.Epsilon, 0d, -0d, 1d];
        var bars = BarsOf(Enumerable.Range(0, 8193).Select(i => prices[i % prices.Length]));
        await CheckReference(bars, new(3, 1, int.MaxValue, true, false, false, true));
    }

    [Fact]
    public async Task PeriodOneDelayAndCanonicalZerosRetainBothPresenceColumns()
    {
        var bars = BarsOf(Enumerable.Range(0, 8193).Select(i => i % 2 == 0 ? -0d : 0d));
        await CheckReference(bars, new(1, 2, 30, true, true, false, true, 7));
        await CheckReference(bars, new(3, 2, 30, false, false, true, false, long.MaxValue - 2));
    }

    [Fact]
    public async Task WholeInputBoundaryAndLargePeriodsRetainOriginalSemantics()
    {
        foreach (int count in new[] { 8191, 8192, 8193 })
            await CheckReference(BarsOf(Enumerable.Range(0, count).Select(i => (i % 31 - 15) / 8d)),
                new(20));
        await CheckReference(BarsOf(Enumerable.Repeat(1d, 8193)), new(int.MaxValue, publishStartup: true));
    }

    [Fact]
    public async Task DuplicateRootsAndMixedGraphsKeepTheirOriginalStates()
    {
        var bars = BarsOf(Enumerable.Range(0, 8193).Select(i => 100 + (i % 31 - 15) / 8d));
        var first = new SeededAdaptiveAverage(3);
        var second = new SeededAdaptiveAverage(7, 1, 30, true, true, false, true);
        var expected = SeededAdaptiveReference.Values(bars, first);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(first, second, first).BuildAsync();
        for (int slot = 0; slot < 4; slot++) AssertBits(Expected(expected, slot), run[first.Outputs[slot]].ToArray());
    }

    [Fact]
    public async Task SingleEnumerationValidationCancellationAndRequiredGpuRecover()
    {
        var bars = BarsOf(Enumerable.Repeat(1d, 8193));
        var indicator = new SeededAdaptiveAverage(3);
        int reads = 0;
        IEnumerable<Bar> Source()
        {
            foreach (var bar in bars) { reads++; yield return bar; }
        }
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(Source())).ConfigureIndicators(indicator);
        using (var run = await builder.BuildAsync()) Assert.Equal(bars.Length, reads);
        var final = bars[^1];
        bars[^1] = new(final.Time, final.Open, final.High, final.Low, final.Close, double.NaN);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
        bars[^1] = final;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        using (var run = await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync())
        {
            Assert.Equal(IndicatorExecutionBackend.Cpu, builder.LastExecution!.Backend);
            Assert.Equal(1, run[indicator.Average].ToArray()[^1]);
        }
        await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
    }

    private static async Task CheckReference(Bar[] bars, SeededAdaptiveAverage indicator, bool checkOwnedHistory = false)
    {
        var reference = SeededAdaptiveReference.Values(bars, indicator);
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input);
            for (int slot = 0; slot < 4; slot++) AssertBits(Expected(reference, slot), run[indicator.Outputs[slot]].ToArray());
            Assert.Equal(bars[^1], run.Latest.Bar);
            if (checkOwnedHistory && mode == IndicatorHistoryMode.Full)
            {
                int row = 0;
                await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                Assert.Equal(bars.Length, row);
            }
        }
    }

    private static double[] Expected(double?[][] reference, int slot) => reference[slot % 2]
        .Select(v => slot < 2 ? v ?? 0 : v.HasValue ? 1d : 0d).ToArray();
    private static void AssertBits(double[] expected, double[] actual) =>
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
}
