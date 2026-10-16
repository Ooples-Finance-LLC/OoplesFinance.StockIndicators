using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedTaAverageNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Ma", "TaLib.Functions.Apo", "TaLib.Functions.Ppo" }
        let first = operation == "TaLib.Functions.Ma" ? 19 : 25
        from count in new[] { 2, first - 1, first, first + 1, first + 2, first + 7, first + 8, first + 9, 8 * first - 1, 8 * first, 8 * first + 1, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativeAveragePreservesArithmeticAndIndependentResults(string operation, int count)
    {
        var fixture = new SharedTaAverageNativeBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var serial = Assert.Single(fixture.NativeSingle());
            var parallel = Assert.Single(fixture.NativeParallel());
            var expected = serial.Select(BitConverter.DoubleToInt64Bits).ToArray();
            Assert.Equal(Math.Max(0, count - (operation == "TaLib.Functions.Ma" ? 19 : 25)), serial.Length);
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
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaAverageNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
