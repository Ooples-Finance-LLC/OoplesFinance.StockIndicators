using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedClassicalWindowVariantBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from method in new[] { ClassicAverageMethod.Wma, ClassicAverageMethod.Trima }
        from count in new[] { 1, 19, 20, 21, 1000, 10000 }
        select new object[] { method, count };

    [Theory, MemberData(nameof(Cases))]
    public void VariantsMatchIndependentReferenceAcrossStartupAndFullWindows(
        ClassicAverageMethod method, int count)
    {
        var fixture = new SharedClassicalWindowVariantBenchmarks { Method = method, Count = count };
        try { fixture.Setup(); }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryIncludesBothVariantsAndBothHistoryModes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedClassicalWindowVariantBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(8, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
        Assert.Equal(new[] { ClassicAverageMethod.Wma, ClassicAverageMethod.Trima },
            run.BenchmarksCases.Select(test => (ClassicAverageMethod)test.Parameters.Items.Single(
                parameter => parameter.Name == "Method").Value).Distinct().OrderBy(method => method));
    }
}
