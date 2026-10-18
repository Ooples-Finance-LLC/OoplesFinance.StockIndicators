using System.Numerics;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedTaHilbertNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "TaLib.Functions.HtDcPhase", "TaLib.Functions.HtSine", "TaLib.Functions.HtTrendline", "TaLib.Functions.HtTrendMode" }
        from count in new[] { 2, 62, 63, 64, 65, 70, 71, 72, 503, 504, 505, 1000, 10000 }
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativeHilbertPrefixesMatchReferencesAndOwnColumns(string operation, int count)
    {
        if (operation == "TaLib.Functions.HtTrendMode")
            Verify(new SharedTaHilbertTrendModeBenchmarks { Operation = operation, Count = count }, 1);
        else
            Verify(new SharedTaHilbertNativeBenchmarks { Operation = operation, Count = count }, operation == "TaLib.Functions.HtSine" ? 2 : 1);
    }

    private static void Verify<T>(SharedTaPackedBenchmarkBase<T> fixture, int columns) where T : INumber<T>
    {
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            Assert.Equal(columns, serial.Length);
            Assert.Equal(columns, parallel.Length);
            var retained = serial.Select(c => c.Select(v => BitConverter.DoubleToInt64Bits(double.CreateChecked(v))).ToArray()).ToArray();
            var later = fixture.NativeParallel();
            for (int slot = 0; slot < columns; slot++)
            {
                Assert.Equal(Math.Max(0, fixture.Count - 63), serial[slot].Length);
                Assert.Equal(retained[slot], parallel[slot].Select(v => BitConverter.DoubleToInt64Bits(double.CreateChecked(v))));
                Assert.Equal(retained[slot], later[slot].Select(v => BitConverter.DoubleToInt64Bits(double.CreateChecked(v))));
                Assert.NotSame(serial[slot], parallel[slot]);
                Assert.NotSame(parallel[slot], later[slot]);
                Array.Fill(later[slot], T.CreateChecked(-999));
                Assert.Equal(retained[slot], serial[slot].Select(v => BitConverter.DoubleToInt64Bits(double.CreateChecked(v))));
                Assert.Equal(retained[slot], parallel[slot].Select(v => BitConverter.DoubleToInt64Bits(double.CreateChecked(v))));
            }
        }
        finally { fixture.Cleanup(); }
    }

    [Theory]
    [InlineData(typeof(SharedTaHilbertNativeBenchmarks), 24)]
    [InlineData(typeof(SharedTaHilbertTrendModeBenchmarks), 8)]
    public void DiscoveryRetainsFourEagerArms(Type fixture, int count)
    {
        var run = BenchmarkConverter.TypeToBenchmarks(fixture);
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(count, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
