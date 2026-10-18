using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class FibonacciBuilderComparisonTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(20)]
    [InlineData(1500)]
    public async Task BuilderMatchesIndependentDoublingIdentityReference(int period)
    {
        var prices = new[] { -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, 1d, -.125 };
        var values = Enumerable.Range(0, period == 1500 ? 21 : 65).Select(i => prices[i % prices.Length]).ToArray();
        var bars = values.Select(v => new Bar(default, 0, 1, 0, v, 1)).ToArray();
        var expected = FixedKernelSnapshotComparison.Reference(values, period, true, 21, SincWindow.Hann, false);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new FibonacciWeightedAverage(period);
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(mode).BuildAsync();
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public void BenchmarkChecksBuilderAndBothNativePaths(int count)
    {
        var benchmark = new SharedFibonacciWeightedBenchmarks { Count = count, Operation = "QuanTAlib.Fwma" };
        try { benchmark.Setup(); }
        finally { benchmark.Cleanup(); }
    }
}
