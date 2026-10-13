using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedDeviationAverageBenchmarkTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(39)] [InlineData(40)] [InlineData(137)] [InlineData(513)]
    [InlineData(1000)] [InlineData(10000)]
    public void AllArmsPreserveRoundedRecurrencesAndNativePrefixReplay(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedDeviationAverageBenchmarks { Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }

    [Fact]
    public void AllocationProbeRoutesEagerNativeArms()
    {
        var report = SharedAllocationProbe.Run("QuanTAlib.Dsma", 64, 1);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm => Assert.True(arm.Bytes.Single() > 0));
        Assert.Throws<ArgumentException>(() => SharedAllocationProbe.Run("QuanTAlib.Dsma", 64, 1, true));
    }
}
