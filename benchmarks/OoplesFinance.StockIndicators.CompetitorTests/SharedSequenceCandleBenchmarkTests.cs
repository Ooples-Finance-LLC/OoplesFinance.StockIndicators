using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedSequenceCandleBenchmarkTests
{
    public static IEnumerable<object[]> Cases => from name in SequenceCandleComparison.Names
        from count in new[] { 2, 3, 4, 5, 6, 7, 8, 9, 65, 1000, 10000 } select new object[] { name, count };
    [Theory, MemberData(nameof(Cases))]
    public void IndependentDecisionsAndNativePartitionsMatchOwnedOutput(string name, int count)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        var fixture = new SharedSequenceCandleBenchmarks { Operation = "TaLib.Candles." + name, Count = count };
        try
        {
            fixture.Setup();
            Assert.Equal(count, Assert.IsType<int[]>(fixture.NativeSingle()).Length);
            Assert.Equal(count, Assert.IsType<int[]>(fixture.NativeParallel()).Length);
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSequenceCandleBenchmarks))));
        }
        finally { fixture.Cleanup(); }
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
    }
    [Theory]
    [InlineData("MatchingLow")] [InlineData("StickSandwich")] [InlineData("ThreeLineStrike")] [InlineData("ThreeOutside")]
    public void AllocationRouteRetainsFourArms(string name)
    {
        var report = SharedAllocationProbe.Run("TaLib.Candles." + name, 65, 2);
        Assert.Equal("TaLib.Candles." + name, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, a => Assert.Equal(2, a.Bytes.Length));
    }
}
