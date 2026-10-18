using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedHikkakeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from name in HikkakeComparison.Names
        from count in new[] { 1000, 10000 }
        select new object[] { name, count };

    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainSeparateExactAndNativeReferenceContracts(string name, int count)
    {
        var fixture = new SharedHikkakeBenchmarks { Operation = "TaLib.Candles." + name, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedHikkakeBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
