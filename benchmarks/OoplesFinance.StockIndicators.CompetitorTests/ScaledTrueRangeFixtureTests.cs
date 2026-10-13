using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("OBV benchmark settings")]
public sealed class ScaledTrueRangeFixtureTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(127)] [InlineData(128)]
    [InlineData(129)] [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void EveryArmMatchesItsIndependentArithmeticAndRestoresSettings(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedScaledTrueRangeBenchmarks { Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AllocationProbeRetainsEagerCpuAndOptionalGpuArms(bool gpu)
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedScaledTrueRangeBenchmarks))));
        var report = SharedAllocationProbe.Run("QuanTAlib.Atr", 64, 1, includeGpu: gpu);
        Assert.Equal(gpu ? new[] { "BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel" }
            : new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }
}
