using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedHilbertTrendlineBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Htit", 1)] [InlineData("QuanTAlib.Htit", 65)]
    [InlineData("QuanTAlib.Htit", 1000)] [InlineData("QuanTAlib.Htit", 10000)]
    [InlineData("Skender.GetHtTrendline", 1)] [InlineData("Skender.GetHtTrendline", 65)]
    [InlineData("Skender.GetHtTrendline", 1000)] [InlineData("Skender.GetHtTrendline", 10000)]
    public void SetupChecksIndependentArithmeticAllOwnedColumnsAndEagerResults(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedHilbertTrendlineBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            if (operation == "QuanTAlib.Htit")
            {
                Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeSingle()).Length);
                Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeParallel()).Length);
            }
            else
            {
                Assert.Equal(count, Assert.IsType<List<HtlResult>>(fixture.NativeSingle()).Count);
                Assert.Equal(count, Assert.IsType<HtlResult[]>(fixture.NativeParallel()).Length);
            }
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedHilbertTrendlineBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Theory]
    [InlineData("QuanTAlib.Htit")] [InlineData("Skender.GetHtTrendline")]
    public void AllocationRouteRetainsAllFourArms(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 64, 2);
        Assert.Equal(operation, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }
}
