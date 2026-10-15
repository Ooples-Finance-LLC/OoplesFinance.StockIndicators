using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTradyNativeCandleBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "DarkCloudCover", "DragonifyDoji", "RisingThreeMethods", "FallingThreeMethods" }
        from count in new[] { 1, 2, 3, 4, 19, 20, 21, 23, 24, 25, 183, 184, 185, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTradyNativeCandleBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            // Setup checks both native paths against the independent reference at
            // every size. Repeat ownership checks through 1,000 bars; the native
            // three-method implementation rebuilds full-series percentiles per check.
            if (count > 1000) return;
            var serial = (System.Collections.IList)fixture.NativeSingle();
            var parallel = (Array)fixture.NativeParallel();
            var retained = SharedTradyNativeCandleBenchmarks.Normalize(serial);
            Assert.Equal(count, serial.Count);
            Assert.Equal(retained, SharedTradyNativeCandleBenchmarks.Normalize(parallel));
            var second = (System.Collections.IList)fixture.NativeSingle();
            var repeated = (Array)fixture.NativeParallel();
            Assert.NotSame(serial, second);
            Assert.NotSame(parallel, repeated);
            Assert.Equal(retained, SharedTradyNativeCandleBenchmarks.Normalize(second));
            Assert.Equal(retained, SharedTradyNativeCandleBenchmarks.Normalize(repeated));
            second.Clear();
            Array.Clear(repeated);
            Assert.Equal(retained, SharedTradyNativeCandleBenchmarks.Normalize(serial));
            Assert.Equal(retained, SharedTradyNativeCandleBenchmarks.Normalize(parallel));
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverFourOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyNativeCandleBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(32, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
