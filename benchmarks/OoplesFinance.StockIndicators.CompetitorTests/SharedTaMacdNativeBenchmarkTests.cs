using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedTaMacdNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Macd", "TaLib.Functions.MacdFix", "TaLib.Functions.MacdExt" }
        from count in new[] { 2, 25, 26, 32, 33, 34, 35, 40, 41, 42, 263, 264, 265, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void PackedNativeColumnsMatchReferencesAndRetainOwnership(string operation, int count)
    {
        var fixture = new SharedTaMacdNativeBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var later = fixture.NativeSingle().Concat(fixture.NativeParallel()).ToArray();
            Assert.Equal(3, serial.Length);
            for (int slot = 0; slot < 3; slot++)
            {
                var bits = serial[slot].Select(BitConverter.DoubleToInt64Bits).ToArray();
                Assert.Equal(Math.Max(0, count - 33), bits.Length);
                Assert.Equal(bits, parallel[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bits, later[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bits, later[slot + 3].Select(BitConverter.DoubleToInt64Bits));
                Assert.NotSame(serial[slot], later[slot]);
                Assert.NotSame(parallel[slot], later[slot + 3]);
                Array.Fill(later[slot], double.NaN);
                Array.Fill(later[slot + 3], double.NaN);
                Assert.Equal(bits, serial[slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bits, parallel[slot].Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsFourEagerArmsForThreeMappings()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaMacdNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
