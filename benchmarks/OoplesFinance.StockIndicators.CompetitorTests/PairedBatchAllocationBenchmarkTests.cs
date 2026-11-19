using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class PairedBatchAllocationBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "ComparePriceMomentumOscillator", "RelativeNormalizedVolatility", "RelativeStrength3DIndicator", "SectorRotationModel" }
        from count in new[] { 1_000, 10_000 } select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void PreservedLoopAndCandidateKeepEveryOutputBit(string operation, int count)
        => new PairedBatchAllocationBenchmarks { Operation = operation, Count = count }.Setup();

    [Fact]
    public void AllFullBatchTimingConfigurationsAreDiscoverable()
    {
        var cases = BenchmarkConverter.TypeToBenchmarks(typeof(PairedBatchAllocationBenchmarks)).BenchmarksCases;
        Assert.Equal(16, cases.Length);
        Assert.Equal(new[] { "Legacy", "Shared" }, cases.Select(c => c.Descriptor.WorkloadMethod.Name).Distinct().Order());
        Assert.All(cases, benchmark =>
        {
            Assert.Equal("Setup", benchmark.Descriptor.GlobalSetupMethod?.Name);
            Assert.Equal(new[] { "Count", "Operation" }, benchmark.Parameters.Items.Select(p => p.Name).Order());
            Assert.Equal(benchmark.Descriptor.WorkloadMethod.Name == "Legacy", benchmark.Descriptor.Baseline);
        });
    }
}
