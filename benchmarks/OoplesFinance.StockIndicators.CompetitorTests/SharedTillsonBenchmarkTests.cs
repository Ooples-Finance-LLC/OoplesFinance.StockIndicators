using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Skender.Stock.Indicators;
using TaCore = TALib.Core;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA T3 settings")]
public sealed class SharedTillsonBenchmarkTests
{
    [Theory]
    [InlineData("Skender.GetT3", 1)] [InlineData("Skender.GetT3", 129)]
    [InlineData("Skender.GetT3", 1000)] [InlineData("Skender.GetT3", 10000)]
    [InlineData("TaLib.Functions.T3", 115)] [InlineData("TaLib.Functions.T3", 129)]
    [InlineData("TaLib.Functions.T3", 1000)] [InlineData("TaLib.Functions.T3", 10000)]
    [InlineData("QuanTAlib.T3", 1)] [InlineData("QuanTAlib.T3", 129)]
    [InlineData("QuanTAlib.T3", 1000)] [InlineData("QuanTAlib.T3", 10000)]
    public void SetupChecksIndependentArithmeticBothHistoriesAndEagerResults(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedTillsonBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            if (operation == "Skender.GetT3")
            {
                Assert.Equal(count, Assert.IsType<List<T3Result>>(fixture.NativeSingle()).Count);
                Assert.Equal(count, Assert.IsType<T3Result[]>(fixture.NativeParallel()).Length);
            }
            else
            {
                int length = operation == "TaLib.Functions.T3" ? count - 114 : count;
                Assert.Equal(length, Assert.IsType<double[]>(fixture.NativeSingle()).Length);
                Assert.Equal(length, Assert.IsType<double[]>(fixture.NativeParallel()).Length);
            }
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedTillsonBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }

    [Theory]
    [InlineData("Skender.GetT3")] [InlineData("TaLib.Functions.T3")] [InlineData("QuanTAlib.T3")]
    public void AllocationRouteRetainsAllFourArms(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 129, 2);
        Assert.Equal(operation, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.Equal(2, arm.Bytes.Length));
    }

    [Fact]
    public void NondefaultNativeSuppressionIsRejected()
    {
        int prior = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.T3);
        var fixture = new SharedTillsonBenchmarks { Operation = "TaLib.Functions.T3", Count = 129 };
        try
        {
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, 1);
            Assert.Throws<InvalidOperationException>(fixture.Setup);
        }
        finally { fixture.Cleanup(); TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.T3, prior); }
    }
}
