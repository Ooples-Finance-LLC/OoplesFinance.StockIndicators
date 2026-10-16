using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTaWindowNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.AvgDev", "TaLib.Functions.Accbands" }
        from count in new[] { 2, 18, 19, 20, 21, 26, 27, 28, 159, 160, 161, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchReferencesAndOwnEveryColumn(string operation, int count)
    {
        var fixture = new SharedTaWindowNativeBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var repeated = fixture.NativeParallel();
            var second = fixture.NativeSingle();
            Assert.Equal(operation.EndsWith("Accbands", StringComparison.Ordinal) ? 3 : 1, serial.Length);
            for (int slot = 0; slot < serial.Length; slot++)
            {
                var saved = serial[slot].Select(BitConverter.DoubleToInt64Bits).ToArray();
                Assert.Equal(Math.Max(0, count - 19), saved.Length);
                Assert.Equal(saved, parallel[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(saved, repeated[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(saved, second[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.NotSame(serial[slot], second[slot]);
                Assert.NotSame(parallel[slot], repeated[slot]);
                Array.Clear(second[slot]);
                Array.Clear(repeated[slot]);
                Assert.Equal(saved, serial[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(saved, parallel[slot].Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void FourEagerArmsCoverBothMappingsAndSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaWindowNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(16, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
