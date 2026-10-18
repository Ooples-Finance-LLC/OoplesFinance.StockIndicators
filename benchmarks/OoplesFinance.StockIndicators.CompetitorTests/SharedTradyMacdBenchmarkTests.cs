using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTradyMacdBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "MovingAverageConvergenceDivergence", "MovingAverageConvergenceDivergenceHistogram" }
        from count in new[] { 1, 8, 9, 10, 11, 12, 13, 25, 26, 27, 71, 72, 73, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTradyMacdBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = (System.Collections.IList)fixture.NativeSingle();
            var parallel = (Array)fixture.NativeParallel();
            var retained = SharedTradyMacdBenchmarks.Normalize(serial);
            Assert.Equal(count, serial.Count);
            SharedTradyMacdBenchmarks.RequireSame(retained, SharedTradyMacdBenchmarks.Normalize(parallel));
            var second = (System.Collections.IList)fixture.NativeSingle();
            var repeated = (Array)fixture.NativeParallel();
            Assert.NotSame(serial, second);
            Assert.NotSame(parallel, repeated);
            SharedTradyMacdBenchmarks.RequireSame(retained, SharedTradyMacdBenchmarks.Normalize(second));
            SharedTradyMacdBenchmarks.RequireSame(retained, SharedTradyMacdBenchmarks.Normalize(repeated));
            second.Clear();
            Array.Clear(repeated);
            SharedTradyMacdBenchmarks.RequireSame(retained, SharedTradyMacdBenchmarks.Normalize(serial));
            SharedTradyMacdBenchmarks.RequireSame(retained, SharedTradyMacdBenchmarks.Normalize(parallel));
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverBothOperationsAndSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyMacdBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(16, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
