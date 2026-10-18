using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedPivotBenchmarkTests
{
    [Theory]
    [InlineData(1)] [InlineData(20)] [InlineData(21)] [InlineData(65)] [InlineData(1000)] [InlineData(10000)]
    public void ExhaustiveReferenceChecksAllEighteenColumnsAndPartitionEdges(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedPivotBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal(count, Assert.IsType<List<RollingPivotsResult>>(fixture.NativeSingle()).Count);
            Assert.Equal(count, Assert.IsType<RollingPivotsResult[]>(fixture.NativeParallel()).Length);
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedPivotBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Fact]
    public void AllocationRouteRetainsAllFourArms()
    {
        var report = SharedAllocationProbe.Run("Skender.GetRollingPivots", 65, 2);
        Assert.Equal("Skender.GetRollingPivots", report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }
}

