using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedSkenderSmoothingBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetAlligator", "Skender.GetGator", "Skender.GetElderRay" }
        from count in new[] { 1, 7, 8, 9, 12, 13, 14, 19, 20, 21, 22, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativePrefixesRetainValuesMasksDatesAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedSkenderSmoothingBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = (System.Collections.IList)fixture.NativeSingle();
            var expected = SharedSkenderSmoothingBenchmarks.Normalize(first);
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
            ComparisonVerifier.Compare(expected, SharedSkenderSmoothingBenchmarks.Normalize(second), "repeated serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderSmoothingBenchmarks.Normalize(parallel), "parallel", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderSmoothingBenchmarks.Normalize(repeated), "repeated parallel", IndicatorErrorBudget.Exact);
            second.Clear();
            Array.Clear(repeated);
            ComparisonVerifier.Compare(expected, SharedSkenderSmoothingBenchmarks.Normalize(first), "retained serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderSmoothingBenchmarks.Normalize(parallel), "retained parallel", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverThreeOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderSmoothingBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
