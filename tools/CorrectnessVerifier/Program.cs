using System.Security.Cryptography;
using System.Xml;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

// Standalone public-API consumer: the same executable can validate a source build or an exact NuGet package.
// Evidence describes executed contracts, not an assertion that all mathematical specifications were reviewed.
var shardIndex = -1;
var shardCount = 0;
if (args.Length == 5 && args[1] == "--shard-index" && args[3] == "--shard-count")
{
    shardIndex = int.Parse(args[2], System.Globalization.CultureInfo.InvariantCulture);
    shardCount = int.Parse(args[4], System.Globalization.CultureInfo.InvariantCulture);
    if (shardCount < 1 || shardIndex < 0 || shardIndex >= shardCount)
        throw new ArgumentException("Invalid shard index/count.");
}
else if (args.Length > 2 || args.Skip(1).Any(a => a.StartsWith("--", StringComparison.Ordinal)))
    throw new ArgumentException("Usage: CorrectnessVerifier [output.xml] [comma-separated configuration filters], or output.xml --shard-index I --shard-count N.");
var output = args.Length > 0 ? args[0] : "correctness-evidence.xml";
var filters = args.Length == 2 ? args[1].Split(',') : Array.Empty<string>();
var assembly = typeof(IIndicator).Assembly;
var cases = IndicatorValidationDiscovery.Discover(new[] { assembly });
var pairs = IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { assembly });
var inventory = cases.Select(c => (Name: c.ToString(), Run: (Func<Task<IndicatorValidationReport>>)(() => IndicatorValidation.ValidateAsync(c))))
    .Concat(pairs.Select(c => (Name: c.ToString(), Run: (Func<Task<IndicatorValidationReport>>)(() => Task.FromResult(MultiSeriesIndicatorValidation.Validate(c))))))
    .OrderBy(c => c.Name, StringComparer.Ordinal).ToArray();
if (inventory.Select(c => c.Name).Distinct(StringComparer.Ordinal).Count() != inventory.Length)
    throw new InvalidOperationException("Duplicate discovered configuration names.");
var work = inventory.Where((_, index) => shardCount == 0 || index % shardCount == shardIndex)
    .Where(c => filters.Length == 0 || filters.Any(f => c.Name.IndexOf(f, StringComparison.Ordinal) >= 0)).ToArray();
