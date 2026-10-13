using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedCrowSoldierBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from name in CrowSoldierComparison.Names
        from count in new[] { 1000, 10000 }
        select new object[] { name, count };

    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsMatchIndependentAndNativeReferences(string name, int count)
    {
        var fixture = new SharedCrowSoldierBenchmarks { Operation = "TaLib.Candles." + name, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedCrowSoldierBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
