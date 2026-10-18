using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedDynamicMomentumBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(11)]
    [InlineData(33)]
    [InlineData(65)]
    [InlineData(127)]
    public void DirectArmsPreserveStartupPeriodChangesAndUnevenParallelRanges(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedDynamicMomentumBenchmarks { Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
