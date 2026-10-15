using OoplesFinance.StockIndicators.Indicators;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA directional settings")]
public sealed class SharedTaMultiBarNativeBenchmarkTests
{
    public static IEnumerable<object[]> Cases =>
        from operation in new[] { "Breakaway", "ConcealingBabySwallow", "LadderBottom", "MatHold", "RisingFallingThreeMethods", "Hikkake", "HikkakeModified", "TasukiGap", "Tristar", "UpDownSideGapThreeMethods" }
        let lookback = SharedTaMultiBarNativeBenchmarks.RequiredLookback(operation)
        from count in new[] { 1, 2, lookback - 1, lookback, lookback + 1, 8 * lookback - 1, 8 * lookback, 8 * lookback + 1, 513, 1000, 10000 }.Distinct()
        select new object[] { operation, count };

    [Theory, MemberData(nameof(Cases))]
    public void NativePrefixesMatchIndependentReferencesAndRetainResults(string operation, int count)
    {
        var fixture = new SharedTaMultiBarNativeBenchmarks { Operation = operation, Count = count };
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

    [Theory]
    [InlineData("Hikkake")]
    [InlineData("HikkakeModified")]
    public void GoldenConfirmationStateCrossesEveryWorkerBoundaryOffset(string operation)
    {
        foreach (var golden in HikkakeComparison.Cases.Where(c => c.Name == operation))
        for (int padding = 0; padding < 16; padding++)
        {
            var first = golden.Bars[0];
            var bars = Enumerable.Repeat(first, padding).Concat(golden.Bars)
                .Concat(Enumerable.Repeat(first, 128 - padding - golden.Bars.Length))
                .Select((b, i) => new Bar(DateTime.UnixEpoch.AddDays(i), b.Open, b.High, b.Low, b.Close, b.Volume)).ToArray();
            var fixture = new SharedTaMultiBarNativeBenchmarks { Operation = operation, Count = bars.Length };
            try
            {
                fixture.Setup(CrowSoldierComparison.FromBars(bars));
                // Padding shifts startup suppression: original pre-warmup zeros
                // can now publish formations. Setup checks those shifted rows
                // independently; the original golden suffix remains invariant.
                int warmup = SharedTaMultiBarNativeBenchmarks.RequiredLookback(operation);
                Assert.Equal(golden.Expected.Skip(warmup), fixture.NativeParallel()
                    .Skip(padding + warmup).Take(golden.Bars.Length - warmup).Select(v => (double)v));
            }
            finally { fixture.Cleanup(); }
        }
    }

    [Fact]
    public void FourEagerArmsCoverTenOperationsAndBothSizes()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTaMultiBarNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(80, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeParallel", "NativeSingle" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
