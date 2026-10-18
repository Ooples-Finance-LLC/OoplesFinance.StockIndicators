using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("OBV benchmark settings")]
public sealed class SharedAdlDetailsFixtureTests
{
    [Theory]
    [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void AllColumnsMatchAcrossStartupAndNativePartitions(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedAdlDetailsBenchmarks { Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup();
            fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void AllOwnedResultArmsAreEagerAndAllocationProbeKeepsThem()
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedAdlDetailsBenchmarks))));
        var report = SharedAllocationProbe.Run("Skender.GetAdl", 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedGpuAdlDetailsBenchmarks))));
    }
}
