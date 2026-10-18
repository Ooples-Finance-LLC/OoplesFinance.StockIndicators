using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedHoltWinterBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    [InlineData(31)]
    [InlineData(65)]
    public void SetupChecksInitialSeedAndFullPrefixParallelReplay(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedHoltWinterBenchmarks { Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
