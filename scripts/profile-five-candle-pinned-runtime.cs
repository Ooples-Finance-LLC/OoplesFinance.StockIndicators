using System.Collections;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Diagnostics;
using System.IO.Compression;
using System.Xml.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    const string Old = "4d571648599e567bb6df0cda1f823d79e95712241078c2ab1721cf7409c10173";
    const string New = "c4915d59aef682579cd0a3edfae98fa363f9694e1321687bdf2b48e32b4aa005";
    const string CandidateManifestSha = "f6a3bfc7781d4a9ccbfe9281f8f72e1d5c248ff4848ee919507b458ab1106055";
    const string CandidateFingerprint = "99af80efc2203fe568c5a682724d6f32d39e76dfe66e422f2528c874abf29ac8";
    const string CandidateArtifactId = "14382986862";
    const string CandidatePdbSha = "77579d1f431ca3b94e8a6ba490d0eed016bd33cd914f7f72a7f51648fad2a636";
    const string Bench = "48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723";
    static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Check(Dictionary<string,string> files) { foreach (var file in files) Require(Hash(file.Key) == file.Value, "Changed protected file: " + file.Key); }
    record Column(int FirstValid, byte[] Values, byte[]? Present);
    static Dictionary<string,Column> Snapshot(object result)
    {
        var outputs = (IDictionary)result.GetType().GetProperty("Outputs")!.GetValue(result)!;
        var snapshot = new Dictionary<string,Column>();
        foreach (DictionaryEntry entry in outputs)
        {
            object column = entry.Value!; Type type = column.GetType();
            var values = (double[])type.GetProperty("Values")!.GetValue(column)!;
            byte[] bits = new byte[values.Length * 8]; Buffer.BlockCopy(values, 0, bits, 0, bits.Length);
            var present = (bool[]?)type.GetProperty("Present")!.GetValue(column);
            byte[]? mask = present == null ? null : new byte[present.Length];
            if (present != null) Buffer.BlockCopy(present, 0, mask!, 0, mask!.Length);
            snapshot.Add((string)entry.Key, new Column((int)type.GetProperty("FirstValid")!.GetValue(column)!, bits, mask));
        }
        return snapshot;
    }
    static int Main(string[] args)
    {
        Require(new[] { New, CandidateManifestSha, CandidateFingerprint }.All(value => value.Length == 64 && value.All(Uri.IsHexDigit)), "UNBOUND candidate/GPU pins; stop before input or output I/O.");
        Require(CandidateArtifactId.All(char.IsDigit) && CandidateArtifactId.Length > 0, "UNBOUND candidate artifact ID; stop before I/O.");
        if (args.Length != 6) throw new ArgumentException("Expected original inputs, runtime view, candidate artifact, arm, pair, fresh output.");
        string input = Path.GetFullPath(args[0]), runtime = Path.GetFullPath(args[1]), candidate = Path.GetFullPath(args[2]);
        string arm = args[3], pair = args[4], output = Path.GetFullPath(args[5]);
        Require(arm == "new", "Unknown arm.");
        Require(pair is "TaLib.Candles.ConcealingBabySwallow" or "TaLib.Candles.LadderBottom" or "TaLib.Candles.ThreeStarsInSouth" or "TaLib.Candles.Breakaway" or "TaLib.Candles.RisingFallingThreeMethods", "Unknown fixed profile pair.");
        Require(!Directory.Exists(output), "Fresh output required."); Directory.CreateDirectory(output);
        var receipt = new Dictionary<string,object?> {
            ["diagnostic_only"] = true, ["qualification"] = false, ["native_win_claim"] = false,
            ["status"] = "running", ["process_id"] = Environment.ProcessId,
            ["process_start_utc"] = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            ["actual_clr"] = Environment.Version.ToString(), ["pair"] = pair, ["bars"] = 10000, ["arm"] = arm,
            ["warmups"] = 1000, ["profile_seconds"] = 5, ["production_builds"] = 0,
            ["timed_benchmark_cases"] = 0, ["processor_count"] = Environment.ProcessorCount,
            ["env_aidotnet_cpu_threads"] = Environment.GetEnvironmentVariable("AIDOTNET_CPU_THREADS"),
            ["source_old"] = "6051a95220eefa36e6f837a9a54e036164b76ebe",
            ["source_new"] = "093969bb3650c3e2aad384cd0ecb2a991084ab49",
            ["old_production_sha256"] = Old, ["new_production_sha256"] = New, ["frozen_harness_sha256"] = Bench,
            ["candidate_source_fingerprint"] = CandidateFingerprint, ["candidate_artifact_id"] = CandidateArtifactId,
            ["limits"] = "Parent-authenticated CI core/PDB kernel CPU and GC diagnostic; no latency, native-win or qualification claim. Full unchanged LibraryPairBenchmarks.Ooples fixture. All hot PID samples including unresolved retained during analysis. No runtime or worker assignments."
        };
        void Save() { string temporary = Path.Combine(output, "replay.json.new"); File.WriteAllText(temporary, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, Path.Combine(output, "replay.json"), true); }
        Save();
        try
        {
            Require(Environment.Version.ToString() == "10.0.15", "Strict CLR 10.0.15 required.");
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                Require(!System.Text.RegularExpressions.Regex.IsMatch((string)entry.Key, @"^(DOTNET_|COMPlus_).*(Tiered|TC_|Jit|ReadyToRun|GC|Roll_Forward|RollForward)", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "Inherited runtime override.");
            string proofPath = Path.Combine(input, "provenance.json");
            Require(Hash(proofPath) == "9013cb468cec2d505c0eb2fb6050517d2a1a5e28b441ade2cd55523be298bf78", "Wrong original proof.");
            string candidateManifest = Path.Combine(candidate, "manifest.json");
            Require(Hash(candidateManifest) == CandidateManifestSha, "Wrong candidate manifest.");
            using var proof = JsonDocument.Parse(File.ReadAllText(proofPath));
            using var candidateProof = JsonDocument.Parse(File.ReadAllText(candidateManifest));
            var cp = candidateProof.RootElement;
            Require(cp.GetProperty("source").GetString() == "093969bb3650c3e2aad384cd0ecb2a991084ab49" && cp.GetProperty("run_id").GetString() == "44201563051" && cp.GetProperty("workflow_head").GetString() == "2a1737f30e5e1164189d0209b39ea46bf38c1239", "Wrong candidate identities.");
            Require(cp.GetProperty("source_sha256").GetString() == CandidateFingerprint
                && cp.GetProperty("runtime_files").GetProperty("src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll").GetString() == New,
                "Candidate fingerprint/core binding mismatch.");
            Require(cp.GetProperty("status").GetString() == "cpu-correctness-complete" && cp.GetProperty("checks").GetProperty("unit").GetProperty("passed").GetInt32() == 46 && cp.GetProperty("checks").GetProperty("parity").GetProperty("passed").GetInt32() == 597, "Actual focused CPU receipt mismatch.");
            string candidatePdb = Path.Combine(candidate, "blobs", CandidatePdbSha);
            string symbolPdb = Path.Combine(Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH") ?? "", "OoplesFinance.StockIndicators.pdb");
            Require(cp.GetProperty("runtime_files").GetProperty("src/bin/Release/net10.0/OoplesFinance.StockIndicators.pdb").GetString() == CandidatePdbSha
                && Hash(candidatePdb) == CandidatePdbSha && Hash(symbolPdb) == CandidatePdbSha,
                "Candidate matching external PDB bytes changed.");
            Require(!Path.GetFullPath(symbolPdb).StartsWith(runtime + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "External PDB must not replace frozen runtime PDB.");
            using (var peStream = File.OpenRead(Path.Combine(candidate, "blobs", New)))
            using (var pe = new PEReader(peStream))
            using (var pdbStream = File.OpenRead(candidatePdb))
            using (var pdbProvider = MetadataReaderProvider.FromPortablePdbStream(pdbStream))
            {
                var entries = pe.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.CodeView).ToArray();
                Require(entries.Length == 1, "Expected single CodeView identity.");
                var codeView = pe.ReadCodeViewDebugDirectoryData(entries[0]);
                var pdbId = pdbProvider.GetMetadataReader().DebugMetadataHeader!.Id;
                Require(pdbId.Length == 20, "Expected portable PDB identity.");
                var pdbGuid = new Guid(pdbId.AsSpan().Slice(0, 16));
                uint stamp = BinaryPrimitives.ReadUInt32LittleEndian(pdbId.AsSpan().Slice(16, 4));
                Require(codeView.Guid == pdbGuid && entries[0].Stamp == stamp && codeView.Age == 1,
                    "Candidate PE/PDB identity mismatch.");
                receipt["candidate_symbol_identity"] = new { guid = pdbGuid.ToString("N"), stamp = stamp.ToString("x8"), age = codeView.Age,
                    production_sha256 = New, pdb_sha256 = CandidatePdbSha, external_symbol_pdb = symbolPdb, protected_runtime_pdb_replaced = false };
            }
            var common = proof.RootElement.GetProperty("common_manifest").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()!);
            Require(common.Count == 94, "Expected 94 common assets.");
            string Contained(string root, string relative) { string path = Path.GetFullPath(Path.Combine(root, relative)); Require(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Escaped pinned root."); return path; }
            var protectedFiles = common.ToDictionary(x => Contained(Path.Combine(input, "common"), x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
            protectedFiles.Add(Path.Combine(input, "production", "old.dll"), Old);
            protectedFiles.Add(Path.Combine(candidate, "blobs", New), New);
            protectedFiles.Add(candidatePdb, CandidatePdbSha); protectedFiles.Add(symbolPdb, CandidatePdbSha);
            protectedFiles.Add(candidateManifest, CandidateManifestSha); Check(protectedFiles);
            string coreHash = arm == "new" ? New : Old;
            var files = common.ToDictionary(x => Contained(runtime, x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
            string core = Path.Combine(runtime, "OoplesFinance.StockIndicators.dll"), benchmark = Path.Combine(runtime, "OoplesFinance.StockIndicators.CompetitorBenchmarks.dll");
            files[core] = coreHash; Require(files[benchmark] == Bench, "Wrong frozen harness."); Check(files);
            var framework = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(Path.GetDirectoryName(typeof(object).Assembly.Location)!, "*.dll")) { try { framework[AssemblyName.GetAssemblyName(path).FullName] = path; } catch (BadImageFormatException) { } }
            var resolver = new AssemblyDependencyResolver(benchmark); var selected = new Dictionary<string,string>(StringComparer.Ordinal);
            foreach (string path in files.Keys.Where(x => Path.GetExtension(x).Equals(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                AssemblyName identity; try { identity = AssemblyName.GetAssemblyName(path); } catch (BadImageFormatException) { continue; }
                if (framework.ContainsKey(identity.FullName)) continue;
                string resolved = Path.GetFullPath(resolver.ResolveAssemblyToPath(identity) ?? throw new InvalidOperationException("Unresolved asset."));
                Require(files.TryGetValue(resolved, out string? expected) && Hash(resolved) == expected, "Unauthenticated asset.");
                string actual = AssemblyName.GetAssemblyName(resolved).FullName;
                Require(!selected.TryGetValue(actual, out string? prior) || prior == resolved, "Ambiguous asset."); selected[actual] = resolved;
            }
            var context = new AuthenticatedContext(selected, framework, files);
            foreach (var entry in selected) { var a = context.LoadFromAssemblyPath(entry.Value); Require(a.FullName == entry.Key && AssemblyLoadContext.GetLoadContext(a) == context && Hash(a.Location) == files[entry.Value], "Preload mismatch."); }
            object[] Closure() => context.Assemblies.Select(a => { Require(!a.IsDynamic && selected.TryGetValue(a.FullName!, out string? path) && Path.GetFullPath(a.Location) == path && Hash(a.Location) == files[path], "Changed isolated closure."); return (object)new { name = a.FullName, location = a.Location, sha256 = Hash(a.Location) }; }).ToArray();
            Assembly Find(string name) => context.Assemblies.Single(a => a.FullName == AssemblyName.GetAssemblyName(Path.Combine(runtime, name)).FullName);
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var production = Find("OoplesFinance.StockIndicators.dll");
            var degree = Find("AiDotNet.Tensors.dll").GetType("AiDotNet.Tensors.Helpers.CpuParallelSettings", true)!.GetProperty("MaxDegreeOfParallelism", flags)!;
            var workers = production.GetType("OoplesFinance.StockIndicators.Indicators.ValuesBarExecution", true)!.GetMethod("WorkerCount", flags);
            object Settings() { int dop = Convert.ToInt32(degree.GetValue(null)); int count = Convert.ToInt32(workers!.Invoke(null, new object[] { 8 })); Require(dop == 2 && count == 2 && Environment.ProcessorCount == 4, "Expected unchanged two workers and four logical CPUs."); return new { dop, effective_workers = count }; }
            receipt["settings_before"] = Settings(); receipt["runtime_files"] = files; receipt["protected_input_hashes"] = protectedFiles; receipt["selected_managed_assets"] = selected;
            var type = Find("OoplesFinance.StockIndicators.CompetitorBenchmarks.dll").GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks", true)!;
            var fixture = Activator.CreateInstance(type)!; type.GetProperty("PairId")!.SetValue(fixture, pair); type.GetProperty("Bars")!.SetValue(fixture, 10000);
            type.GetMethod("Setup")!.Invoke(fixture, null); var invoke = type.GetMethod("Ooples")!;
            object selectedPair = type.GetField("_pair", flags)!.GetValue(fixture)!;
            Require((string)selectedPair.GetType().GetProperty("Id", flags)!.GetValue(selectedPair)! == pair, "Frozen candle factory pair changed.");
            var library = (Delegate)selectedPair.GetType().GetProperty("Library", flags)!.GetValue(selectedPair)!;
            object factory = library.Target ?? throw new InvalidOperationException("Frozen candle factory closure missing.");
            string name = (string)factory.GetType().GetField("n", flags)!.GetValue(factory)!;
            Require(pair == "TaLib.Candles." + name, "Frozen candle factory name changed.");
            string family = name is "MatHold" or "RisingFallingThreeMethods" ? "FiveCandleContinuationComparison"
                : name is "Breakaway" or "ConcealingBabySwallow" or "LadderBottom" ? "ExtendedReversalComparison" : "ExhaustionComparison";
            var factoryType = Find("OoplesFinance.StockIndicators.CompetitorBenchmarks.dll").GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks." + family, true)!;
            var create = factoryType.GetMethod("Create", flags)!;
            var parameters = create.GetParameters();
            var expectedDefaults = family switch
            {
                "FiveCandleContinuationComparison" => new Dictionary<string,object> { ["longPeriod"] = 10, ["shortPeriod"] = 10, ["penetration"] = .5 },
                "ExtendedReversalComparison" => new Dictionary<string,object> { ["period"] = 10 },
                _ => new Dictionary<string,object> { ["longPeriod"] = 10, ["shortPeriod"] = 10, ["shadowPeriod"] = 10, ["nearPeriod"] = 5, ["farPeriod"] = 5 }
            };
            Require(parameters.Length == expectedDefaults.Count + 1 && parameters[0].Name == "name", "Unexpected factory signature.");
            foreach (var parameter in parameters.Skip(1))
                Require(parameter.HasDefaultValue && expectedDefaults.TryGetValue(parameter.Name!, out object? expected) && Equals(parameter.DefaultValue, expected), "Changed frozen factory default: " + parameter.Name);
            object[] factoryArgs = parameters.Select((parameter, index) => index == 0 ? (object)name : parameter.DefaultValue!).ToArray();
            object indicator = create.Invoke(null, factoryArgs)!;
            Require(indicator.GetType().Name == name + "Candle", "Wrong actual factory indicator.");
            string[] properties = name switch
            {
                "MatHold" => ["LongPeriod", "ShortPeriod", "Penetration"],
                "RisingFallingThreeMethods" => ["LongPeriod", "ShortPeriod"],
                "Breakaway" or "ConcealingBabySwallow" or "LadderBottom" => ["Period"],
                "AdvanceBlock" => ["LongPeriod", "ShadowPeriod", "NearPeriod", "FarPeriod"],
                "Stalled" => ["LongPeriod", "ShortPeriod", "ShadowPeriod", "NearPeriod"],
                _ => ["LongPeriod", "ShortPeriod", "ShadowPeriod"]
            };
            var actualProperties = new Dictionary<string,object>();
            foreach (string propertyName in properties)
            {
                object actual = indicator.GetType().GetProperty(propertyName, flags)!.GetValue(indicator)!;
                string parameterName = char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
                Require(expectedDefaults.TryGetValue(parameterName, out object? expected) && Equals(actual, expected), "Changed actual indicator property: " + propertyName);
                actualProperties.Add(propertyName, actual);
            }
            receipt["factory_parameters"] = new { name, family, defaults = expectedDefaults, actual_indicator_type = indicator.GetType().FullName,
                actual_properties = actualProperties, benchmark_period_argument_ignored = true,
                fixture = "LibraryPairBenchmarks.Setup -> ComparisonVerifier.BenchmarkFixture -> CompetitorData.Create(count, seed42); no override" };
            receipt["settings_after_setup"] = Settings(); receipt["closure_before"] = Closure(); Save();
            for (int i = 0; i < 1000; i++) GC.KeepAlive(invoke.Invoke(fixture, null));
            receipt["profile_start_utc"] = DateTime.UtcNow; receipt["profile_start_timestamp"] = Stopwatch.GetTimestamp();
            receipt["timestamp_frequency"] = Stopwatch.Frequency;
            Console.WriteLine($"PROFILE START five-candle candidate PID={Environment.ProcessId}");
            var timer = Stopwatch.StartNew(); int iterations = 0; object? last = null;
            do { last = invoke.Invoke(fixture, null); iterations++; } while (timer.Elapsed.TotalSeconds < 5);
            receipt["profile_end_timestamp"] = Stopwatch.GetTimestamp(); receipt["profile_end_utc"] = DateTime.UtcNow;
            receipt["iterations"] = iterations; Console.WriteLine($"PROFILE END five-candle candidate PID={Environment.ProcessId} iterations={iterations}");
            GC.KeepAlive(last);
            receipt["settings_after"] = Settings(); receipt["closure_after"] = Closure(); Check(files); Check(protectedFiles);
            receipt["runtime_unchanged"] = true; receipt["protected_inputs_unchanged"] = true; receipt["status"] = "complete"; Save(); return 0;
        }
        catch (Exception error) { receipt["status"] = "failed"; receipt["error"] = error.ToString(); Save(); Console.Error.WriteLine(error); return 1; }
    }
    sealed class AuthenticatedContext(Dictionary<string,string> selected, Dictionary<string,string> framework, Dictionary<string,string> hashes) : AssemblyLoadContext("AuthenticatedFiveCandleCpuProfile", false)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if (selected.TryGetValue(name.FullName, out string? path)) { Require(Hash(path) == hashes[path], "Dependency changed."); return LoadFromAssemblyPath(path); }
            static bool Compatible(AssemblyName actual, AssemblyName requested) => actual.Name == requested.Name && (actual.CultureName ?? "") == (requested.CultureName ?? "") && (actual.GetPublicKeyToken() ?? []).SequenceEqual(requested.GetPublicKeyToken() ?? []) && actual.Version >= requested.Version;
            Require(framework.Keys.Any(identity => Compatible(new AssemblyName(identity), name)), "Unselected nonframework dependency.");
            Assembly shared = Default.LoadFromAssemblyName(name);
            Require(framework.TryGetValue(shared.FullName!, out string? expected) && Path.GetFullPath(shared.Location).Equals(expected, StringComparison.OrdinalIgnoreCase) && Compatible(shared.GetName(), name), "Wrong shared framework."); return shared;
        }
    }
}
