using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class SharedAllocationProbeTests
{
    [Fact]
    public void MeasurementIncludesWorkerAllocationsAndReleasesOwnedResults()
    {
        var result = new OwnedResult();
        for (int i = 0; i < 10; i++)
            SharedAllocationProbe.Measure(() => Task.Run<object>(() => new byte[4096]).GetAwaiter().GetResult());
        long allocated = SharedAllocationProbe.Measure(() => Task.Run<object>(() =>
        {
            result.Values = new byte[4096];
            return result;
        }).GetAwaiter().GetResult());
        Assert.True(allocated >= 4096);
        Assert.True(result.Disposed);
        Assert.Equal(4096, result.Values!.Length);
    }

    [Theory]
    [InlineData("QuanTAlib.Historical")]
    [InlineData("QuanTAlib.Realized")]
    [InlineData("QuanTAlib.Dema")]
    [InlineData("QuanTAlib.Tema")]
    [InlineData("QuanTAlib.Qema")]
    [InlineData("TaLib.Functions.Beta")]
    [InlineData("TaLib.Functions.Rsi")]
    [InlineData("TaLib.Functions.Cmo")]
    public void SharedFixtureSetupAndAllFourOwnedResultArmsComplete(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 64, 2);
        Assert.Equal(operation, report.Operation);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" },
            report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm =>
        {
            Assert.Equal(2, arm.Bytes.Length);
            Assert.All(arm.Bytes, bytes => Assert.True(bytes >= 0));
        });
    }

    [Fact]
    public void InvalidRequestsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => SharedAllocationProbe.Run("unknown", 64, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SharedAllocationProbe.Run("QuanTAlib.Dema", 1, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SharedAllocationProbe.Run("QuanTAlib.Dema", 64, 0));
    }

    private sealed class OwnedResult : IDisposable
    {
        internal byte[]? Values;
        internal bool Disposed;
        public void Dispose() => Disposed = true;
    }
}
