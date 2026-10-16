using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("Shared benchmark settings")]
public sealed class TriangularBuilderBenchmarkTests
{
    [Theory]
    [InlineData("QuanTAlib.Trima")]
    [InlineData("TaLib.Functions.Trima")]
    public void SetupChecksBothHistoryModesAndNativeWorkers(string operation)
    {
        foreach (int count in new[] { 64, 513, 1000, 10000 })
        {
            var benchmark = new SharedTriangularBenchmarks { Operation = operation, Count = count, IncludeGpuValidation = false };
            try { benchmark.Setup(); }
            finally { benchmark.Cleanup(); }
        }
    }
}
