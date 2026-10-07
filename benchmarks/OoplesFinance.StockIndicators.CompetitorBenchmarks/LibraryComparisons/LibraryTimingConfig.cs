using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Slow competitor APIs can take over a minute for one 10k-bar invocation.
// Keep the verified in-process binary, but bound the complete benchmark at
// thirty minutes instead of the toolchain's five-minute default. Each arm
// retains three warmups and three real measurements, with no hidden unrolling.
public sealed class LibraryTimingConfig : ManualConfig
{
    public LibraryTimingConfig()
    {
        AddJob(
            Job.ShortRun.WithToolchain(
                    new InProcessEmitToolchain(TimeSpan.FromMinutes(30), logOutput: true)
                )
                .WithInvocationCount(1)
                .WithUnrollFactor(1)
        );
    }
}
