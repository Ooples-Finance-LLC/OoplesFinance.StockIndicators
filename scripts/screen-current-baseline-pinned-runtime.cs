using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    const string Old = "4d571648599e567bb6df0cda1f823d79e95712241078c2ab1721cf7409c10173";
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
        if (args.Length != 5) throw new ArgumentException("Expected original inputs, runtime view, arm, pair, fresh output.");
        string input = Path.GetFullPath(args[0]), runtime = Path.GetFullPath(args[1]);
        string arm = args[2], pair = args[3], output = Path.GetFullPath(args[4]);
        Require(arm is "old" or "new" or "native", "Unknown arm.");
        Require(pair is "TaLib.Functions.LinearReg" or "TaLib.Functions.LinearRegSlope" or "TaLib.Functions.LinearRegIntercept" or "TaLib.Functions.LinearRegAngle" or "TaLib.Functions.Tsf" or "Skender.GetEpma", "Unknown pair.");
        Require(!Directory.Exists(output), "Fresh output required."); Directory.CreateDirectory(output);
        var receipt = new Dictionary<string,object?> {
            ["diagnostic_only"] = true, ["qualification"] = false, ["native_win_claim"] = false,
            ["status"] = "running", ["process_id"] = Environment.ProcessId,
            ["process_start_utc"] = Process.GetCurrentProcess().StartTime.ToUniversalTime(),
            ["actual_clr"] = Environment.Version.ToString(), ["pair"] = pair, ["bars"] = 10000, ["arm"] = arm,
            ["warmups"] = 1000, ["single_call_samples"] = 10, ["production_builds"] = 0,
            ["timed_benchmark_cases"] = 0, ["processor_count"] = Environment.ProcessorCount,
            ["env_aidotnet_cpu_threads"] = Environment.GetEnvironmentVariable("AIDOTNET_CPU_THREADS"),
            ["source_old"] = "6051a95220eefa36e6f837a9a54e036164b76ebe",
            ["source_new"] = "6051a95220eefa36e6f837a9a54e036164b76ebe",
            ["old_production_sha256"] = Old, ["new_production_sha256"] = Old, ["frozen_harness_sha256"] = Bench,
            ["limits"] = "Baseline A/A diagnostic: both labels use the same existing binary. Isolated-process MethodInfo/Stopwatch screen only, not BenchmarkDotNet qualification or native-win evidence. All timings require both outer idle gates. No runtime or worker assignments. Native follows source rounds and may have different runtime warmth."
        };
        void Save() { string temporary = Path.Combine(output, "case.json.new"); File.WriteAllText(temporary, JsonSerializer.Serialize(receipt, new JsonSerializerOptions { WriteIndented = true })); File.Move(temporary, Path.Combine(output, "case.json"), true); }
        Save();
        try
        {
            Require(Environment.Version.ToString() == "10.0.15", "Strict CLR 10.0.15 required.");
            foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
                Require(!System.Text.RegularExpressions.Regex.IsMatch((string)entry.Key, @"^(DOTNET_|COMPlus_).*(Tiered|TC_|Jit|ReadyToRun|GC|Roll_Forward|RollForward)", System.Text.RegularExpressions.RegexOptions.IgnoreCase), "Inherited runtime override.");
            string proofPath = Path.Combine(input, "provenance.json");
            Require(Hash(proofPath) == "9013cb468cec2d505c0eb2fb6050517d2a1a5e28b441ade2cd55523be298bf78", "Wrong original proof.");
            using var proof = JsonDocument.Parse(File.ReadAllText(proofPath));
            var common = proof.RootElement.GetProperty("common_manifest").EnumerateObject().ToDictionary(x => x.Name, x => x.Value.GetString()!);
            Require(common.Count == 94, "Expected 94 common assets.");
            string Contained(string root, string relative) { string path = Path.GetFullPath(Path.Combine(root, relative)); Require(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "Escaped pinned root."); return path; }
            var protectedFiles = common.ToDictionary(x => Contained(Path.Combine(input, "common"), x.Key), x => x.Value, StringComparer.OrdinalIgnoreCase);
            protectedFiles.Add(Path.Combine(input, "production", "old.dll"), Old);
            Check(protectedFiles);
            string coreHash = Old;
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
            object Settings() => new { dop = degree.GetValue(null), effective_workers = workers?.Invoke(null, new object[] { 8 }) };
            receipt["settings_before"] = Settings(); receipt["runtime_files"] = files; receipt["protected_input_hashes"] = protectedFiles; receipt["selected_managed_assets"] = selected;
            var type = Find("OoplesFinance.StockIndicators.CompetitorBenchmarks.dll").GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks", true)!;
            var fixture = Activator.CreateInstance(type)!; type.GetProperty("PairId")!.SetValue(fixture, pair); type.GetProperty("Bars")!.SetValue(fixture, 10000);
            type.GetMethod("Setup")!.Invoke(fixture, null);
            int period = (int)type.GetField("Period", flags)!.GetRawConstantValue()!;
            Require(period == 20, "Unexpected actual benchmark period.");
            receipt["benchmark_period_constant"] = period;
            object selectedPair = type.GetField("_pair", flags)!.GetValue(fixture)!;
            Require((string)selectedPair.GetType().GetProperty("Id", flags)!.GetValue(selectedPair)! == pair, "Wrong actual factory pair.");
            receipt["actual_factory_pair"] = pair;
            var invoke = type.GetMethod(arm == "native" ? "Competitor" : "Ooples")!;
            receipt["settings_after_setup"] = Settings(); receipt["closure_before"] = Closure(); Save();
            for (int i = 0; i < 1000; i++) GC.KeepAlive(invoke.Invoke(fixture, null));
            var samples = new List<double>(); var retained = new List<object>(); receipt["measurement_start_utc"] = DateTime.UtcNow;
            for (int i = 0; i < 10; i++)
            {
                long start = Stopwatch.GetTimestamp(); object result = invoke.Invoke(fixture, null)!; long end = Stopwatch.GetTimestamp();
                samples.Add((end - start) * 1000.0 / Stopwatch.Frequency); if (arm != "native") retained.Add(result); GC.KeepAlive(result);
            }
            receipt["measurement_end_utc"] = DateTime.UtcNow; receipt["samples_ms"] = samples;
            receipt["source_outputs"] = retained.Select(Snapshot).ToArray(); receipt["source_outputs_compared_outer"] = false;
            receipt["settings_after"] = Settings(); receipt["closure_after"] = Closure(); Check(files); Check(protectedFiles);
            receipt["runtime_unchanged"] = true; receipt["protected_inputs_unchanged"] = true; receipt["status"] = "complete"; Save(); return 0;
        }
        catch (Exception error) { receipt["status"] = "failed"; receipt["error"] = error.ToString(); Save(); Console.Error.WriteLine(error); return 1; }
    }
    sealed class AuthenticatedContext(Dictionary<string,string> selected, Dictionary<string,string> framework, Dictionary<string,string> hashes) : AssemblyLoadContext("AuthenticatedRegressionBaselineScreen", false)
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