if (work.Length == 0) throw new InvalidOperationException("No configurations selected; an empty run cannot pass.");
var reports = new IndicatorValidationReport?[work.Length];
var errors = new string?[work.Length];
var next = -1;
await Task.WhenAll(Enumerable.Range(0, Math.Min(8, Environment.ProcessorCount)).Select(_ => Task.Run(async () =>
{
    int index;
    while ((index = Interlocked.Increment(ref next)) < work.Length)
    {
        try { reports[index] = await work[index].Run().ConfigureAwait(false); }
        catch (Exception ex) { errors[index] = ex.ToString(); }
    }
})));
using (var writer = XmlWriter.Create(output, new XmlWriterSettings { Indent = true }))
{
    writer.WriteStartElement("correctnessEvidence");
    writer.WriteAttributeString("scope", shardCount > 0 ? "shard" : filters.Length == 0 ? "all-discovered-configurations" : "filtered");
    if (shardCount > 0)
    {
        writer.WriteAttributeString("shardIndex", shardIndex.ToString());
        writer.WriteAttributeString("shardCount", shardCount.ToString());
    }
    writer.WriteAttributeString("requiredNumericalFixtures", string.Join(",",
        IndicatorAdversarialCases.Generate(2, 244).Select(fixture => fixture.Name)));
    writer.WriteAttributeString("assembly", assembly.FullName);
    using (var sha = SHA256.Create())
    using (var binary = File.OpenRead(assembly.Location))
        writer.WriteAttributeString("assemblySha256", BitConverter.ToString(sha.ComputeHash(binary)).Replace("-", ""));
    writer.WriteAttributeString("sourceRevision", Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "local-uncommitted");
    writer.WriteAttributeString("runtime", Environment.Version.ToString());
    writer.WriteAttributeString("os", Environment.OSVersion.ToString());
    writer.WriteAttributeString("pointerBits", (IntPtr.Size * 8).ToString());
    writer.WriteAttributeString("utc", DateTime.UtcNow.ToString("O"));
    if (shardCount > 0)
    {
        writer.WriteStartElement("inventory");
        foreach (var item in inventory) writer.WriteElementString("name", item.Name);
        writer.WriteEndElement();
    }
    for (var i = 0; i < work.Length; i++)
    {
        var report = reports[i];
        writer.WriteStartElement("case");
        writer.WriteAttributeString("name", work[i].Name);
        writer.WriteAttributeString("passed", (report?.IsValid == true && errors[i] is null).ToString());
        writer.WriteAttributeString("fixtures", (report?.FixturesCompleted ?? 0).ToString());
        writer.WriteAttributeString("values", (report?.ValuesChecked ?? 0).ToString());
        writer.WriteAttributeString("outputOverflowRejections", (report?.OutputOverflowRejectionsChecked ?? 0).ToString());
        writer.WriteAttributeString("overflowReferenceSlots", string.Join(",", report?.FormulaCoverage?.OutputOverflowReferenceSlots ?? Array.Empty<int>()));
        writer.WriteAttributeString("inputRejections", (report?.InputRejectionsChecked ?? 0).ToString());
        writer.WriteAttributeString("independentTrajectorySlots", string.Join(",", report?.FormulaCoverage?.IndependentTrajectoryOutputSlots ?? Array.Empty<int>()));
        writer.WriteAttributeString("recurrenceOnlySlots", string.Join(",", report?.FormulaCoverage?.RecurrenceOnlyOutputSlots ?? Array.Empty<int>()));
        writer.WriteAttributeString("missingStartupSlots", string.Join(",", report?.FormulaCoverage?.MissingStartupOutputSlots ?? Array.Empty<int>()));
        if (report is not null)
            foreach (var fixture in report.FixtureEvidence)
            {
                writer.WriteStartElement("fixture");
                writer.WriteAttributeString("name", fixture.Name);
                writer.WriteAttributeString("inputBars", fixture.InputBars.ToString());
                writer.WriteAttributeString("valuesChecked", fixture.ValuesChecked.ToString());
                writer.WriteAttributeString("outputOverflowRejectionsChecked", fixture.OutputOverflowRejectionsChecked.ToString());
                writer.WriteAttributeString("outputOverflowBarIndex", fixture.OutputOverflowBarIndex?.ToString() ?? "");
                writer.WriteAttributeString("outputOverflowSlot", fixture.OutputOverflowSlot?.ToString() ?? "");
                writer.WriteAttributeString("outputOverflowSign", fixture.OutputOverflowSign?.ToString() ?? "");
                writer.WriteAttributeString("inputRejectionsChecked", fixture.InputRejectionsChecked.ToString());
                writer.WriteAttributeString("completed", fixture.Completed.ToString());
                writer.WriteAttributeString("passed", fixture.Passed.ToString());
                writer.WriteAttributeString("customerResetChecked", fixture.CustomerResetChecked.ToString());
                writer.WriteEndElement();
            }
        if (report is not null)
            foreach (var failure in report.Failures)
            {
                writer.WriteElementString("failure", failure.ToString());
                Console.Error.WriteLine(work[i].Name + ": " + failure);
            }
        if (errors[i] is not null) writer.WriteElementString("error", errors[i]);
        writer.WriteEndElement();
    }
    writer.WriteEndElement();
}
var failed = reports.Count(r => r?.IsValid != true) + errors.Where((e, i) => e is not null && reports[i]?.IsValid == true).Count();
Console.WriteLine($"{work.Length} configurations; {failed} failed. Evidence: {Path.GetFullPath(output)}");
return failed == 0 ? 0 : 1;
