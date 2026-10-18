using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedTradyRemainingCandleBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from name in new[] { "DarkCloudCover", "RisingThreeMethods", "FallingThreeMethods", "DragonifyDoji" }
        from count in new[] { 1000,10000 }
        select new object[] { name,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainExactReferenceContracts(string name,int count)
    {
        var fixture=new SharedTradyRemainingCandleBenchmarks { Operation="Trady.Candlestick."+name,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyRemainingCandleBenchmarks))));
            using var full=fixture.BuilderFull();
            using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
