using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSkenderRetrospectiveNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Skender.GetDpo", "Skender.GetFcb" }
        from count in new[] { 0, 1, 10, 11, 12, 19, 20, 21, 39, 40, 41, 87, 88, 89, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void ConfirmedPrefixesAndTrailingMasksMatchIndependentReferences(string operation, int count)
    {
        var fixture = new SharedSkenderRetrospectiveNativeBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var first = fixture.NativeParallel();
            var later = fixture.NativeParallel();
            Assert.NotSame(first, later);
            ComparisonVerifier.Compare(SharedSkenderRetrospectiveNativeBenchmarks.Normalize(first),
                SharedSkenderRetrospectiveNativeBenchmarks.Normalize(later), operation);
            if (count > 0)
            {
                var retained = SharedSkenderRetrospectiveNativeBenchmarks.Normalize(first);
                Array.Clear((Array)later);
                ComparisonVerifier.Compare(retained, SharedSkenderRetrospectiveNativeBenchmarks.Normalize(first), operation);
            }
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsSnapshotAndTwoEagerNativeArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderRetrospectiveNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(12, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "NativeParallel", "NativeSingle", "Snapshot" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
