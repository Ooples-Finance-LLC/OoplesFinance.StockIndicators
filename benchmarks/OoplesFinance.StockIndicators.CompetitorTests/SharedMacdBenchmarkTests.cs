using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedMacdBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Macd","TaLib.Functions.MacdFix","TaLib.Functions.MacdExt","Skender.GetMacd","Skender.GetPvo","Trady.Indicator.MovingAverageConvergenceDivergence","Trady.Indicator.MovingAverageConvergenceDivergenceHistogram" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedMacdBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedMacdBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
