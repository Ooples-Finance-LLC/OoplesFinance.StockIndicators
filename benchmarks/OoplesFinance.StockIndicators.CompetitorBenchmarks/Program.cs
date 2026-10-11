using BenchmarkDotNet.Running;
using OoplesFinance.StockIndicators.CompetitorBenchmarks;

if (args.Length == 4 && args[0] == "--profile-hilbert")
{
    var count = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    var iterations = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
    if (count < 64 || iterations < 1 || args[1] is not
        ("TaLib.Functions.HtDcPeriod" or "TaLib.Functions.HtPhasor" or "TaLib.Functions.Mama"))
        throw new ArgumentException("Expected a supported Hilbert operation, at least 64 bars, and positive iterations.");
    var benchmark = new SharedHilbertBenchmarks { Operation = args[1], Count = count };
    try
    {
        benchmark.Setup();
        for (int i = 0; i < 100; i++) benchmark.BuilderLatestOnly().Dispose();
        Console.WriteLine("Hilbert warmup complete; starting steady-state builder loop.");
        for (int i = 0; i < iterations; i++) benchmark.BuilderLatestOnly().Dispose();
    }
    finally { benchmark.Cleanup(); }
    return;
}

if (args.Length == 4 && args[0] == "--profile-strength")
{
    var count = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    var iterations = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
    if (count < 64 || iterations < 1 || args[1] is not ("TaLib.Functions.Rsi" or "TaLib.Functions.Cmo"))
        throw new ArgumentException("Expected RSI/CMO, at least 64 bars, and a positive iteration count.");
    var benchmark = new SharedWilderStrengthBenchmarks { Operation = args[1], Count = count };
    try
    {
        benchmark.Setup();
        for (int i = 0; i < 100; i++) benchmark.BuilderLatestOnly().Dispose();
        Console.WriteLine("Strength warmup complete; starting steady-state builder loop.");
        for (int i = 0; i < iterations; i++) benchmark.BuilderLatestOnly().Dispose();
    }
    finally { benchmark.Cleanup(); }
    return;
}

if (args.Length == 4 && args[0] == "--profile-directional")
{
    var count = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    var iterations = int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture);
    if (count < 64 || iterations < 1 || !TaDirectionalComparison.Names.Contains(args[1]))
        throw new ArgumentException("Expected a directional API name, at least 64 bars, and a positive iteration count.");
    var benchmark = new SharedTaDirectionalBenchmarks { Operation = "TaLib.Functions." + args[1], Count = count };
    try
    {
        benchmark.Setup();
        for (int i = 0; i < 100; i++) benchmark.BuilderLatestOnly().Dispose();
        Console.WriteLine("Directional warmup complete; starting steady-state builder loop.");
        for (int i = 0; i < iterations; i++) benchmark.BuilderLatestOnly().Dispose();
    }
    finally { benchmark.Cleanup(); }
    return;
}

if (args.Length == 4 && args[0] == "--profile-cost-boundary")
{
    PilotCostBoundaryProfile.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
    return;
}

if (args.Length == 1 && args[0] == "--probe-gpu-scheduling")
{
    await PilotGpuSchedulingProbe.Run();
    return;
}

if (args.Length == 2 && args[0] == "--verify-sma-baseline")
{
    PilotCoreReplay.Verify(args[1]);
    return;
}

if (args.Length == 3 && args[0] == "--verify-cpu-native")
{
    var count = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    new CpuNativeWorkload(args[1], count).Verify();
    Console.WriteLine($"Verified direct native and Ooples outputs: {args[1]}, {count} bars.");
    return;
}

if (args.Length == 4 && args[0] == "--profile-builder")
{
    CpuBuilderProfile.Run(args[1], args[2], int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
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

if (args.Length == 4 && args[0] == "--alloc-shared")
{
    SharedAllocationProbe.Write(Console.Out, args[1],
        int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture),
        int.Parse(args[3], System.Globalization.CultureInfo.InvariantCulture));
    return;
}

if (args.Length > 0 && args[0].Equals("--alloc", StringComparison.OrdinalIgnoreCase))
{
    AllocProbe.Run(Console.Out);
    return;
}

// Discover only this suite: other suites interpret COMPARISON_PAIR against the
// library manifest, which intentionally does not duplicate tuple API routes.
if (args.Contains("*SmaFeasibilityBenchmarks*", StringComparer.Ordinal))
    BenchmarkSwitcher.FromTypes([typeof(SmaFeasibilityBenchmarks)]).Run(args);
else if (args.Any(arg => arg.Contains("AsinOwnershipFeasibilityBenchmarks", StringComparison.Ordinal)))
    BenchmarkSwitcher.FromTypes([typeof(AsinOwnershipFeasibilityBenchmarks)]).Run(args);
else if (args.Contains("*FeasibilityBenchmarks*", StringComparer.Ordinal))
    BenchmarkSwitcher.FromTypes([typeof(AsinFeasibilityBenchmarks), typeof(SmaFeasibilityBenchmarks), typeof(AsinOwnershipFeasibilityBenchmarks)]).Run(args);
else if (args.Contains("*CpuBuilderBenchmarks*", StringComparer.Ordinal))
    BenchmarkSwitcher.FromTypes([typeof(CpuBuilderBenchmarks)]).Run(args);
else
    BenchmarkSwitcher.FromAssembly(typeof(BatchBenchmarks).Assembly).Run(args);
