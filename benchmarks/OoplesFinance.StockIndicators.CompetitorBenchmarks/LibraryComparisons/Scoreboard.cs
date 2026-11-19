using System.Diagnostics;
using System.Globalization;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>
/// Times both arms of each comparison pair after warming them, and prints one line per pair and size.
/// Each reading is the best of many short batches, which is the least disturbed one on a busy machine.
/// </summary>
internal static class Scoreboard
{
    private const int Period = 20;

    // The reference oracles are exact and can take minutes a pair, so agreement is checked only on request.
    // LibraryPairBenchmarks always checks it before it times anything.
    private static readonly bool Verify = Environment.GetEnvironmentVariable("SCOREBOARD_VERIFY") == "1";

    internal static void Run(string? filter)
    {
        Console.WriteLine("pair,indicator,bars,ooples_us,competitor_us,ooples_over_competitor,status");
        var pairs = ComparisonPairs.All.OrderBy(pair => pair.Id, StringComparer.Ordinal)
            .Where(pair => filter is null || pair.Id.Contains(filter, StringComparison.Ordinal)).ToArray();
        // The first pair also pays for the builder and harness code every pair shares reaching its
        // optimised tier, so it is run once and discarded before anything is reported.
        if (pairs.Length > 0) _ = Line(pairs[0], 1_000);
        foreach (var pair in pairs)
        {
            foreach (var bars in new[] { 1_000, 10_000 })
                {
                Console.WriteLine(Line(pair, bars));
                Console.Out.Flush();
            }
        }
    }

    private static string Line(ComparisonPair pair, int bars)
    {
        var prefix = string.Join(",", pair.Id, pair.Indicator.Replace(',', ';'), bars.ToString(CultureInfo.InvariantCulture));
        try
        {
            var data = ComparisonVerifier.BenchmarkFixture(pair, bars);
            if (Verify) ComparisonVerifier.Check(pair, data, Period, verifyIsolation: false);
            var ooples = Best(() => pair.Ooples(data, Period));
            var competitor = Best(() => pair.Competitor(data, Period));
            return string.Create(CultureInfo.InvariantCulture, $"{prefix},{ooples:F2},{competitor:F2},{ooples / competitor:F3},ok");
        }
#pragma warning disable CA1031 // One failing pair is reported on its line; the rest still run.
        catch (Exception failure)
#pragma warning restore CA1031
        {
            var message = (failure.InnerException ?? failure).Message.Replace(',', ';').Replace((char)10, ' ').Replace((char)13, ' ');
            return prefix + ",,,," + failure.GetType().Name + ": " + (message.Length > 160 ? message[..160] : message);
        }
    }

    // Microseconds per call. The call count per batch is sized from one timed call so a slow arm still
    // finishes, and the warm-up is long enough for the tiered JIT to settle on short arms.
    private static double Best(Func<object> call)
    {
        var single = Stopwatch.StartNew();
        GC.KeepAlive(call());
        var first = single.Elapsed.TotalMilliseconds;
        var calls = (int)Math.Max(1, Math.Min(200, 2 / Math.Max(first, 0.001)));
        var warm = Stopwatch.StartNew();
        var warmed = 0;
        while (warm.ElapsedMilliseconds < 150 && warmed < 400 || warmed < 3)
        {
            GC.KeepAlive(call());
            warmed++;
            if (first > 500) break;
        }
        var best = double.MaxValue;
        var total = Stopwatch.StartNew();
        var batches = 0;
        while (total.ElapsedMilliseconds < 300 && batches < 400 || batches < 3)
        {
            var batch = Stopwatch.StartNew();
            for (var i = 0; i < calls; i++) GC.KeepAlive(call());
            best = Math.Min(best, batch.Elapsed.TotalMilliseconds * 1000 / calls);
            batches++;
            if (first > 500) break;
        }
        return best;
    }
}
