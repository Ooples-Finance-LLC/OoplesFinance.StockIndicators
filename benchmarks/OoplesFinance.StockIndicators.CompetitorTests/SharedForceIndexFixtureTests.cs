using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("OBV benchmark settings")]
public sealed class SharedForceIndexFixtureTests
{
    [Theory]
    [InlineData(1)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void IndependentArithmeticReferencesAndNativeReplayAgree(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedForceIndexBenchmarks { Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void EagerNativeResultsAndAllAllocationArmsRemainAvailable()
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedForceIndexBenchmarks))));
        var result = SharedAllocationProbe.Run("Skender.GetForceIndex", 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, result.Arms.Select(a => a.Method));
        Assert.All(result.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedGpuForceIndexBenchmarks))));
        var gpu = SharedAllocationProbe.Run("Skender.GetForceIndex", 64, 1, includeGpu: true);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel" },
            gpu.Arms.Select(a => a.Method));
        Assert.All(gpu.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }

    [Theory]
    [InlineData(1)] [InlineData(13)] [InlineData(14)] [InlineData(15)]
    [InlineData(127)] [InlineData(128)] [InlineData(129)]
    [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void GpuColumnsMatchIndependentReferencesAcrossStartupAndBlocks(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedGpuForceIndexBenchmarks { Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}
