using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SectorRotationBatchBenchmarkTests
{
    [Theory]
    [InlineData(1_000)]
    [InlineData(10_000)]
    public void FullPairedBatchRetainsLegacyBitsAndSignals(int count)
    {
        var fixture = new SectorRotationBatchBenchmarks { Count = count };
        fixture.Setup();
        Assert.Equal(count, fixture.Shared()["Srm"].Count);
    }
    [Fact]
    public void AllFullBatchTimingConfigurationsAreDiscoverable()
    {
        var cases = BenchmarkConverter.TypeToBenchmarks(typeof(SectorRotationBatchBenchmarks)).BenchmarksCases;
        Assert.Equal(4, cases.Length);
        Assert.Equal(new[] { "Legacy", "Shared" }, cases.Select(c => c.Descriptor.WorkloadMethod.Name).Distinct().Order());
        Assert.All(cases, benchmark =>
        {
            Assert.Equal("Setup", benchmark.Descriptor.GlobalSetupMethod?.Name);
            Assert.Equal(new[] { "Count" }, benchmark.Parameters.Items.Select(p => p.Name));
            Assert.Equal(benchmark.Descriptor.WorkloadMethod.Name == "Legacy", benchmark.Descriptor.Baseline);
        });
    }
}
