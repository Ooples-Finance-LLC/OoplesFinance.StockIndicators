using System.Diagnostics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// A single verified, warmed workload for PerfView/EventPipe CPU sampling.
internal static class CpuPilotProfile
{
    internal static void Run(string id, string arm, int seconds)
    {
        if (seconds < 1 || seconds > 300) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (arm is not ("CpuBatch" or "CpuStreaming" or "Competitor" or "PublicApi"))
            throw new ArgumentOutOfRangeException(nameof(arm));
        var pair = ComparisonPairs.Get(id);
        var data = ComparisonVerifier.BenchmarkFixture(pair, 10_000);
        var kernel = CpuKernelPilots.Create(id);
        var output = new double[data.Count * kernel.OutputCount];
        CpuKernelPilots.Verify(id, data, kernel, output);
        ComparisonVerifier.Check(pair, ComparisonVerifier.BenchmarkFixture(pair, 160), 20, verifyIsolation: false);
        object? result = null;
        void Invoke()
        {
            if (arm == "Competitor") result = pair.Competitor(data, 20);
            else if (arm == "PublicApi") result = pair.Ooples(data, 20);
            else
            {
                kernel.Reset();
                if (arm == "CpuBatch") kernel.Process(data.IndicatorBars, output);
                else for (var i = 0; i < data.Count; i++)
                    kernel.Update(data.IndicatorBars[i], output.AsSpan(i * kernel.OutputCount, kernel.OutputCount));
            }
        }
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 2) Invoke();
        Console.WriteLine($"PROFILE START {id} {arm} PID={Environment.ProcessId}");
        timer.Restart();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var iterations = 0;
        do { Invoke(); iterations++; } while (timer.Elapsed.TotalSeconds < seconds);
        Console.WriteLine($"PROFILE END iterations={iterations} seconds={timer.Elapsed.TotalSeconds:F3} allocated={GC.GetAllocatedBytesForCurrentThread() - before}");
        GC.KeepAlive(result); GC.KeepAlive(output);
    }
}
