using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedSkenderEmaFamilyBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetMacd", "Skender.GetPvo", "Skender.GetPmo", "Skender.GetTsi", "Skender.GetTrix" }
        from count in new[] { 1, 12, 20, 25, 26, 33, 34, 39, 40, 48, 49, 59, 60, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativePrefixesRetainValuesMasksDatesAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedSkenderEmaFamilyBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = (System.Collections.IList)fixture.NativeSingle();
            var expected = SharedSkenderEmaFamilyBenchmarks.Normalize(first);
            var second = (System.Collections.IList)fixture.NativeSingle();
            var parallel = (Array)fixture.NativeParallel();
            var repeated = (Array)fixture.NativeParallel();
            Assert.NotSame(first, second);
            Assert.NotSame(parallel, repeated);
            for (int i = 0; i < count; i++)
            {
                Assert.NotSame(first[i], second[i]);
                Assert.NotSame(parallel.GetValue(i), repeated.GetValue(i));
            }
            ComparisonVerifier.Compare(expected, SharedSkenderEmaFamilyBenchmarks.Normalize(second), "repeated serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderEmaFamilyBenchmarks.Normalize(parallel), "parallel", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderEmaFamilyBenchmarks.Normalize(repeated), "repeated parallel", IndicatorErrorBudget.Exact);
            second.Clear();
            Array.Clear(repeated);
            ComparisonVerifier.Compare(expected, SharedSkenderEmaFamilyBenchmarks.Normalize(first), "retained serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderEmaFamilyBenchmarks.Normalize(parallel), "retained parallel", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverFiveOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderEmaFamilyBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(40, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
