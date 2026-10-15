using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSkenderIchimokuNativeBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(8)] [InlineData(9)] [InlineData(25)]
    [InlineData(26)] [InlineData(27)] [InlineData(51)] [InlineData(52)] [InlineData(53)]
    [InlineData(76)] [InlineData(77)] [InlineData(78)] [InlineData(79)] [InlineData(207)]
    [InlineData(208)] [InlineData(209)] [InlineData(615)] [InlineData(616)] [InlineData(617)]
    [InlineData(1000)] [InlineData(10000)]
    public void ShiftedWindowsRetainAllNativeFieldsAndOwnedRows(int count)
    {
        var fixture = new SharedSkenderIchimokuNativeBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            var later = fixture.NativeParallel();
            Assert.Equal(count, parallel.Length);
            Assert.NotSame(parallel, later);
            for (int i = 0; i < count; i++)
            {
                Assert.Equal(serial[i].Date, parallel[i].Date);
                Assert.Equal(serial[i].TenkanSen, parallel[i].TenkanSen);
                Assert.Equal(serial[i].KijunSen, parallel[i].KijunSen);
                Assert.Equal(serial[i].SenkouSpanA, parallel[i].SenkouSpanA);
                Assert.Equal(serial[i].SenkouSpanB, parallel[i].SenkouSpanB);
                Assert.Equal(serial[i].ChikouSpan, parallel[i].ChikouSpan);
                Assert.NotSame(parallel[i], later[i]);
            }
            var expected = SharedSkenderIchimokuNativeBenchmarks.Normalize(parallel);
            Array.Clear(later);
            ComparisonVerifier.Compare(expected, SharedSkenderIchimokuNativeBenchmarks.Normalize(parallel), "retained Ichimoku", OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsThreeEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderIchimokuNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(6, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "NativeParallel", "NativeSingle", "Snapshot" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
