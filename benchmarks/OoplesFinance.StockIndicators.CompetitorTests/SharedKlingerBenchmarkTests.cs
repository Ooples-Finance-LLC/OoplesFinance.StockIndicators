using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedKlingerBenchmarkTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(55)]
    [InlineData(56)]
    [InlineData(57)]
    [InlineData(68)]
    [InlineData(69)]
    [InlineData(127)]
    [InlineData(1000)]
    [InlineData(10000)]
    public void SetupChecksStartupAndExactNativePrefixReplay(int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedKlingerBenchmarks { Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }

    [Fact]
    public void NativeResultsAreEagerForBenchmarkValidation() =>
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedKlingerBenchmarks))));
}
