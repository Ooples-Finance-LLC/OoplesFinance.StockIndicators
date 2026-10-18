using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedConnorsBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(64)]
    [InlineData(101)]
    [InlineData(103)]
    [InlineData(127)]
    public void SetupChecksAllComponentsStartupAndUnevenPrefixReplay(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedConnorsBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            Assert.IsAssignableFrom<IReadOnlyList<Skender.Stock.Indicators.ConnorsRsiResult>>(fixture.NativeSingle());
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
