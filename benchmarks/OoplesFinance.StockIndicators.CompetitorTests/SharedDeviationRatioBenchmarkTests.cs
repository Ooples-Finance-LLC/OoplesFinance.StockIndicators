using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedDeviationRatioBenchmarkTests
{
    [Theory]
    [InlineData(1)] [InlineData(65)] [InlineData(1000)] [InlineData(10000)]
    public void SetupChecksIndependentArithmeticBothOwnedColumnsAndEagerResults(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedDeviationRatioBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeSingle()).Length);
            Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeParallel()).Length);
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedDeviationRatioBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Fact]
    public void AllocationRouteRetainsAllFourArms()
    {
        var report = SharedAllocationProbe.Run("QuanTAlib.Vidya", 64, 2);
        Assert.Equal("QuanTAlib.Vidya", report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }
}
