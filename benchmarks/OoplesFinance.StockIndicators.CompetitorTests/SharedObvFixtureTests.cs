using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[CollectionDefinition("OBV benchmark settings", DisableParallelization = true)]
public sealed class ObvBenchmarkSettingsCollection { }

[Collection("OBV benchmark settings")]
public sealed class SharedObvFixtureTests
{
    [Theory]
    [InlineData(2)] [InlineData(17)] [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void NativePartitionCorrectionsAndBuilderColumnsAgree(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedObvBenchmarks { Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup();
            Assert.Equal(fixture.NativeSingle(), fixture.NativeParallel());
            fixture.Cleanup();
            fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void AllocationProbeRetainsAllFourArmsAndRejectsUnsupportedGpu()
    {
        var result = SharedAllocationProbe.Run("TaLib.Functions.Obv", 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, result.Arms.Select(x => x.Method));
        Assert.All(result.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
        Assert.Throws<ArgumentException>(() => SharedAllocationProbe.Run("TaLib.Functions.Obv", 64, 1, includeGpu: true));
    }
}
