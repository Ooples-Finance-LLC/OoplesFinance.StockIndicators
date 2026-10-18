using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedTradyStochasticBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Trady.Indicator.RawStochasticsValue","Trady.Indicator.Stochastics+Fast","Trady.Indicator.Stochastics+Slow","Trady.Indicator.Stochastics+Full","Trady.Indicator.StochasticsOscillator+Fast","Trady.Indicator.StochasticsOscillator+Slow","Trady.Indicator.StochasticsOscillator+Full","Trady.Indicator.StochasticsMomentum" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedTradyStochasticBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyStochasticBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
