using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedEnvelopeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetMaEnvelopes","Skender.GetKeltner","Skender.GetStarcBands","Trady.Indicator.KeltnerChannels" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedEnvelopeBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedEnvelopeBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
