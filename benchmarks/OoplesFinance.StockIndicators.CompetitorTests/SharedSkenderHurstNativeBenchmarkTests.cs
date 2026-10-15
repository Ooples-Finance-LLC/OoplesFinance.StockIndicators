using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSkenderHurstNativeBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(27)] [InlineData(28)] [InlineData(29)] [InlineData(159)] [InlineData(160)]
    [InlineData(161)] [InlineData(1000)] [InlineData(10000)]
    public void WindowWorkersRetainBothNativeExponentsAndOwnedRows(int count)
    {
        var fixture = new SharedSkenderHurstNativeBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var later = fixture.NativeParallel();
            Assert.Equal(count, parallel.Length);
            Assert.NotSame(parallel, later);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(serial[i].Date, parallel[i].Date);
                Assert.Equal(serial[i].HurstExponent, parallel[i].HurstExponent);
                Assert.Equal(serial[i].HurstExponentAL, parallel[i].HurstExponentAL);
                Assert.NotSame(parallel[i], later[i]);
            }
            var expected = SharedSkenderHurstNativeBenchmarks.Normalize(parallel);
            Array.Clear(later);
            ComparisonVerifier.Compare(expected, SharedSkenderHurstNativeBenchmarks.Normalize(parallel), "retained Hurst", HurstComparison.Budget);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsThreeEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderHurstNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(6, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "NativeParallel", "NativeSingle", "Snapshot" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
