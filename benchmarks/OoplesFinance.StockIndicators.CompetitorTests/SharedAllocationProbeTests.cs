using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
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
    [InlineData("TaLib.Functions.LinearReg")]
    [InlineData("TaLib.Functions.LinearRegSlope")]
    [InlineData("TaLib.Functions.LinearRegIntercept")]
    [InlineData("TaLib.Functions.LinearRegAngle")]
    [InlineData("TaLib.Functions.Tsf")]
    [InlineData("Skender.GetCmo")]
    [InlineData("Trady.Indicator.EfficiencyRatio")]
    [InlineData("Skender.GetFisherTransform")]
    [InlineData("QuanTAlib.Rema")]
    [InlineData("QuanTAlib.Zlema")]
    [InlineData("QuanTAlib.Hwma")]
    [InlineData("QuanTAlib.Ltma")]
    [InlineData("QuanTAlib.Afirma")]
    [InlineData("QuanTAlib.Alma")]
    [InlineData("Skender.GetAlma")]
    [InlineData("QuanTAlib.Trima")]
    [InlineData("TaLib.Functions.Trima")]
    [InlineData("QuanTAlib.Slope")]
    [InlineData("Skender.GetSlope")]
    [InlineData("QuanTAlib.Historical")]
    [InlineData("QuanTAlib.Realized")]
    [InlineData("QuanTAlib.Dema")]
    [InlineData("QuanTAlib.Tema")]
    [InlineData("QuanTAlib.Qema")]
    [InlineData("TaLib.Functions.Beta")]
    [InlineData("TaLib.Functions.Correl")]
    [InlineData("TaLib.Functions.Cci")]
    [InlineData("TaLib.Functions.Mfi")]
    [InlineData("Skender.GetMfi")]
    [InlineData("TaLib.Functions.Rsi")]
    [InlineData("TaLib.Functions.Cmo")]
    [InlineData("TaLib.Functions.HtDcPeriod")]
    [InlineData("TaLib.Functions.HtPhasor")]
    [InlineData("TaLib.Functions.Mama")]
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

    [Theory]
    [InlineData("TaLib.Functions.LinearRegAngle")]
    [InlineData("TaLib.Functions.Rsi")]
    [InlineData("unknown")]
    [InlineData("TaLib.Functions.Cmo")]
    public void ExplicitGpuRequestRejectsUnsupportedOperations(string operation) =>
        Assert.Throws<ArgumentException>(() => SharedAllocationProbe.Run(operation, 64, 1, includeGpu: true));

    private sealed class OwnedResult : IDisposable
    {
        internal byte[]? Values;
        internal bool Disposed;
        public void Dispose() => Disposed = true;
    }
}
