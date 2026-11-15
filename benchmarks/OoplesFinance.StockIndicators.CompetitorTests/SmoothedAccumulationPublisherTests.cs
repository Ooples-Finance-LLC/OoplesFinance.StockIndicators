using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SmoothedAccumulationPublisherTests
{
    [Fact]
    public void TaPublisherKeepsExactSingleColumnWithoutDiscardedColumnCopies()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 1;
            var prices = Enumerable.Repeat(1d, 10_000).ToArray();
            var data = CompetitorData.FromOhlcv(prices, prices, prices, prices, prices);
            ComparisonSeries Publish() => SmoothedAccumulationComparison.Owned(data.IndicatorBars, true, 3, 10);
            Publish(); Publish(); // Initialize shared zero storage and reflection-free builder metadata.
            long before = GC.GetAllocatedBytesForCurrentThread();
            ComparisonSeries actual = null!;
            for (int i = 0; i < 3; i++) actual = Publish();
            long bytes = (GC.GetAllocatedBytesForCurrentThread() - before) / 3;
            // Existing binaries allocate about2.10MB here, including0.96MB
            // for discarded columns. The corrected publisher needs about1.14MB;
            // retain substantial headroom and avoid a stopwatch assertion.
            Assert.True(bytes <= 1_700_000, $"Single-column publication allocated {bytes:N0} bytes.");
            Assert.Equal(new[] { "Oscillator" }, actual.Outputs.Keys);
            Equal(SmoothedAccumulationComparison.Reference(data, true, 3, 10, 0, false), actual);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public void SkenderPublisherRetainsEveryDetailColumnAndIndependentReferenceBit()
    {
        var data = CompetitorData.Create(137);
        var actual = SmoothedAccumulationComparison.Owned(data.IndicatorBars, false, 3, 10, 2);
        Assert.Equal(SmoothedAccumulationComparison.Names, actual.Outputs.Keys);
        Equal(SmoothedAccumulationComparison.Reference(data, false, 3, 10, 2, false), actual);
    }

    private static void Equal(ComparisonSeries expected, ComparisonSeries actual)
    {
        Assert.Equal(expected.Outputs.Keys, actual.Outputs.Keys);
        foreach (string name in expected.Outputs.Keys)
        {
            Assert.Equal(expected.Outputs[name].Values.Select(BitConverter.DoubleToInt64Bits),
                actual.Outputs[name].Values.Select(BitConverter.DoubleToInt64Bits));
            Assert.Equal(expected.Outputs[name].Present, actual.Outputs[name].Present);
        }
    }
}
