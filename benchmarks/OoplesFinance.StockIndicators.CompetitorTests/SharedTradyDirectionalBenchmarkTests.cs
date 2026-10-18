using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedTradyDirectionalBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from name in DirectionalComparison.Names
        from count in new[] { 1000,10000 }
        select new object[] { name,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainExactValuesAndPresence(string name,int count)
    {
        var fixture=new SharedTradyDirectionalBenchmarks { Operation="Trady.Indicator."+name,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyDirectionalBenchmarks))));
            using var full=fixture.BuilderFull();
            using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
