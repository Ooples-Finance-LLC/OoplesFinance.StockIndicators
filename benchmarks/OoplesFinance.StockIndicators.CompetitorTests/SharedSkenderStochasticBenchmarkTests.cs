using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedSkenderStochasticBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetStoch", "Skender.GetStochRsi", "Skender.GetSmi" }
        from count in new[] { 1, 13, 14, 15, 16, 17, 18, 19, 20, 21, 39, 40, 41, 42, 44, 45, 46, 47, 143, 144, 145, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativeRangesRetainValuesMasksDatesAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedSkenderStochasticBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = (System.Collections.IList)fixture.NativeSingle();
            var expected = SharedSkenderStochasticBenchmarks.Normalize(first);
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
            ComparisonVerifier.Compare(expected, SharedSkenderStochasticBenchmarks.Normalize(second), "repeated serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderStochasticBenchmarks.Normalize(parallel), "parallel", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderStochasticBenchmarks.Normalize(repeated), "repeated parallel", IndicatorErrorBudget.Exact);
            second.Clear();
            Array.Clear(repeated);
            ComparisonVerifier.Compare(expected, SharedSkenderStochasticBenchmarks.Normalize(first), "retained serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderStochasticBenchmarks.Normalize(parallel), "retained parallel", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverThreeOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderStochasticBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
