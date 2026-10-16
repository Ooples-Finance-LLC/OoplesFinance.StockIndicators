using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedRelativeVolatilityBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(27)]
    [InlineData(28)]
    [InlineData(65)]
    [InlineData(127)]
    [InlineData(8193)]
    public void SetupChecksStartupAndBoundedNativeReplayAtUnevenWorkerBoundaries(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedRelativeVolatilityBenchmarks { Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
