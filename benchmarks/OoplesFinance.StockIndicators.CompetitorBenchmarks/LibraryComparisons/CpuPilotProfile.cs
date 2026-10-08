using System.Diagnostics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// A single verified, warmed workload for PerfView/EventPipe CPU sampling.
internal static class CpuPilotProfile
{
    internal static void Run(string id, string arm, int seconds)
    {
        if (seconds < 1 || seconds > 300) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (arm is not ("OoplesOwnedBatch" or "CompetitorOwnedBatch" or "OoplesReusableBatch" or "CompetitorReusableBatch"))
            throw new ArgumentOutOfRangeException(nameof(arm));
        var work = new CpuNativeWorkload(id, 10_000);
        if (arm.Contains("Reusable", StringComparison.Ordinal) && !CpuNativeWorkload.SupportsReusable(id))
            throw new NotSupportedException(id + " has no reusable native batch API.");
        work.Verify();
        var kernel = CpuKernelPilots.Create(id);
        var output = new double[work.Data.Count * kernel.OutputCount];
        object? result = null;
        void Invoke()
        {
            if (arm == "CompetitorOwnedBatch") result = work.NativeOwned();
            else if (arm == "OoplesOwnedBatch") result = work.OoplesOwned();
            else if (arm == "CompetitorReusableBatch") result = work.NativeReusable();
            else
            {
                if (id == "TaLib.Functions.Asin") IndicatorKernels.Asin(work.Data.Closes, output);
                else { kernel.Reset(); kernel.Process(work.Data.IndicatorBars, output); }
                result = output;
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
