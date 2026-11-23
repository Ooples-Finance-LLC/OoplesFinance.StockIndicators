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
        Console.WriteLine("pair,indicator,bars,ooples_us,competitor_us,ooples_over_competitor,route,status,types");
        var pairs = ComparisonPairs.All.OrderBy(pair => pair.Id, StringComparer.Ordinal)
            .Where(pair => filter is null || pair.Id.Contains(filter, StringComparison.Ordinal)).ToArray();
        // The first pair also pays for the builder and harness code every pair shares reaching its
        // optimised tier, so it is run once and discarded before anything is reported.
        if (pairs.Length > 0) _ = Line(pairs[0], 1_000);
        foreach (var pair in pairs)
        {
            foreach (var bars in new[] { 1_000, 10_000 })
            {
                var line = Line(pair, bars);
                Console.WriteLine(line);
                Console.Out.Flush();
                // An arm that needs a quarter second a call at the small size would take minutes at the large one.
                if (Slow(line)) break;
            }
        }
    }

    private static bool Slow(string line)
    {
        var fields = line.Split(',');
        return fields.Length > 4
            && (double.TryParse(fields[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var ooples) && ooples > 250_000
                || double.TryParse(fields[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var competitor) && competitor > 250_000);
    }

    private static string Line(ComparisonPair pair, int bars)
    {
        var prefix = string.Join(",", pair.Id, pair.Indicator.Replace(',', ';'), bars.ToString(CultureInfo.InvariantCulture));
        try
        {
            var data = ComparisonVerifier.BenchmarkFixture(pair, bars);
            if (Verify) ComparisonVerifier.Check(pair, data, Period, verifyIsolation: false);
            ComparisonBuilder.ForgetLatest();
            var ooples = Best(() => pair.Ooples(data, Period));
            var reason = ComparisonBuilder.LatestRoute;
            var route = reason is null ? "none" : reason.StartsWith("Fast", StringComparison.Ordinal) ? "fast" : "exact";
            var types = ComparisonBuilder.LatestTypes;
            var competitor = Best(() => pair.Competitor(data, Period));
            return string.Create(CultureInfo.InvariantCulture,
                $"{prefix},{ooples:F2},{competitor:F2},{ooples / competitor:F3},{route},ok,{types}");
        }
#pragma warning disable CA1031 // One failing pair is reported on its line; the rest still run.
        catch (Exception failure)
#pragma warning restore CA1031
        {
            var message = (failure.InnerException ?? failure).Message.Replace(',', ';').Replace((char)10, ' ').Replace((char)13, ' ');
            return prefix + ",,,,," + failure.GetType().Name + ": " + (message.Length > 160 ? message[..160] : message);
        }
    }

    // Microseconds per call. The warm-up runs on the clock, not on a call count: the tiered JIT promotes a
    // method on a timer after it has been called enough, so a cheap arm called a few hundred times in a few
    // milliseconds would otherwise be measured unoptimised. The batch size is taken from a warm call.
    private static double Best(Func<object> call)
    {
        var single = Stopwatch.StartNew();
        GC.KeepAlive(call());
        var first = single.Elapsed.TotalMilliseconds;
        if (first > 500) return first * 1000;
        var warm = Stopwatch.StartNew();
        var warmed = 0;
        while (warm.ElapsedMilliseconds < 400 || warmed < 60)
        {
            GC.KeepAlive(call());
            warmed++;
        }
        single.Restart();
        GC.KeepAlive(call());
        var calls = (int)Math.Max(1, Math.Min(500, 1 / Math.Max(single.Elapsed.TotalMilliseconds, 0.0005)));
        var best = double.MaxValue;
        var total = Stopwatch.StartNew();
        var batches = 0;
        while (total.ElapsedMilliseconds < 400 || batches < 5)
        {
            var batch = Stopwatch.StartNew();
            for (var i = 0; i < calls; i++) GC.KeepAlive(call());
            best = Math.Min(best, batch.Elapsed.TotalMilliseconds * 1000 / calls);
            batches++;
        }
        return best;
    }
}
