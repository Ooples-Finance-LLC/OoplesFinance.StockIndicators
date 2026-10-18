using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTradyAtrChannelBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "ChandelierExit", "KeltnerChannels" }
        from count in new[] { 1, 2, 19, 20, 21, 22, 159, 160, 161, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTradyAtrChannelBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = (System.Collections.IList)fixture.NativeSingle();
            var parallel = (Array)fixture.NativeParallel();
            var retained = SharedTradyAtrChannelBenchmarks.Normalize(serial);
            Assert.Equal(count, serial.Count);
            SharedTradyAtrChannelBenchmarks.RequireSame(retained, SharedTradyAtrChannelBenchmarks.Normalize(parallel));
            var second = (System.Collections.IList)fixture.NativeSingle();
            var repeated = (Array)fixture.NativeParallel();
            Assert.NotSame(serial, second);
            Assert.NotSame(parallel, repeated);
            SharedTradyAtrChannelBenchmarks.RequireSame(retained, SharedTradyAtrChannelBenchmarks.Normalize(second));
            SharedTradyAtrChannelBenchmarks.RequireSame(retained, SharedTradyAtrChannelBenchmarks.Normalize(repeated));
            second.Clear();
            Array.Clear(repeated);
            SharedTradyAtrChannelBenchmarks.RequireSame(retained, SharedTradyAtrChannelBenchmarks.Normalize(serial));
            SharedTradyAtrChannelBenchmarks.RequireSame(retained, SharedTradyAtrChannelBenchmarks.Normalize(parallel));
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverBothOperationsAndSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyAtrChannelBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(16, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
