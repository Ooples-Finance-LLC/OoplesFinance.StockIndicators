using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// These native prefix controls can take minutes per invocation. Only the
// execution deadline differs from TensorsGpuTimingConfig; sampling is unchanged.
public sealed class SlowNativeTimingConfig : ManualConfig
{
    public SlowNativeTimingConfig() => AddJob(Job.ShortRun
        .WithToolchain(new InProcessEmitToolchain(TimeSpan.FromHours(2), true))
        .WithUnrollFactor(1).WithWarmupCount(3).WithIterationCount(5)
        .WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100)));
}
