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
    const string New = "7bbfb63d3794034d88047d11d40682938d8076ff58aa64d53a0ba2096089317b";
    const string CandidateManifestSha = "c0be31608e8df96ad275e31604b6916e7d027e782eb1bcd8a48659d77a658c2d";
    const string CandidateFingerprint = "7075fdf54f314f89e9b08e423b444e9850456525cb0f636d5ebbe410c12f0d81";
    const string GpuReceiptSha = "71112bcbc23d5ca4190edbc6735f0bddb0f9d3f79f7f34dbbce92f0eec3b29b7";
    const string GpuTrxGzSha = "9358f2957b525771e9bdf6f650444422c4a787741debc696a9ba5ff97aa88fd5";
    const string CandidateArtifactId = "14352708612";
    const string CandidatePdbSha = "a0a72191f46e8c56f6cb18938a0c9da144651da3a735df723126b65ccf144fb6";
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
        Require(new[] { New, CandidateManifestSha, CandidateFingerprint, GpuReceiptSha, GpuTrxGzSha }.All(value => value.Length == 64 && value.All(Uri.IsHexDigit)), "UNBOUND candidate/GPU pins; stop before input or output I/O.");
        Require(CandidateArtifactId.All(char.IsDigit) && CandidateArtifactId.Length > 0, "UNBOUND candidate artifact ID; stop before I/O.");
        string gpuEvidenceCommit = Environment.GetEnvironmentVariable("GPU_RECEIPT_COMMIT") ?? "";
        Require(gpuEvidenceCommit.Length == 40 && gpuEvidenceCommit.All(Uri.IsHexDigit), "UNBOUND GPU-only commit; stop before I/O.");
        if (args.Length != 7) throw new ArgumentException("Expected original inputs, runtime view, candidate artifact, GPU receipt, arm, pair, fresh output.");
        string input = Path.GetFullPath(args[0]), runtime = Path.GetFullPath(args[1]), candidate = Path.GetFullPath(args[2]);
        string gpuReceiptPath = Path.GetFullPath(args[3]);
        string arm = args[4], pair = args[5], output = Path.GetFullPath(args[6]);
        Require(arm == "new", "Unknown arm.");
        Require(pair == "TaLib.Functions.Stoch", "Unknown pair.");
        Require(!Directory.Exists(output), "Fresh output required."); Directory.CreateDirectory(output);
        var receipt = new Dictionary<string,object?> {
            ["diagnostic_only"] = true, ["qualification"] = false, ["native_win_claim"] = false,
            ["status"] = "running", ["process_id"] = Environment.ProcessId,
            ["process_start_utc"] = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            ["actual_clr"] = Environment.Version.ToString(), ["pair"] = pair, ["bars"] = 10000, ["arm"] = arm,
            ["warmups"] = 1000, ["profile_seconds"] = 10, ["production_builds"] = 0,
            ["timed_benchmark_cases"] = 0, ["processor_count"] = Environment.ProcessorCount,
            ["env_aidotnet_cpu_threads"] = Environment.GetEnvironmentVariable("AIDOTNET_CPU_THREADS"),
            ["source_old"] = "6051a95220eefa36e6f837a9a54e036164b76ebe",
            ["source_new"] = "0129b49e750feaee5bbc0ff4391ec90692a9f596",
            ["old_production_sha256"] = Old, ["new_production_sha256"] = New, ["frozen_harness_sha256"] = Bench,
            ["candidate_source_fingerprint"] = CandidateFingerprint, ["gpu100_receipt_sha256"] = GpuReceiptSha, ["gpu100_trx_gz_sha256"] = GpuTrxGzSha, ["gpu_evidence_commit"] = gpuEvidenceCommit, ["candidate_artifact_id"] = CandidateArtifactId,
            ["limits"] = "Existing-binary candidate-only kernel CPU profile; no latency, native-win or qualification claim. Full unchanged LibraryPairBenchmarks.Ooples fixture. All hot PID samples including unresolved retained during analysis. No runtime or worker assignments."
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
            Require(cp.GetProperty("source").GetString() == "0129b49e750feaee5bbc0ff4391ec90692a9f596" && cp.GetProperty("run_id").GetString() == "44136637430" && cp.GetProperty("workflow_head").GetString() == "2a1737f30e5e1164189d0209b39ea46bf38c1239", "Wrong candidate identities.");
            Require(cp.GetProperty("source_sha256").GetString() == CandidateFingerprint
                && cp.GetProperty("runtime_files").GetProperty("src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll").GetString() == New,
                "Candidate fingerprint/core binding mismatch.");
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
                Require(codeView.Guid == pdbGuid && codeView.Guid.ToString("N") == "9615e644223546ecbb276030fe50f7bf"
                    && entries[0].Stamp == stamp && stamp == 0xbf3c3403 && codeView.Age == 1,
                    "Candidate PE/PDB identity mismatch.");
                receipt["candidate_symbol_identity"] = new { guid = pdbGuid.ToString("N"), stamp = stamp.ToString("x8"), age = codeView.Age,
                    production_sha256 = New, pdb_sha256 = CandidatePdbSha, external_symbol_pdb = symbolPdb, protected_runtime_pdb_replaced = false };
            }
            Require(Hash(gpuReceiptPath) == GpuReceiptSha, "Wrong independently validated candidate GPU100 receipt.");
            using var gpuProof = JsonDocument.Parse(File.ReadAllText(gpuReceiptPath));
            var gp = gpuProof.RootElement; var buildOrigin = gp.GetProperty("build_origin");
            Require(gp.GetProperty("source_sha256").GetString() == CandidateFingerprint && gp.GetProperty("passed_cases").GetInt32() == 100,
                "GPU100 fingerprint/count mismatch.");
            Require(buildOrigin.GetProperty("run_id").ToString() == "44136637430"
                && buildOrigin.GetProperty("workflow_head").GetString() == "2a1737f30e5e1164189d0209b39ea46bf38c1239"
                && buildOrigin.GetProperty("source").GetString() == "0129b49e750feaee5bbc0ff4391ec90692a9f596"
                && buildOrigin.GetProperty("artifact_id").ToString() == CandidateArtifactId, "GPU receipt build origin mismatch.");
            var manifestFiles = cp.GetProperty("runtime_files");
            var coreEntries = manifestFiles.EnumerateObject().Where(entry => entry.Name.EndsWith("/OoplesFinance.StockIndicators.dll", StringComparison.Ordinal)).ToArray();
            Require(coreEntries.Length == 4 && coreEntries.All(entry => entry.Value.GetString() == New), "Candidate production core closure mismatch.");
            Require(gp.GetProperty("test_assembly_sha256").GetString() == manifestFiles.GetProperty("benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll").GetString(), "GPU test assembly mismatch.");
            string gpuTrxPath = Path.Combine(Path.GetDirectoryName(gpuReceiptPath)!, "gpu.trx.gz");
            Require(Hash(gpuTrxPath) == GpuTrxGzSha, "Wrong GPU100 compressed TRX.");
            using var gzip = new GZipStream(File.OpenRead(gpuTrxPath), CompressionMode.Decompress);
            using var trxBuffer = new MemoryStream(); gzip.CopyTo(trxBuffer); byte[] trxBytes = trxBuffer.ToArray();
            Require(Convert.ToHexStringLower(SHA256.HashData(trxBytes)) == gp.GetProperty("trx_sha256").GetString(), "GPU100 decompressed TRX mismatch.");
            using var xmlReader = System.Xml.XmlReader.Create(new MemoryStream(trxBytes), new System.Xml.XmlReaderSettings { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null });
            var trx = XDocument.Load(xmlReader); var results = trx.Descendants().Where(element => element.Name.LocalName == "UnitTestResult").ToArray();
            Require(results.Length == 100 && results.All(element => (string?)element.Attribute("outcome") == "Passed"), "GPU100 TRX outcomes mismatch.");
            var counters = trx.Descendants().Single(element => element.Name.LocalName == "Counters");
            Require((int?)counters.Attribute("total") == 100 && (int?)counters.Attribute("executed") == 100 && (int?)counters.Attribute("passed") == 100 && (int?)counters.Attribute("failed") == 0, "GPU100 TRX counters mismatch.");
            var definitions = trx.Descendants().Where(element => element.Name.LocalName == "UnitTest").ToDictionary(element => (string)element.Attribute("id")!, element => element.Descendants().Single(method => method.Name.LocalName == "TestMethod"));
            var actualMethods = results.Select(result => definitions[(string)result.Attribute("testId")!]).Select(method => (string)method.Attribute("className")! + "." + (string)method.Attribute("name")!).ToHashSet(StringComparer.Ordinal);
            var expectedMethods = gp.GetProperty("methods").EnumerateArray().Select(element => element.GetString()!).ToHashSet(StringComparer.Ordinal);
            Require(expectedMethods.Count == 12 && actualMethods.SetEquals(expectedMethods), "GPU100 method inventory mismatch.");
            receipt["gpu100_verified_cases"] = 100; receipt["gpu100_verified_methods"] = expectedMethods.ToArray();
            var common = proof.RootElement.GetProperty("common_manifest").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()!);
            Require(common.Count == 94, "Expected 94 common assets.");
            string Contained(string root, string relative) { string path = Path.GetFullPath(Path.Combine(root, relative)); Require(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Escaped pinned root."); return path; }
            var protectedFiles = common.ToDictionary(x => Contained(Path.Combine(input, "common"), x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
            protectedFiles.Add(Path.Combine(input, "production", "old.dll"), Old);
            protectedFiles.Add(Path.Combine(candidate, "blobs", New), New);
            protectedFiles.Add(candidatePdb, CandidatePdbSha); protectedFiles.Add(symbolPdb, CandidatePdbSha);
            protectedFiles.Add(candidateManifest, CandidateManifestSha); protectedFiles.Add(gpuReceiptPath, GpuReceiptSha); protectedFiles.Add(gpuTrxPath, GpuTrxGzSha); Check(protectedFiles);
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
            object configuredPair = type.GetField("_pair", flags)!.GetValue(fixture)!;
            var configuredLibrary = (Delegate)configuredPair.GetType().GetProperty("Library", flags)!.GetValue(configuredPair)!;
            object factoryClosure = configuredLibrary.Target ?? throw new InvalidOperationException("Missing frozen factory closure.");
            object Parameter(string name) => factoryClosure.GetType().GetField(name, flags)!.GetValue(factoryClosure)!;
            Require(Convert.ToInt32(Parameter("window")) == 5 && Convert.ToInt32(Parameter("k")) == 3
                && Convert.ToInt32(Parameter("d")) == 3 && !Convert.ToBoolean(Parameter("fast"))
                && Parameter("km").ToString() == "Sma" && Parameter("dm").ToString() == "Sma"
                && !Convert.ToBoolean(Parameter("first")) && Convert.ToInt32(Parameter("suppression")) == 0,
                "Frozen Stoch factory parameters changed.");
            receipt["factory_parameters"] = new { window = 5, k = 3, d = 3, fast = false, km = "Sma", dm = "Sma", first = false, suppression = 0 };
            receipt["settings_after_setup"] = Settings(); receipt["closure_before"] = Closure(); Save();
            for (int i = 0; i < 1000; i++) GC.KeepAlive(invoke.Invoke(fixture, null));
            receipt["profile_start_utc"] = DateTime.UtcNow; receipt["profile_start_timestamp"] = Stopwatch.GetTimestamp();
            receipt["timestamp_frequency"] = Stopwatch.Frequency;
            Console.WriteLine($"PROFILE START Stoch candidate PID={Environment.ProcessId}");
            var timer = Stopwatch.StartNew(); int iterations = 0; object? last = null;
            do { last = invoke.Invoke(fixture, null); iterations++; } while (timer.Elapsed.TotalSeconds < 10);
            receipt["profile_end_timestamp"] = Stopwatch.GetTimestamp(); receipt["profile_end_utc"] = DateTime.UtcNow;
            receipt["iterations"] = iterations; Console.WriteLine($"PROFILE END Stoch candidate PID={Environment.ProcessId} iterations={iterations}");
            GC.KeepAlive(last);
            receipt["settings_after"] = Settings(); receipt["closure_after"] = Closure(); Check(files); Check(protectedFiles);
            receipt["runtime_unchanged"] = true; receipt["protected_inputs_unchanged"] = true; receipt["status"] = "complete"; Save(); return 0;
        }
        catch (Exception error) { receipt["status"] = "failed"; receipt["error"] = error.ToString(); Save(); Console.Error.WriteLine(error); return 1; }
    }
    sealed class AuthenticatedContext(Dictionary<string,string> selected, Dictionary<string,string> framework, Dictionary<string,string> hashes) : AssemblyLoadContext("AuthenticatedStochGridCpuProfile", false)
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
