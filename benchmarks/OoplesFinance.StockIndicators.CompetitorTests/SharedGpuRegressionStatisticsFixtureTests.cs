using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedGpuRegressionStatisticsFixtureTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "QuanTAlib.Slope", "Skender.GetSlope" }
        from count in new[] { 1, 19, 20, 21, 257, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeviceColumnsMatchIndependentCpuAndNativeReferences(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedGpuRegressionStatisticsBenchmarks { Operation = operation, Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData("QuanTAlib.Slope")] [InlineData("Skender.GetSlope")]
    public void AllocationProbeRetainsSixEagerArms(string operation)
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedGpuRegressionStatisticsBenchmarks))));
        var report = SharedAllocationProbe.Run(operation, 64, 1, includeGpu: true);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "GpuFull", "GpuLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }
}
