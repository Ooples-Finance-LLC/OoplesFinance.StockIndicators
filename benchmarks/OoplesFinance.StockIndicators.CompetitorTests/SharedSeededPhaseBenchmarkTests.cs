using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedSeededPhaseBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Mama", 1)] [InlineData("QuanTAlib.Mama", 65)]
    [InlineData("QuanTAlib.Mama", 1000)] [InlineData("QuanTAlib.Mama", 10000)]
    [InlineData("Skender.GetMama", 1)] [InlineData("Skender.GetMama", 65)]
    [InlineData("Skender.GetMama", 1000)] [InlineData("Skender.GetMama", 10000)]
    public void SetupChecksIndependentArithmeticAllOwnedColumnsAndEagerResults(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedSeededPhaseBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            if (operation == "QuanTAlib.Mama")
            {
                Assert.Equal(2 * count, Assert.IsType<double[]>(fixture.NativeSingle()).Length);
                Assert.Equal(2 * count, Assert.IsType<double[]>(fixture.NativeParallel()).Length);
            }
            else
            {
                Assert.Equal(count, Assert.IsType<List<MamaResult>>(fixture.NativeSingle()).Count);
                Assert.Equal(count, Assert.IsType<MamaResult[]>(fixture.NativeParallel()).Length);
            }
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSeededPhaseBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Theory]
    [InlineData("QuanTAlib.Mama")] [InlineData("Skender.GetMama")]
    public void AllocationRouteRetainsAllFourArms(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 64, 2);
        Assert.Equal(operation, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }
}
