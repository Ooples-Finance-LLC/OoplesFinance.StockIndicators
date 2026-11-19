using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Slow competitor APIs can take over a minute for one 10k-bar invocation, so the complete
// benchmark is bounded at thirty minutes instead of the toolchain's five-minute default.
//
// The invocation count is left to the pilot stage, with a floor of 100 ms per iteration. A fixed
// count of one timed each arm on about its fifth call ever, which is the JIT's unoptimised first
// tier and one stopwatch reading of a call that can take microseconds: a measurement of start-up,
// not of the library. The pilot may settle on a single call: its default floor of four made a
// competitor call that takes over a minute run four times per iteration, which no longer fits the bound.
public sealed class LibraryTimingConfig : ManualConfig
{
    public LibraryTimingConfig()
    {
        AddJob(
            Job.ShortRun.WithToolchain(
                    new InProcessEmitToolchain(TimeSpan.FromMinutes(30), logOutput: true)
                )
                .WithUnrollFactor(1)
                .WithMinInvokeCount(1)
                .WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100))
        );
    }
}
