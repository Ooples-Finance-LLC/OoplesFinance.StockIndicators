using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedAtrStopBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetAtrStop", "Skender.GetSuperTrend", "Skender.GetChandelier", "Trady.Indicator.ChandelierExit" }
        from count in new[] { 1000, 10000 }
        select new object[] { operation, count };
    [Theory, MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainEveryValueAndPresence(string operation, int count)
    {
        var fixture = new SharedAtrStopBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedAtrStopBenchmarks))));
            using var full = fixture.BuilderFull(); using var latest = fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
