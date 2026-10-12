using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedExpandingRecurrenceBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Rema", 1)] [InlineData("QuanTAlib.Rema", 2)]
    [InlineData("QuanTAlib.Rema", 19)] [InlineData("QuanTAlib.Rema", 65)]
    [InlineData("QuanTAlib.Zlema", 1)] [InlineData("QuanTAlib.Zlema", 9)]
    [InlineData("QuanTAlib.Zlema", 20)] [InlineData("QuanTAlib.Zlema", 65)]
    public void SetupChecksExpandingStartupAndFullPrefixReplay(string operation, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedExpandingRecurrenceBenchmarks { Operation = operation, Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
