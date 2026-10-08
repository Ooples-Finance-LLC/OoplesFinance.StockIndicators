using System.Diagnostics;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Uses the same complete lifecycle and common-grid inputs as CpuBuilderBenchmarks.
internal static class CpuBuilderProfile
{
    internal static void Run(string id, string arm, int seconds)
    {
        if (seconds is < 1 or > 300) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (arm is not ("Builder" or "Native")) throw new ArgumentOutOfRangeException(nameof(arm));
        if (!CpuBuilderWorkload.PairIds.Contains(id)) throw new ArgumentOutOfRangeException(nameof(id));
        var benchmark = new CpuBuilderBenchmarks { PairId = id, Bars = 10_000 };
        benchmark.Setup();
        object? result = null;
        void Invoke()
        {
            if (arm == "Builder") benchmark.OoplesBuilderBatch().GetAwaiter().GetResult();
            else result = benchmark.CompetitorNativeBatch();
        }
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 2) Invoke();
        Console.WriteLine($"PROFILE START {id} {arm} PID={Environment.ProcessId}");
        timer.Restart();
        var before = GC.GetTotalAllocatedBytes(precise: true);
        var iterations = 0;
        do { Invoke(); iterations++; } while (timer.Elapsed.TotalSeconds < seconds);
        var elapsed = timer.Elapsed.TotalSeconds;
        var allocated = GC.GetTotalAllocatedBytes(precise: true) - before;
        Console.WriteLine($"PROFILE END iterations={iterations} seconds={elapsed:F3} allocated={allocated} bytesPerCall={(double)allocated / iterations:F0}");
        GC.KeepAlive(result);
    }
}
