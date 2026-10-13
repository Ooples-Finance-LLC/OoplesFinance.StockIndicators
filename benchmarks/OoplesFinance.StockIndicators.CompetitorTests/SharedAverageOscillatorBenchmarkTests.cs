using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedAverageOscillatorBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Trady.Indicator.SimpleMovingAverageOscillator","Trady.Indicator.ExponentialMovingAverageOscillator","Trady.Indicator.ExponentialMovingAverage","Trady.Indicator.WeightedMovingAverage","Skender.GetAwesome" }
        from count in new[] { 1000,10000 }
        select new object[] { operation,count };
    [Theory,MemberData(nameof(Cases))]
    public void SavedBaselineInputsRetainAllExactValuesAndPresence(string operation,int count)
    {
        var fixture=new SharedAverageOscillatorBenchmarks { Operation=operation,Count=count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedAverageOscillatorBenchmarks))));
            using var full=fixture.BuilderFull(); using var latest=fixture.BuilderLatestOnly();
        }
        finally { fixture.Cleanup(); }
    }
}
