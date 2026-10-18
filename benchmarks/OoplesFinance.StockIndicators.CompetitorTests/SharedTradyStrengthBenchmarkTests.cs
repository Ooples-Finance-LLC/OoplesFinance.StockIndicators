using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTradyStrengthBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "RelativeStrength", "RelativeStrengthIndex", "NetMomentumOscillator", "RelativeMomentum", "RelativeMomentumIndex" }
        from count in new[] { 1, 20, 21, 24, 25, 26, 513, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTradyStrengthBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var retained = serial.ToArray();
            Assert.Equal(count, serial.Count);
            Assert.Equal(retained, parallel);
            var second = fixture.NativeSingle();
            var repeated = fixture.NativeParallel();
            Assert.NotSame(serial, second);
            Assert.NotSame(parallel, repeated);
            Assert.Equal(retained, second);
            Assert.Equal(retained, repeated);
            second.Clear();
            Array.Clear(repeated);
            Assert.Equal(retained, serial);
            Assert.Equal(retained, parallel);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverFiveOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyStrengthBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(40, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
