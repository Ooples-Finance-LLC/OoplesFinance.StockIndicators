using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedClassicStochasticBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Stoch","TaLib.Functions.StochF","TaLib.Functions.StochRsi","Skender.GetStochRsi","Trady.Indicator.StochasticsRsiOscillator" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedClassicStochasticBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedClassicStochasticBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
