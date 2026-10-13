using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedStrengthBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetRsi","Skender.GetAdx","Trady.Indicator.RelativeStrength","Trady.Indicator.RelativeStrengthIndex","Trady.Indicator.NetMomentumOscillator","Trady.Indicator.RelativeMomentum","Trady.Indicator.RelativeMomentumIndex" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedStrengthBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedStrengthBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
