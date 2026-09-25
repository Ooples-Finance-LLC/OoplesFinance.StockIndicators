using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

// A diagnostic queue, not an enrollment or release gate. Keep the validator's rules/budgets intact.
if (args.Length is < 2 or > 4)
    throw new ArgumentException("Usage: NumericalBacklogScanner backlog.txt results.jsonl [representatives|all] [workers=8]");
var mode = args.Length > 2 ? args[2] : "representatives";
if (mode is not ("representatives" or "all")) throw new ArgumentException("Unknown selection mode.");
var workers = args.Length > 3 ? int.Parse(args[3]) : Math.Min(8, Environment.ProcessorCount);
if (workers is < 1 or > 32) throw new ArgumentOutOfRangeException(nameof(workers));
var backlog = File.ReadAllLines(args[0]).Select(s => s.Trim()).Where(s => s.Length > 0 && !s.StartsWith('#')).ToArray();
if (backlog.Length == 0 || backlog.Distinct(StringComparer.Ordinal).Count() != backlog.Length)
    throw new ArgumentException("Backlog must contain unique, nonempty configuration names.");
var assembly = typeof(IIndicator).Assembly;
var discovered = IndicatorValidationDiscovery.Discover(new[] { assembly }).ToDictionary(c => c.ToString(), StringComparer.Ordinal);
var missing = backlog.Where(n => !discovered.ContainsKey(n)).ToArray();
if (missing.Length > 0) throw new ArgumentException("Unknown configurations: " + string.Join(", ", missing));
var selected = backlog.Select(n => discovered[n]);
if (mode == "representatives") selected = selected.GroupBy(c => c.IndicatorType)
    .Select(g => g.FirstOrDefault(c => c.Name == "default") ?? g.OrderBy(c => c.Name, StringComparer.Ordinal).First());
var work = selected.OrderBy(c => c.ToString(), StringComparer.Ordinal).ToArray();
IndicatorValidationFixture[] Fixtures(int count) => IndicatorAdversarialCases.Generate(count, 244)
    .SelectMany(f => new[]
    {
        new IndicatorValidationFixture("backlog-scan/" + f.Name, f.Bars),
        // Exercise daily, weekly, monthly and yearly aggregations, which can otherwise stay at startup.
        new IndicatorValidationFixture("backlog-scan-calendar/" + f.Name, f.Bars.Select((b, i) =>
            new Bar(b.Time.AddDays(7d * i), b.Open, b.High, b.Low, b.Close, b.Volume)))
    }).ToArray();
var fixtures = Fixtures(256);
string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
var header = JsonSerializer.Serialize(new
{
    kind = "scan", version = 3, purpose = "diagnostic-only", mode,
    assemblySha256 = Hash(assembly.Location), scannerSha256 = Hash(typeof(Program).Assembly.Location),
    selection = work.Select(c => c.ToString()).ToArray(),
    fixtureNames = fixtures.Select(f => f.Name).ToArray(), minimumBars = 256, postWarmupBars = 32, maximumBars = 8192, calendarDayStride = 7, seed = 244,
    requireFormulaReference = true, requireMathematicalContract = true
});
var completed = new HashSet<string>(StringComparer.Ordinal);
var passing = 0;
if (File.Exists(args[1]))
{
    using var reader = File.OpenText(args[1]);
    if (reader.ReadLine() != header) throw new InvalidOperationException("Evidence belongs to a different scanner, assembly, selection or fixture policy. Use a new output file.");
    string? line;
    while ((line = reader.ReadLine()) is not null)
    {
        using var document = JsonDocument.Parse(line);
        var entry = document.RootElement;
        var name = entry.GetProperty("name").GetString()!;
        if (entry.GetProperty("kind").GetString() != "result" || !work.Any(c => c.ToString() == name) || !completed.Add(name))
            throw new InvalidOperationException("Evidence contains duplicate or unselected results.");
        if (entry.GetProperty("passed").GetBoolean()) passing++;
    }
}
using var output = new StreamWriter(args[1], append: true) { AutoFlush = true };
if (completed.Count == 0 && new FileInfo(args[1]).Length == 0) output.WriteLine(header);
var pending = work.Where(c => !completed.Contains(c.ToString())).ToArray();
using var stop = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop.Cancel(); };
var gate = new object();
var next = -1;
Console.WriteLine($"{work.Length} selected; {completed.Count} resumed; {workers} workers. No enrollment changes will be made.");
await Task.WhenAll(Enumerable.Range(0, workers).Select(_ => Task.Run(async () =>
{
    int index;
    while (!stop.IsCancellationRequested && (index = Interlocked.Increment(ref next)) < pending.Length)
    {
        var testCase = pending[index];
        var watch = Stopwatch.StartNew();
        IndicatorValidationReport? report = null;
        string? error = null;
        IReadOnlyList<IndicatorValidationFixture> caseFixtures = Array.Empty<IndicatorValidationFixture>();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
        timeout.CancelAfter(TimeSpan.FromMinutes(3)); // Cooperative, not a hard process time limit.
        try
        {
            var probe = testCase.Factory();
            using var probeLifetime = probe as IDisposable;
            var warmup = probe.WarmupBars;
            if (warmup < 0 || warmup > 8192 - 32) throw new InvalidOperationException("Warmup exceeds scanner budget.");
            caseFixtures = Fixtures(Math.Max(256, warmup + 32));
            report = await IndicatorValidation.ValidateAsync(testCase, new IndicatorValidationOptions
            {
                AdditionalFixtures = caseFixtures,
                RequireFormulaReference = true,
                RequireMathematicalContract = true
            }, timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { break; }
        catch (Exception ex) { error = ex.ToString(); }
        var exercised = report is not null && caseFixtures.Count == fixtures.Length && caseFixtures.All(f => report.FixtureEvidence.Any(e =>
            e.Name == f.Name && e.InputBars == f.Bars.Count && e.Completed && e.Passed));
        var passed = error is null && report?.IsValid == true && exercised;
        var result = JsonSerializer.Serialize(new
        {
            kind = "result", name = testCase.ToString(), passed, numericalFixturesPassed = exercised,
            fixtureBars = caseFixtures.FirstOrDefault()?.Bars.Count,
            seconds = watch.Elapsed.TotalSeconds, error,
            failures = report?.Failures, fixtures = report?.FixtureEvidence,
            coverage = report?.FormulaCoverage
        });
        lock (gate)
        {
            output.WriteLine(result);
            completed.Add(testCase.ToString());
            if (passed) passing++;
            Console.WriteLine($"{completed.Count}/{work.Length}: {(passed ? "candidate" : "needs-work")} {testCase} ({watch.Elapsed.TotalSeconds:F1}s)");
        }
    }
})));
Console.WriteLine($"{completed.Count}/{work.Length} completed; {passing} candidates; {completed.Count - passing} need work. Candidates still require formula/route review and fault checks.");
return stop.IsCancellationRequested || completed.Count != work.Length ? 2 : passing == work.Length ? 0 : 1;
