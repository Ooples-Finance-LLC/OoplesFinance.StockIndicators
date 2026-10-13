using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedRangeDojiBenchmarkTests
{
    public static IEnumerable<object[]> Cases => from name in AverageRangeDojiComparison.Names
        from count in new[] { 2, 10, 11, 12, 65, 1000, 10000 } select new object[] { name, count };
    [Theory, MemberData(nameof(Cases))]
    public void IndependentDecisionsAndNativePartitionsMatchOwnedOutput(string name, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedRangeDojiBenchmarks { Operation = "TaLib.Candles." + name, Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal(count, Assert.IsType<int[]>(fixture.NativeSingle()).Length);
            Assert.Equal(count, Assert.IsType<int[]>(fixture.NativeParallel()).Length);
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedRangeDojiBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Theory]
    [InlineData("Doji")] [InlineData("DragonflyDoji")] [InlineData("GravestoneDoji")]
    [InlineData("LongLeggedDoji")] [InlineData("TakuriLine")]
    public void AllocationRouteRetainsFourArms(string name)
    {
        var report = SharedAllocationProbe.Run("TaLib.Candles." + name, 65, 2);
        Assert.Equal("TaLib.Candles." + name, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, a => Assert.Equal(2, a.Bytes.Length));
    }
}
