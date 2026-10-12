using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;
using Xunit;

namespace OoplesFinance.StockIndicators.CompetitorTests;

[Collection("TA MACD settings")]
public sealed class SharedSmoothedAccumulationFixtureTests
{
    [Theory]
    [InlineData(1)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    [InlineData(17)] [InlineData(513)] [InlineData(1000)] [InlineData(10000)]
    public void IndependentReferencesAndNativePrefixReplayCoverStartupAndRestoreSettings(int count)
    {
        foreach (string operation in new[] { "Skender.GetChaikinOsc", "TaLib.Functions.AdOsc" })
        {
            int prior = CpuParallelSettings.MaxDegreeOfParallelism;
            using var settings = new TripleRateComparison.Settings(true, 5);
            var fixture = new SharedSmoothedAccumulationBenchmarks { Count = count, Operation = operation };
            try
            {
                CpuParallelSettings.MaxDegreeOfParallelism = 2;
                fixture.Setup(); fixture.Cleanup(); fixture.Cleanup();
                Assert.Equal(2, CpuParallelSettings.MaxDegreeOfParallelism);
                Assert.Equal(TALib.Core.CompatibilityMode.Metastock, TALib.Core.CompatibilitySettings.Get());
                Assert.Equal(5, TALib.Core.UnstablePeriodSettings.Get(TALib.Core.UnstableFunc.Ema));
            }
            finally { fixture.Cleanup(); CpuParallelSettings.MaxDegreeOfParallelism = prior; }
        }
    }

    [Fact]
    public void AllTimingArmsRetainEagerResults() =>
        Assert.Empty(BenchmarkDotNet.Validators.DeferredExecutionValidator.FailOnError.Validate(
            BenchmarkDotNet.Running.BenchmarkConverter.TypeToBenchmarks(typeof(SharedSmoothedAccumulationBenchmarks))));

    [Theory]
    [InlineData("Skender.GetChaikinOsc")] [InlineData("TaLib.Functions.AdOsc")]
    public void AllocationProbeRetainsAllFourArmsAndRejectsGpu(string operation)
    {
        var report = SharedAllocationProbe.Run(operation, 64, 2);
        Assert.Equal(new[] { "BuilderFull", "BuilderLatestOnly", "NativeSingle", "NativeParallel" }, report.Arms.Select(a => a.Method));
        Assert.All(report.Arms, arm =>
        {
            Assert.Equal(2, arm.Bytes.Length);
            Assert.All(arm.Bytes, bytes => Assert.True(bytes > 0));
        });
        Assert.Throws<ArgumentException>(() => SharedAllocationProbe.Run(operation, 64, 1, includeGpu: true));
    }

    [Fact]
    public void InvalidSetupLeavesSharedSettingsUntouched()
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        using var settings = new TripleRateComparison.Settings(true, 5);
        Assert.Throws<ArgumentException>(() => new SharedSmoothedAccumulationBenchmarks { Operation = "unknown", Count = 64 }.Setup());
        Assert.Throws<ArgumentOutOfRangeException>(() => new SharedSmoothedAccumulationBenchmarks { Count = 0 }.Setup());
        Assert.Equal(prior, CpuParallelSettings.MaxDegreeOfParallelism);
        Assert.Equal(TALib.Core.CompatibilityMode.Metastock, TALib.Core.CompatibilitySettings.Get());
        Assert.Equal(5, TALib.Core.UnstablePeriodSettings.Get(TALib.Core.UnstableFunc.Ema));
    }
}
