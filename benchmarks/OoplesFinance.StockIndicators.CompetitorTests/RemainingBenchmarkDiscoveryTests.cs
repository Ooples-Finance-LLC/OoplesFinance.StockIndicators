using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class RemainingBenchmarkDiscoveryTests
{
    [Theory]
    [InlineData(typeof(RemainingWilderBenchmarks), 24)]
    [InlineData(typeof(RemainingPriceAverageBenchmarks), 32)]
    [InlineData(typeof(RemainingExtrapolationBenchmarks), 16)]
    [InlineData(typeof(RemainingComposedAverageBenchmarks), 24)]
    [InlineData(typeof(RemainingStrengthRangeBenchmarks), 16)]
    [InlineData(typeof(RemainingRatioBenchmarks), 16)]
    [InlineData(typeof(RemainingOscillatorBenchmarks), 24)]
    [InlineData(typeof(RemainingEmaGapBenchmarks), 24)]
    [InlineData(typeof(RemainingAroonDpoBenchmarks), 16)]
    [InlineData(typeof(RemainingVolumeBenchmarks), 32)]
    [InlineData(typeof(RemainingVolumeTrendBenchmarks), 32)]
    [InlineData(typeof(RemainingDirectionalFlowBenchmarks), 24)]
    [InlineData(typeof(RemainingBarFlowBenchmarks), 32)]
    [InlineData(typeof(RemainingRangeHealthBenchmarks), 32)]
    [InlineData(typeof(RemainingAtrDerivedBenchmarks), 32)]
    [InlineData(typeof(RemainingExactStatisticsBenchmarks), 24)]
    [InlineData(typeof(RemainingPressureFlowBenchmarks), 24)]
    [InlineData(typeof(RemainingOrderedVolatilityBenchmarks), 16)]
    [InlineData(typeof(RemainingTrueStrengthBenchmarks), 32)]
    [InlineData(typeof(RemainingExpandedAverageBenchmarks), 32)]
    [InlineData(typeof(RemainingChainedAverageBenchmarks), 24)]
    [InlineData(typeof(RemainingOscillatorAliasBenchmarks), 32)]
    [InlineData(typeof(RemainingRecursiveExtraBenchmarks), 32)]
    [InlineData(typeof(RemainingVolumeAliasBenchmarks), 32)]
    [InlineData(typeof(RemainingRangeAliasBenchmarks), 32)]
    [InlineData(typeof(RemainingAccumulationBenchmarks), 32)]
    [InlineData(typeof(RemainingVolumeOscillatorBenchmarks), 32)]
    [InlineData(typeof(RemainingSpecialAverageBenchmarks), 32)]
    [InlineData(typeof(RemainingLaguerreBenchmarks), 16)]
    [InlineData(typeof(RemainingDoubleRecurrenceBenchmarks), 24)]
    [InlineData(typeof(RemainingWeightedExtraBenchmarks), 16)]
    [InlineData(typeof(RemainingAdaptiveLaguerreBenchmarks), 16)]
    public void SharedLifecycleRetainsEveryBenchmarkCase(Type type, int count)
    {
        var cases = BenchmarkConverter.TypeToBenchmarks(type).BenchmarksCases;
        Assert.Equal(count, cases.Length);
        Assert.Equal(new[] { "Legacy", "Shared" }, cases.Select(c => c.Descriptor.WorkloadMethod.Name).Distinct().Order());
        Assert.All(cases, benchmark =>
        {
            Assert.Equal("Setup", benchmark.Descriptor.GlobalSetupMethod?.Name);
            Assert.Equal(new[] { "Count", "History", "Operation" }, benchmark.Parameters.Items.Select(p => p.Name).Order());
            Assert.Equal(benchmark.Descriptor.WorkloadMethod.Name == "Legacy", benchmark.Descriptor.Baseline);
        });
    }
}
