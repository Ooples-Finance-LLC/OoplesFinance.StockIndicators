using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedSeededMomentumBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetPmo", "Skender.GetTsi", "Skender.GetTrix", "TaLib.Functions.Trix" }
        from count in new[] { 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainEveryValueAndPresence(string operation, int count)
    {
        var fixture = new SharedSeededMomentumBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSeededMomentumBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
