using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedParabolicNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in ParabolicComparison.Ids
        from count in new[] { 0, 1, 2, 4, 5, 6, 7, 8, 9, 31, 32, 33, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void PrefixesPreserveNativeStopsReversalsAndOwnership(string operation, int count)
    {
        var fixture = new SharedParabolicNativeBenchmarks { Count = count, Operation = operation };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var later = fixture.NativeParallel();
            Assert.NotSame(parallel, later);
            ComparisonVerifier.Compare(fixture.Normalize(serial), fixture.Normalize(parallel), operation, IndicatorErrorBudget.Exact);
            if (parallel is decimal?[] decimals)
                Assert.Equal(((IEnumerable<decimal?>)serial).ToArray(), decimals);
            if (parallel is double[] doubles)
                Assert.Equal(((double[])serial).Select(BitConverter.DoubleToInt64Bits), doubles.Select(BitConverter.DoubleToInt64Bits));
            if (parallel is ParabolicSarResult[] stops)
                for (int i = 0; i < count; i++) Assert.NotSame(stops[i], ((ParabolicSarResult[])later)[i]);
            var retained = fixture.Normalize(parallel);
            Array.Clear((Array)later);
            ComparisonVerifier.Compare(retained, fixture.Normalize(parallel), "retained SAR", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsAllFourMappingsAndThreeEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedParabolicNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "NativeParallel", "NativeSingle", "Snapshot" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
