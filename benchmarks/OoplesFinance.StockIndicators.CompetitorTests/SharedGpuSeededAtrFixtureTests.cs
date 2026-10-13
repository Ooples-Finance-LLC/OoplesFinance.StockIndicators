using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedGpuSeededAtrFixtureTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetAtr", "TaLib.Functions.Atr", "TaLib.Functions.Natr", "Trady.Indicator.AverageTrueRange" }
        from count in new[] { 21, 129, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeviceColumnsMatchIndependentCpuAndNativeReferences(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedGpuSeededAtrBenchmarks { Operation = operation, Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData("Skender.GetAtr")] [InlineData("TaLib.Functions.Atr")]
    [InlineData("TaLib.Functions.Natr")] [InlineData("Trady.Indicator.AverageTrueRange")]
    public void AllocationProbeRetainsSixEagerArms(string operation)
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedGpuSeededAtrBenchmarks))));
        var report = SharedAllocationProbe.Run(operation, 64, 1, includeGpu: true);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }
}
