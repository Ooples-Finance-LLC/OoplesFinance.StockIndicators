using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedHilbertSignalBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.HtDcPhase", "TaLib.Functions.HtSine", "TaLib.Functions.HtTrendMode", "TaLib.Functions.HtTrendline" }
        from count in new[] { 0, 36, 37, 62, 63, 64, 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainEveryDetailColumn(string operation, int count)
    {
        var fixture = new SharedHilbertSignalBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedHilbertSignalBenchmarks))));
            using var full = fixture.BuilderFull(); using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
