using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTaReversalContinuationBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "AdvanceBlock", "Stalled", "ThreeStarsInSouth", "ThreeBlackCrows", "IdenticalThreeCrows", "ThreeWhiteSoldiers", "PiercingLine", "DarkCloudCover" }
        from count in new[] { 1, 2, 10, 11, 12, 13, 14, 87, 88, 89, 95, 96, 97, 103, 104, 105, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTaReversalContinuationBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var retained = serial.ToArray();
            Assert.Equal(count, serial.Length);
            Assert.Equal(retained, parallel);
            var second = fixture.NativeSingle();
            var repeated = fixture.NativeParallel();
            Assert.NotSame(serial, second);
            Assert.NotSame(parallel, repeated);
            Assert.Equal(retained, second);
            Assert.Equal(retained, repeated);
            Array.Clear(second);
            Array.Clear(repeated);
            Assert.Equal(retained, serial);
            Assert.Equal(retained, parallel);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverEightOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaReversalContinuationBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(64, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
