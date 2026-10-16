using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedCommodityChannelBenchmarkTests
{
    [Theory]
    [InlineData(27)]
    [InlineData(65)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void SetupChecksIndependentNativeOrdersAndBothOwnedHistories(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedCommodityChannelBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal(count - 19, fixture.NativeSingle().Length);
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedCommodityChannelBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
}
