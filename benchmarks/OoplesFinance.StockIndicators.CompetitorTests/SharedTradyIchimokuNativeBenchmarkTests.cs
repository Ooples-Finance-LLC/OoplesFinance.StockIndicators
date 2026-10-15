using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedTradyIchimokuNativeBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(8)] [InlineData(9)] [InlineData(24)] [InlineData(25)]
    [InlineData(26)] [InlineData(27)] [InlineData(51)] [InlineData(52)] [InlineData(53)]
    [InlineData(76)] [InlineData(77)] [InlineData(78)] [InlineData(157)] [InlineData(158)]
    [InlineData(159)] [InlineData(1000)] [InlineData(10000)]
    public void ExplicitCoordinatesRetainEveryExtendedRow(int count)
    {
        var fixture = new SharedTradyIchimokuNativeBenchmarks { Count = count };
        try
        {
            fixture.Setup();
            var serial = fixture.NativeSingle();
            var parallel = fixture.NativeParallel();
            Assert.Equal(count + 51, serial.Count);
            Assert.Equal(serial, parallel);
            var later = fixture.NativeParallel();
            Assert.NotSame(parallel, later);
            Assert.Equal(parallel, later);
            Array.Clear(later);
            Assert.Equal(serial, parallel);
        }
        finally { fixture.Cleanup(); }
    }

    [Fact]
    public void DiscoveryRetainsThreeEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedTradyIchimokuNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(6, run.BenchmarksCases.Length);
        Assert.Equal(new[] { "NativeParallel", "NativeSingle", "Snapshot" },
            run.BenchmarksCases.Select(test => test.Descriptor.WorkloadMethod.Name).Distinct().OrderBy(name => name));
    }
}
