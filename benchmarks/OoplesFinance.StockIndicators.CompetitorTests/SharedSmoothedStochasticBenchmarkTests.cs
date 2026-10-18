using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedSmoothedStochasticBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetStoch","Skender.GetSmi","Trady.Indicator.StochasticsMomentumIndex" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedSmoothedStochasticBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSmoothedStochasticBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
