using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedSeededAdaptiveBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Kama", 1)] [InlineData("QuanTAlib.Kama", 65)]
    [InlineData("QuanTAlib.Kama", 1000)] [InlineData("QuanTAlib.Kama", 10000)]
    [InlineData("Skender.GetKama", 1)] [InlineData("Skender.GetKama", 65)]
    [InlineData("Skender.GetKama", 1000)] [InlineData("Skender.GetKama", 10000)]
    public void SetupChecksIndependentArithmeticAllOwnedColumnsAndEagerResults(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedSeededAdaptiveBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            if (operation == "QuanTAlib.Kama")
            {
                Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeSingle()).Length);
                Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeParallel()).Length);
            }
            else
            {
                Assert.Equal(count, Assert.IsType<List<KamaResult>>(fixture.NativeSingle()).Count);
                Assert.Equal(count, Assert.IsType<KamaResult[]>(fixture.NativeParallel()).Length);
            }
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSeededAdaptiveBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Theory]
    [InlineData("QuanTAlib.Kama")] [InlineData("Skender.GetKama")]
    public void AllocationRouteRetainsAllFourArms(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 64, 2);
        Assert.Equal(operation, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }
}
