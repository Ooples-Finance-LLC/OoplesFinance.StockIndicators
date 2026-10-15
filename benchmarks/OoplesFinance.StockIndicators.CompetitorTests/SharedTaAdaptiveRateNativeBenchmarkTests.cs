using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedTaAdaptiveRateNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Trix", "TaLib.Functions.Kama", "TaLib.Functions.Mavp" }
        let first = SharedTaAdaptiveRateNativeBenchmarks.RequiredLookback(operation)
        from count in new[] { 2, first - 1, first, first + 1, first + 2, first + 7, first + 8, first + 9, 8 * first - 1, 8 * first, 8 * first + 1, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativeAdaptiveRatePreservesArithmeticAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedTaAdaptiveRateNativeBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = Assert.Single(fixture.NativeSingle());
            var parallel = Assert.Single(fixture.NativeParallel());
            var expected = serial.Select(BitConverter.DoubleToInt64Bits).ToArray();
            Assert.Equal(Math.Max(0, count - (SharedTaAdaptiveRateNativeBenchmarks.RequiredLookback(operation))), serial.Length);
            Assert.Equal(expected, parallel.Select(BitConverter.DoubleToInt64Bits));
            foreach (var later in new[] { fixture.NativeSingle(), fixture.NativeParallel() })
            {
                var column = Assert.Single(later);
                Assert.NotSame(serial, column);
                Assert.NotSame(parallel, column);
                Assert.Equal(expected, column.Select(BitConverter.DoubleToInt64Bits));
                Array.Fill(column, double.NaN);
                Assert.Equal(expected, serial.Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(expected, parallel.Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryKeepsAllFourEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaAdaptiveRateNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
