using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedPenetrationBenchmarkTests(ITestOutputHelper log)
{
    [Theory]
    [InlineData("PiercingLine", 1000)] [InlineData("PiercingLine", 10000)]
    [InlineData("DarkCloudCover", 1000)] [InlineData("DarkCloudCover", 10000)]
    public void OriginalBenchmarkInputsMatchReferenceAndSavedNativeContract(string name, int count)
    {
        var fixture = new SharedPenetrationBenchmarks { Operation = "TaLib.Candles." + name, Count = count };
        try
        {
            fixture.Setup();
            Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
                BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedPenetrationBenchmarks))));
            using var full = fixture.BuilderFull();
            using var latest = fixture.BuilderLatestOnly();
            if (count == 10000)
            {
                // Include worker allocations; exclude retained pools and fixture preparation.
                for (int i = 0; i < 3; i++)
                {
                    long before = GC.GetTotalAllocatedBytes(true);
                    using var f = fixture.BuilderFull();
                    long fullBytes = GC.GetTotalAllocatedBytes(true) - before;
                    before = GC.GetTotalAllocatedBytes(true);
                    using var l = fixture.BuilderLatestOnly();
                    long latestBytes = GC.GetTotalAllocatedBytes(true) - before;
                    log.WriteLine($"{name}; sample={i}; Full={fullBytes}; Latest={latestBytes}; retained pools excluded");
                }
            }
        }
        finally { fixture.Cleanup(); }
    }
}
