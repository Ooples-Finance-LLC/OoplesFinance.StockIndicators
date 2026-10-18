using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedQuarticDynamicBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Mgdi")]
    [InlineData("Skender.GetDynamic")]
    public void AllocationRouteRetainsAllFourArms(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 64, 2);
        Assert.Equal(operation, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm =>
        {
            Assert.Equal(2, arm.Bytes.Length);
            Assert.All(arm.Bytes, bytes => Assert.True(bytes >= 0));
        });
    }

    [Theory]
    [InlineData("QuanTAlib.Mgdi", 1)] [InlineData("QuanTAlib.Mgdi", 65)]
    [InlineData("QuanTAlib.Mgdi", 1000)] [InlineData("QuanTAlib.Mgdi", 10000)]
    [InlineData("Skender.GetDynamic", 1)] [InlineData("Skender.GetDynamic", 65)]
    [InlineData("Skender.GetDynamic", 1000)] [InlineData("Skender.GetDynamic", 10000)]
    public void SetupChecksIndependentArithmeticBothHistoriesAndEagerNativeResults(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedQuarticDynamicBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            if (operation == "QuanTAlib.Mgdi")
            {
                Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeSingle()).Length);
                Assert.Equal(count, Assert.IsType<double[]>(fixture.NativeParallel()).Length);
            }
            else
            {
                Assert.Equal(count, Assert.IsType<List<DynamicResult>>(fixture.NativeSingle()).Count);
                Assert.Equal(count, Assert.IsType<DynamicResult[]>(fixture.NativeParallel()).Length);
            }
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedQuarticDynamicBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
