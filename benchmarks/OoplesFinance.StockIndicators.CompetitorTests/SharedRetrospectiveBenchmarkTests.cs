using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedRetrospectiveBenchmarkTests
{
    [Theory]
    [InlineData(1)] [InlineData(40)] [InlineData(41)] [InlineData(65)] [InlineData(1000)] [InlineData(10000)]
    public void ExhaustiveReferenceChecksAllFourColumnsAndPartitionEdges(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedRetrospectiveBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal(count, Assert.IsType<List<FractalResult>>(fixture.NativeSingle()).Count);
            Assert.Equal(count, Assert.IsType<FractalResult[]>(fixture.NativeParallel()).Length);
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedRetrospectiveBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Fact]
    public void AllocationRouteRetainsAllFourArms()
    {
        var report = SharedAllocationProbe.Run("Skender.GetFractal", 65, 2);
        Assert.Equal("Skender.GetFractal", report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }
}
