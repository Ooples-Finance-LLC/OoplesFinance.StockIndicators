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
    [InlineData(2)] [InlineData(17)] [InlineData(20)] [InlineData(21)]
    [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void NativePartitionsAndBuilderColumnsAgree(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        foreach (string operation in new[] { "TaLib.Functions.Obv", "Skender.GetObv", "Trady.Indicator.OnBalanceVolume" })
        {
            var fixture = new SharedObvBenchmarks { Count = count, Operation = operation };
            try
            {
                CpuParallelSettings.MaxDegreeOfParallelism = 2;
                // Setup verifies every value and presence column against native
                // serial/parallel results and the unoptimized reference builder.
                fixture.Setup();
                fixture.Cleanup();
                fixture.Cleanup();
                Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
            }
            finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
        }
    }

    [Theory]
    [InlineData("TaLib.Functions.Obv")]
    [InlineData("Skender.GetObv")]
    [InlineData("Trady.Indicator.OnBalanceVolume")]
    public void AllocationProbeRetainsAllFourCpuArms(string operation)
    {
        var result = SharedAllocationProbe.Run(operation, 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, result.Arms.Select(x => x.Method));
        Assert.All(result.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }

    [Fact]
    public void GpuFixtureRetainsEagerResultsForTiming() =>
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedGpuObvBenchmarks))));
}
