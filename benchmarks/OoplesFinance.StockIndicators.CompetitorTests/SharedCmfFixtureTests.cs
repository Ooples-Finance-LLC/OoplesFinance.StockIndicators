using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("OBV benchmark settings")]
public sealed class SharedCmfFixtureTests
{
    [Theory]
    [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(31)] [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void AllColumnsMatchReferencesAcrossStartupAndNativeHalos(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedCmfBenchmarks { Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void NativeResultsAreEagerAndAllFourAllocationArmsRemainAvailable()
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedCmfBenchmarks))));
        var report = SharedAllocationProbe.Run("Skender.GetCmf", 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }
}
