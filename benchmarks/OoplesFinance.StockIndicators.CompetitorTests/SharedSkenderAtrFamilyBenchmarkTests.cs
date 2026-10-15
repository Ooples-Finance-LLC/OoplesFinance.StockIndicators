using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Xunit;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedSkenderAtrFamilyBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetAtrStop", "Skender.GetSuperTrend", "Skender.GetChandelier", "Skender.GetKeltner", "Skender.GetStarcBands" }
        from count in new[] { 1, 19, 20, 21, 22, 159, 160, 161, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void DirectNativePrefixesRetainValuesMasksDatesAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedSkenderAtrFamilyBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = (System.Collections.IList)fixture.NativeSingle();
            var expected = SharedSkenderAtrFamilyBenchmarks.Normalize(first);
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
            ComparisonVerifier.Compare(expected, SharedSkenderAtrFamilyBenchmarks.Normalize(second), "repeated serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderAtrFamilyBenchmarks.Normalize(parallel), "parallel", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderAtrFamilyBenchmarks.Normalize(repeated), "repeated parallel", IndicatorErrorBudget.Exact);
            AssertDecimalColumns(first, second);
            AssertDecimalColumns(first, parallel);
            AssertDecimalColumns(first, repeated);
            second.Clear();
            Array.Clear(repeated);
            ComparisonVerifier.Compare(expected, SharedSkenderAtrFamilyBenchmarks.Normalize(first), "retained serial", IndicatorErrorBudget.Exact);
            ComparisonVerifier.Compare(expected, SharedSkenderAtrFamilyBenchmarks.Normalize(parallel), "retained parallel", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    private static void AssertDecimalColumns(object expected, object actual)
    {
        if (expected is IReadOnlyList<AtrStopResult> stops)
            Assert.Equal(stops.Select(v => (v.AtrStop, v.BuyStop, v.SellStop)),
                ((IReadOnlyList<AtrStopResult>)actual).Select(v => (v.AtrStop, v.BuyStop, v.SellStop)));
        if (expected is IReadOnlyList<SuperTrendResult> trends)
            Assert.Equal(trends.Select(v => (v.SuperTrend, v.UpperBand, v.LowerBand)),
                ((IReadOnlyList<SuperTrendResult>)actual).Select(v => (v.SuperTrend, v.UpperBand, v.LowerBand)));
    }

    [Fact]
    public void FourEagerArmsCoverFiveOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderAtrFamilyBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(40, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
