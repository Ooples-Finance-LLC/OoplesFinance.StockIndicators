using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;

if (args.Length == 3 && args[0] == "--verify-cpu-native")
{
    var count = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    new CpuNativeWorkload(args[1], count).Verify();
    Console.WriteLine($"Verified direct native and Ooples outputs: {args[1]}, {count} bars.");
    return;
}

if (args.Length == 4 && args[0] == "--profile-cpu-pilot")
{
    CpuPilotProfile.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
    return;
}

if (args.Length == 2 && args[0] == "--comparison-manifest")
{
    File.WriteAllText(args[1], System.Text.Json.JsonSerializer.Serialize(ComparisonManifest.Create(),
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }) + "\n");
    return;
}

if (args.Length == 4 && args[0] == "--verify-library-shard")
{
    var shard = int.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture);
    var total = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    if (total < 1 || shard < 0 || shard >= total) throw new ArgumentOutOfRangeException(nameof(args));
    var results = ComparisonPairs.All.OrderBy(pair => pair.Id, StringComparer.Ordinal)
        .Where((_, index) => index % total == shard)
        .Select(pair =>
        {
            Console.WriteLine($"Shard {shard}/{total}: checking {pair.Id}...");
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var checkedValues = ComparisonVerifier.Verify(pair);
            Console.WriteLine($"Shard {shard}/{total}: {pair.Id} passed {checkedValues} values in {timer.Elapsed.TotalSeconds:F1}s.");
            return new { pair.Id, CheckedValues = checkedValues };
        }).ToArray();
    File.WriteAllText(args[3], System.Text.Json.JsonSerializer.Serialize(new { Shard = shard, Total = total, Results = results }));
    Console.WriteLine($"Shard {shard}/{total}: verified {results.Length} comparison pairs.");
    return;
}

if (args.Length > 0 && args[0] == "--api-catalog")
{
    var json = System.Text.Json.JsonSerializer.Serialize(CompetitorApiCatalog.Discover(),
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
    if (args.Length == 2) File.WriteAllText(args[1], json + "\n");
    else Console.WriteLine(json);
    return;
}

if (args.Length > 0 && args[0] == "--verify-library-pairs")
{
    foreach (var pair in ComparisonPairs.All)
        Console.WriteLine($"{pair.Id}: {ComparisonVerifier.Verify(pair)} independently checked values");
    return;
}

// These checks run without BenchmarkDotNet and do not produce timings:
//   --coverage  what each library ships, so a missing row is never mistaken for a slow one
//   --verify    what each library computes, so the timings are known to be over the same arithmetic
if (args.Length > 0 && args[0].Equals("--coverage", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(CompetitorCoverage.ToMarkdown());
    return;
}

if (args.Length > 0 && args[0].Equals("--verify", StringComparison.OrdinalIgnoreCase))
{
    AgreementCheck.Run(Console.Out);
    return;
}

if (args.Length > 0 && args[0].Equals("--verify-workloads", StringComparison.OrdinalIgnoreCase))
{
    WorkloadCheck.Run(Console.Out);
    return;
}

if (args.Length > 0 && args[0].Equals("--alloc", StringComparison.OrdinalIgnoreCase))
{
    AllocProbe.Run(Console.Out);
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(BatchBenchmarks).Assembly).Run(args);
