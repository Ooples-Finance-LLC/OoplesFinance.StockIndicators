using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSchaffCycleBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(49)]
    [InlineData(50)]
    [InlineData(58)]
    [InlineData(59)]
    [InlineData(60)]
    [InlineData(61)]
    [InlineData(127)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void SetupChecksStartupAndExactNativePrefixReplay(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedSchaffCycleBenchmarks { Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }

    [Fact]
    public void NativeResultsAreEagerForBenchmarkValidation() =>
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSchaffCycleBenchmarks))));
}
