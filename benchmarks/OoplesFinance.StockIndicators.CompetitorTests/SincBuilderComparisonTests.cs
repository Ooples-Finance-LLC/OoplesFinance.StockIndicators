using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SincBuilderComparisonTests
{
    [Theory]
    [InlineData(SincWindow.Rectangular)]
    [InlineData(SincWindow.Hann)]
    [InlineData(SincWindow.Hamming)]
    [InlineData(SincWindow.Blackman)]
    [InlineData(SincWindow.BlackmanHarris)]
    public async Task BuilderMatchesIndependentStagedTrigonometricReference(SincWindow window)
    {
        double[] prices = [-0d, double.Epsilon, -double.Epsilon, double.MaxValue / 16, -double.MaxValue / 16, 1, -.125];
        var values = Enumerable.Range(0, 65).Select(i => prices[i % prices.Length]).ToArray();
        var bars = values.Select(v => new Bar(default, 0, 1, 0, v, 1)).ToArray();
        foreach (int taps in new[] { window == SincWindow.Rectangular ? 1 : window == SincWindow.Hann ? 3 : 2, 7, 20, 21 })
        foreach (int period in new[] { 3, 14 })
        {
            var expected = FixedKernelSnapshotComparison.Reference(values, period, false, taps, window, false);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new WindowedSinc(period, taps, window);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
                AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
            }
        }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public void BenchmarkChecksBuilderAndBothNativePaths(int count)
    {
        var benchmark = new SharedWindowedSincBenchmarks { Count = count, Operation = "QuanTAlib.Afirma" };
        try { benchmark.Setup(); }
        finally { benchmark.Cleanup(); }
    }
}
