using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("OBV benchmark settings")]
public sealed class SharedVolumeWeightedPriceFixtureTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetVwap", "Skender.GetVwma", "Trady.Indicator.VolumeWeightedAveragePrice" }
        from count in new[] { 1, 19, 20, 21, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory]
    [MemberData(nameof(Cases))]
    public void NativePartitionsAndEveryBuilderColumnMatchIndependentReferences(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedVolumeWeightedPriceBenchmarks { Operation = operation, Count = count };
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
            Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
        }
        finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Theory]
    [InlineData("Skender.GetVwap")] [InlineData("Skender.GetVwma")]
    [InlineData("Trady.Indicator.VolumeWeightedAveragePrice")]
    public void NativeResultsAreEagerAndAllocationProbeRetainsAllArms(string operation)
    {
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedVolumeWeightedPriceBenchmarks))));
        var report = SharedAllocationProbe.Run(operation, 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(Assert.Single(arm.Bytes) > 0));
    }
}
