using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTradyNativeStochasticBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Raw", "Fast", "Slow", "Full", "FastDifference", "SlowDifference", "FullDifference" }
        from count in new[] { 1, 2, 3, 4, 5, 19, 20, 21, 22, 23, 24, 25, 183, 184, 185, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTradyNativeStochasticBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = (System.Collections.IList)fixture.NativeSingle();
            var parallel = (Array)fixture.NativeParallel();
            var retained = SharedTradyNativeStochasticBenchmarks.Normalize(serial);
            Assert.Equal(count, serial.Count);
            SharedTradyNativeStochasticBenchmarks.RequireSame(retained, SharedTradyNativeStochasticBenchmarks.Normalize(parallel));
            var second = (System.Collections.IList)fixture.NativeSingle();
            var repeated = (Array)fixture.NativeParallel();
            Assert.NotSame(serial, second);
            Assert.NotSame(parallel, repeated);
            SharedTradyNativeStochasticBenchmarks.RequireSame(retained, SharedTradyNativeStochasticBenchmarks.Normalize(second));
            SharedTradyNativeStochasticBenchmarks.RequireSame(retained, SharedTradyNativeStochasticBenchmarks.Normalize(repeated));
            second.Clear();
            Array.Clear(repeated);
            SharedTradyNativeStochasticBenchmarks.RequireSame(retained, SharedTradyNativeStochasticBenchmarks.Normalize(serial));
            SharedTradyNativeStochasticBenchmarks.RequireSame(retained, SharedTradyNativeStochasticBenchmarks.Normalize(parallel));
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverSevenOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyNativeStochasticBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(56, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
