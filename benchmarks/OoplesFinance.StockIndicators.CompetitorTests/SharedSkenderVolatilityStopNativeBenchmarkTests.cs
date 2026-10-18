using BenchmarkDotNet.Running;
using BenchmarkDotNet.Validators;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSkenderVolatilityStopNativeBenchmarkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(22)] [InlineData(23)] [InlineData(159)] [InlineData(160)] [InlineData(161)]
    [InlineData(1000)] [InlineData(10000)]
    public void NativePartitionsRetainAllFieldsAndOwnership(int count)
    {
        var fixture = new SharedSkenderVolatilityStopNativeBenchmarks { Count = count };
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
                Assert.Equal(serial[i].Sar, parallel[i].Sar);
                Assert.Equal(serial[i].UpperBand, parallel[i].UpperBand);
                Assert.Equal(serial[i].LowerBand, parallel[i].LowerBand);
                Assert.Equal(serial[i].IsStop, parallel[i].IsStop);
                Assert.NotSame(parallel[i], later[i]);
            }
            var expected = SharedSkenderVolatilityStopNativeBenchmarks.Normalize(parallel);
            Array.Clear(later);
            ComparisonVerifier.Compare(expected, SharedSkenderVolatilityStopNativeBenchmarks.Normalize(parallel), "retained stops", IndicatorErrorBudget.Exact);
        }
        finally { fixture.Cleanup(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FutureFirstReversalClearsEarlierPartitions(bool reverse)
    {
        var quotes = Enumerable.Range(0, 100).Select(i =>
        {
            decimal close = reverse && i >= 70 ? 200 - i : 100 + i;
            return new Quote { Date = new DateTime(2020, 1, 1).AddDays(i), Open = close, High = close + 1, Low = close - 1, Close = close, Volume = 100 };
        }).ToList();
        var full = quotes.GetVolatilityStop(20, 3).ToList();
        Assert.Equal(!reverse, full[20].IsStop.HasValue);
        foreach (int end in new[] { 19, 20, 21, 22, 69, 70, 71, 72, 99, 100 })
        {
            var rows = SharedSkenderVolatilityStopNativeBenchmarks.PrefixWithFinalStartup(quotes, end);
            ComparisonVerifier.Compare(SharedSkenderVolatilityStopNativeBenchmarks.Normalize(full.Take(end)),
                SharedSkenderVolatilityStopNativeBenchmarks.Normalize(rows.Take(end)), "future reversal", IndicatorErrorBudget.Exact);
        }
    }

    [Fact]
    public void DiscoveryRetainsThreeEagerArms()
    {
        var run = BenchmarkConverter.TypeToBenchmarks(typeof(SharedSkenderVolatilityStopNativeBenchmarks));
        Assert.Empty(DeferredExecutionValidator.FailOnError.Validate(run));
        Assert.Equal(6, run.BenchmarksCases.Length);
    }
}
