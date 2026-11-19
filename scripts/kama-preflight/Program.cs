using System.Collections;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

// Package-free framework-dependent bootstrap. All indicator calls use the actual
// frozen benchmark fixture; this program never calls a private production kernel.
internal static class Program
{
    private const BindingFlags All = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    private static readonly string[] Pairs = ["QuanTAlib.Kama", "Skender.GetKama", "TaLib.Functions.Kama", "Trady.Indicator.KaufmanAdaptiveMovingAverage"];
    private static readonly string[] Fields = ["Opens", "Highs", "Lows", "Closes", "Volumes"];
    private static object Property(object owner, string name) => owner.GetType().GetProperty(name, All)!.GetValue(owner)!;
    private static object Field(object owner, string name) => owner.GetType().GetField(name, All)!.GetValue(owner)!;
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static string Hash(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
    private static string FileHash(string path) => Hash(File.ReadAllBytes(path));
    private static string DoublesHash(double[] values)
    {
        byte[] bytes = new byte[checked(values.Length * sizeof(double))];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return Hash(bytes);
    }
    private static void SameJson(object actual, JsonElement expected, string message) =>
        Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(actual), JsonNode.Parse(expected.GetRawText())), message);
    private static void Write(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    private static (Dictionary<string, string> Values, Dictionary<string, string> Presence) ResultHashes(object result, string pair)
    {
        var values = new Dictionary<string, string>();
        var presence = new Dictionary<string, string>();
        string[] names = pair == "Skender.GetKama" ? ["Kama", "ER"] : ["Kama"];
        foreach (object entry in (IEnumerable)Property(result, "Outputs"))
        {
            string name = (string)Property(entry, "Key");
            Require(names.Contains(name) && !values.ContainsKey(name), "Unexpected/duplicate returned column");
            object column = Property(entry, "Value");
            double[] data = (double[])Property(column, "Values");
            bool[] present = (bool[])Property(column, "Present");
            Require(data.Length == 10000 && present.Length == 10000 && (int)Property(column, "FirstValid") == 0, "Incomplete returned column");
            int first = pair == "QuanTAlib.Kama" ? 0 : name == "ER" || pair == "TaLib.Functions.Kama" ? 20 : 19;
            for (int i = 0; i < data.Length; i++)
            {
                Require(present[i] == (i >= first), "Changed returned presence alignment");
                Require(i >= first || double.IsNaN(data[i]), "Absent returned value must be NaN");
            }
            byte[] mask = new byte[present.Length];
            Buffer.BlockCopy(present, 0, mask, 0, mask.Length);
            values.Add(name, DoublesHash(data));
            presence.Add(name, Hash(mask));
        }
        Require(values.Count == names.Length, "Missing returned column");
        return (values, presence);
    }
    private static JsonElement ParseHashMap(JsonElement ready, string name)
    {
        using var document = JsonDocument.Parse(ready.GetProperty(name).GetString()!);
        return document.RootElement.Clone();
    }
    private static int Main(string[] args)
    {
        if (args.Length != 4) throw new ArgumentException("Expected runtime, expected-contract JSON, output directory, and source commit");
        string runtime = Path.GetFullPath(args[0]), expectedPath = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[2]), source = args[3];
        Require(Directory.Exists(output), "Caller must create a fresh bounded output directory");
        string executable = Environment.ProcessPath!;
        Write(Path.Combine(output, "bootstrap.json"), new
        {
            actual_clr = Environment.Version.ToString(), disable_gpu = Environment.GetEnvironmentVariable("AIDOTNET_DISABLE_GPU"),
            process_executable = executable, process_executable_sha256 = FileHash(executable),
            runner_assembly_sha256 = FileHash(typeof(Program).Assembly.Location),
            runner_deps_sha256 = FileHash(Path.ChangeExtension(typeof(Program).Assembly.Location, "deps.json")),
            runner_runtimeconfig_sha256 = FileHash(Path.ChangeExtension(typeof(Program).Assembly.Location, "runtimeconfig.json")),
            framework_description = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
            corelib_path = typeof(object).Assembly.Location, corelib_sha256 = FileHash(typeof(object).Assembly.Location)
        });
        Require(Environment.Version.ToString() == "10.0.15" && Environment.GetEnvironmentVariable("AIDOTNET_DISABLE_GPU") == "1", "Wrong CLR or CPU bootstrap");
        using var expectedDocument = JsonDocument.Parse(File.ReadAllBytes(expectedPath));
        var context = AssemblyLoadContext.Default;
        using var invocation = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(output, "invocation.json")));
        JsonElement declared = invocation.RootElement.GetProperty("runtime_file_pins");
        Assembly? Resolve(AssemblyLoadContext loadContext, AssemblyName name)
        {
            string sibling = name.Name + ".dll";
            return declared.TryGetProperty(sibling, out _) ? loadContext.LoadFromAssemblyPath(Path.Combine(runtime, sibling)) : null;
        }
        context.Resolving += Resolve;
        try
        {
            string corePath = Path.Combine(runtime, "OoplesFinance.StockIndicators.dll");
            string harnessPath = Path.Combine(runtime, "OoplesFinance.StockIndicators.CompetitorBenchmarks.dll");
            Require(FileHash(corePath) == declared.GetProperty(Path.GetFileName(corePath)).GetString(), "Wrong actual core");
            Require(FileHash(harnessPath) == declared.GetProperty(Path.GetFileName(harnessPath)).GetString(), "Wrong actual common harness");
            Assembly core = context.LoadFromAssemblyPath(corePath), harness = context.LoadFromAssemblyPath(harnessPath);
            Assembly tensors = context.LoadFromAssemblyPath(Path.Combine(runtime, "AiDotNet.Tensors.dll"));
            object engine = tensors.GetType("AiDotNet.Tensors.Engines.AiDotNetEngine", true)!.GetProperty("Current", All)!.GetValue(null)!;
            Require(engine.GetType().FullName == "AiDotNet.Tensors.Engines.CpuEngine", "Expected default CPU engine");
            PropertyInfo workersProperty = tensors.GetType("AiDotNet.Tensors.Helpers.CpuParallelSettings", true)!.GetProperty("MaxDegreeOfParallelism", All)!;
            int workers = (int)workersProperty.GetValue(null)!;
            Type builderType = core.GetType("OoplesFinance.StockIndicators.Builder.StockIndicatorBuilder", true)!;
            object builder = Activator.CreateInstance(builderType)!;
            string backend = Field(builder, "_executionBackend").ToString()!, history = Field(builder, "_historyMode").ToString()!;
            Require(backend == "Auto" && history == "Full", "Changed default builder settings");
            Type fixtureType = harness.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks", true)!;
            int period = (int)fixtureType.GetField("Period", All)!.GetRawConstantValue()!;
            Require(period == 20, "Changed actual LP period");
            Type dataType = harness.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.CompetitorData", true)!;
            Type verifierType = harness.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.ComparisonVerifier", true)!;
            var records = new List<object>();
            foreach (string pair in Pairs)
            {
                object fixture = Activator.CreateInstance(fixtureType)!;
                fixtureType.GetProperty("PairId")!.SetValue(fixture, pair);
                fixtureType.GetProperty("Bars")!.SetValue(fixture, 10000);
                fixtureType.GetMethod("Setup")!.Invoke(fixture, null);
                Require((int)workersProperty.GetValue(null)! == workers, "Setup altered default workers");
                object registration = Field(fixture, "_pair"), data = Field(fixture, "_data");
                Require(!(bool)Property(registration, "IsCandle") && (string)Property(registration, "Indicator") == "SeededAdaptiveAverage", "Changed actual adapter indicator");
                object expectedData = pair == Pairs[3]
                    ? dataType.GetMethod("Create", All)!.Invoke(null, [10000, 42])!
                    : verifierType.GetMethod("Fixture", All)!.Invoke(null, ["walk", 10000])!;
                var hashes = new Dictionary<string, string>();
                foreach (string field in Fields)
                {
                    double[] values = (double[])Property(data, field), expectedValues = (double[])Property(expectedData, field);
                    Require(values.Length == 10000 && DoublesHash(values) == DoublesHash(expectedValues), "Actual fixture mismatch");
                    hashes.Add(field, DoublesHash(values));
                }
                string factory = pair == Pairs[3] ? "CompetitorData.Create(10000,42)" : "ComparisonVerifier.Fixture(walk,10000), Random731";
                object closure = ((Delegate)Property(registration, "Library")).Target!;
                Dictionary<string, object> closureParameters = pair == Pairs[2] ? new() { ["suppression"] = 0 }
                    : pair == Pairs[3] ? new() { ["fast"] = 2, ["slow"] = 30 }
                    : new() { ["fast"] = 2, ["slow"] = 30, ["quan"] = pair == Pairs[0] };
                foreach (var parameter in closureParameters)
                    Require(Equals(Field(closure, parameter.Key), parameter.Value), "Actual registered factory parameter mismatch");
                string parameters = pair == Pairs[0] ? "period20;fast2;slow30;publishStartup=true;clampFast=true;resetFlat=false;fastFlat=false;outputDelay=0"
                    : pair == Pairs[1] ? "period20;fast2;slow30;publishStartup=false;clampFast=false;resetFlat=true;fastFlat=false;outputDelay=0"
                    : pair == Pairs[2] ? "period20;fast2;slow30;publishStartup=false;clampFast=false;resetFlat=false;fastFlat=true;outputDelay=1;suppression=0"
                    : "period20;fast2;slow30;publishStartup=false;clampFast=false;resetFlat=false;fastFlat=false;outputDelay=0";
                object referenceResult = ((Delegate)Property(registration, "Reference")).DynamicInvoke(data, period)!;
                var reference = ResultHashes(referenceResult, pair);
                object actualResult = fixtureType.GetMethod("Ooples")!.Invoke(fixture, null)!;
                var actual = ResultHashes(actualResult, pair);
                JsonElement ready = expectedDocument.RootElement.GetProperty(pair).GetProperty("ready");
                Require(factory == ready.GetProperty("actual_factory").GetString() && parameters == ready.GetProperty("actual_indicator_parameters").GetString(), "Retained factory mismatch");
                SameJson(hashes, ready.GetProperty("actual_data_bits"), "Retained five-column fixture mismatch");
                SameJson(actual.Values, ParseHashMap(ready, "full_returned_output_hash"), "Retained actual output mismatch");
                SameJson(actual.Presence, ParseHashMap(ready, "presence_sha256"), "Retained actual mask mismatch");
                SameJson(reference.Values, JsonSerializer.SerializeToElement(actual.Values), "Independent exact reference output mismatch");
                SameJson(reference.Presence, JsonSerializer.SerializeToElement(actual.Presence), "Independent exact reference mask mismatch");
                Require((int)workersProperty.GetValue(null)! == workers, "Actual adapter altered default workers");
                records.Add(new { pair, bars = 10000, period, factory, parameters, actual_data_bits = hashes, full_returned_output_hash = actual.Values, presence_sha256 = actual.Presence });
            }
            Write(Path.Combine(output, "bridge.json"), new
            {
                status = "common-harness-four-adapter-preflight-passed", source, actual_clr = Environment.Version.ToString(),
                core_sha256 = FileHash(corePath), harness_sha256 = FileHash(harnessPath), cpu_engine = engine.GetType().FullName,
                default_backend = backend, default_history = history, default_workers = workers,
                setup_calls = 4, explicit_ooples_calls = 4, builds = 0, profiles = 0, warmups = 0, timings = 0,
                qualification = false, performance_claim = false, contexts = records
            });
            return 0;
        }
        finally { context.Resolving -= Resolve; }
    }
}
