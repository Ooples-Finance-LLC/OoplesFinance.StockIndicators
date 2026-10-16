using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedAdaptiveVariantBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Kama", "Trady.Indicator.KaufmanAdaptiveMovingAverage" }
        from count in new[] { 0, 1, 19, 20, 21, 65, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void SavedInputsAndStartupRetainEveryValueAndPresence(string operation, int count)
    {
        var fixture = new SharedAdaptiveVariantBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedAdaptiveVariantBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
