using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA strength settings")]
public sealed class SharedTaStochasticNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.Stoch", "TaLib.Functions.StochF", "TaLib.Functions.StochRsi" }
        let first = SharedTaStochasticNativeBenchmarks.RequiredLookback(operation)
        from count in new[] { 2, first - 1, first, first + 1, first + 2, first + 7, first + 8, first + 9, 8 * first - 1, 8 * first, 8 * first + 1, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativeKAndDMatchReferencesAcrossWarmupAndWorkerBoundaries(string operation, int count)
    {
        var fixture = new SharedTaStochasticNativeBenchmarks { Operation = operation, Count = count };
        try
        {
            fixture.Setup();
            var results = new[] { fixture.NativeSingle(), fixture.NativeParallel(), fixture.NativeSingle(), fixture.NativeParallel() };
            foreach (var columns in results)
            {
                Assert.Equal(2, columns.Length);
                Assert.NotSame(columns[0], columns[1]);
                foreach (var column in columns)
                    Assert.Equal(Math.Max(0, count - SharedTaStochasticNativeBenchmarks.RequiredLookback(operation)), column.Length);
            }
            for (int slot = 0; slot < 2; slot++)
            {
                var expected = results[0][slot].Select(BitConverter.DoubleToInt64Bits).ToArray();
                foreach (var columns in results.Skip(1))
                {
                    Assert.NotSame(results[0][slot], columns[slot]);
                    Assert.Equal(expected, columns[slot].Select(BitConverter.DoubleToInt64Bits));
                }
                Array.Fill(results[2][slot], double.NaN);
                Array.Fill(results[3][slot], double.NaN);
                Assert.Equal(expected, results[0][slot].Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(expected, results[1][slot].Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryIncludesBothNativeArmsAndBuilderHistories()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaStochasticNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(24, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
