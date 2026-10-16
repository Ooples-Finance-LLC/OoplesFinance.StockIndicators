using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedPriceCandleBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetDoji", "Skender.GetMarubozu", "Skender.GetHeikinAshi" }
        from count in new[] { 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainEveryDetailColumn(string operation, int count)
    {
        var fixture = new SharedPriceCandleBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedPriceCandleBenchmarks))));
            using var full = fixture.BuilderFull(); using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
