using System.Diagnostics;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Profiles the exact 100k-bar benchmark arms; fixture verification stays outside measured counters.
internal static class PilotCostBoundaryProfile
{
    internal static void Run(string scenario, string arm, int seconds)
    {
        if (scenario is not ("Asin" or "SmaGrid" or "SmaDecimal")) throw new ArgumentOutOfRangeException(nameof(scenario));
        if (seconds is < 1 or > 60) throw new ArgumentOutOfRangeException(nameof(seconds));
        var benchmark = new PilotCostBoundaryBenchmarks { Case = scenario };
        object? result = null;
        Action invoke = arm switch
        {
            "Builder" => () => result = benchmark.OoplesLatestOnlyBuilder(),
            "Talib" => () => result = benchmark.TalibValues(),
            "InPlace" => () => result = benchmark.TalibInPlaceLatestOnlyPayload(),
            "Adapter" => () => result = benchmark.TalibLatestOnlyPayload(),
            "Core" => () => result = benchmark.OoplesValues(),
            _ => throw new ArgumentOutOfRangeException(nameof(arm))
        };
        benchmark.Setup();
        using var process = Process.GetCurrentProcess();
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 3) invoke();
        Console.WriteLine($"PROFILE START case={scenario} arm={arm} pid={Environment.ProcessId}");
        var before = GC.GetTotalAllocatedBytes(true);
        var pauses = GC.GetTotalPauseDuration();
        var cpu = process.TotalProcessorTime;
        var collections = new[] { GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2) };
        int iterations = 0;
        timer.Restart();
        do { invoke(); iterations++; } while (timer.Elapsed.TotalSeconds < seconds);
        var elapsed = timer.Elapsed.TotalSeconds;
        var cpuSeconds = (process.TotalProcessorTime - cpu).TotalSeconds;
        var pauseSeconds = (GC.GetTotalPauseDuration() - pauses).TotalSeconds;
        var bytes = GC.GetTotalAllocatedBytes(true) - before;
        Console.WriteLine($"PROFILE END iterations={iterations} seconds={elapsed:F4} nsPerCall={elapsed * 1e9 / iterations:F0} bytesPerCall={(double)bytes / iterations:F0} cpuSeconds={cpuSeconds:F4} gcPauseSeconds={pauseSeconds:F4} gen0={GC.CollectionCount(0)-collections[0]} gen1={GC.CollectionCount(1)-collections[1]} gen2={GC.CollectionCount(2)-collections[2]}");
        GC.KeepAlive(result);
    }
}
