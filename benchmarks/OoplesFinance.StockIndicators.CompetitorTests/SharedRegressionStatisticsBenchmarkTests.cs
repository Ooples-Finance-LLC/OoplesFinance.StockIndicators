using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedRegressionStatisticsBenchmarkTests
{
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(19)] [InlineData(20)]
    [InlineData(21)] [InlineData(39)] [InlineData(40)] [InlineData(127)]
    public void SetupChecksStartupGlobalCoordinatesAndFinalOverlayAcrossWorkers(int count)
    {
        foreach (string operation in new[] { "QuanTAlib.Slope", "Skender.GetSlope" })
        {
            int prior = CpuParallelSettings.MaxDegreeOfParallelism;
            var fixture = new SharedRegressionStatisticsBenchmarks { Operation = operation, Count = count };
            try { fixture.Setup(); }
            finally { fixture.Cleanup(); }
            Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
        }
    }
}
