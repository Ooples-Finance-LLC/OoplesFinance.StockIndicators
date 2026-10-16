using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedSkenderDiagnosticsBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetMaEnvelopes", "Skender.GetSmaAnalysis", "Skender.GetPrs", "Skender.GetStdDevChannels" }
        from count in new[] { 1, 2, 19, 20, 21, 39, 40, 41, 159, 160, 161, 513, 1000, 10000 }
        where operation != "Skender.GetPrs" || count >= 20
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativeRangesRetainValuesMasksDatesAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedSkenderDiagnosticsBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = (System.Collections.IList)fixture.NativeSingle();
            var expected = SharedSkenderDiagnosticsBenchmarks.Normalize(first);
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
            ComparisonVerifier.Compare(expected, SharedSkenderDiagnosticsBenchmarks.Normalize(second), "repeated serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderDiagnosticsBenchmarks.Normalize(parallel), "parallel", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderDiagnosticsBenchmarks.Normalize(repeated), "repeated parallel", IndicatorErrorBudget.Exact);
            second.Clear();
            Array.Clear(repeated);
            ComparisonVerifier.Compare(expected, SharedSkenderDiagnosticsBenchmarks.Normalize(first), "retained serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderDiagnosticsBenchmarks.Normalize(parallel), "retained parallel", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverFourOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderDiagnosticsBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(32, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
