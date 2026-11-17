using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text.Json;

internal static class Program
{
    static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static int Main(string[] args)
    {
        if (args.Length != 3) throw new ArgumentException("Expected input, runtime and fresh output directories.");
        string input=Path.GetFullPath(args[0]), runtime=Path.GetFullPath(args[1]), output=Path.GetFullPath(args[2]);
        Require(!Directory.Exists(output), "Output must be fresh.");
        Directory.CreateDirectory(output);
        var receipt=new Dictionary<string,object?> { ["diagnostic_only"]=true,["qualification"]=false,["performance_claim"]=false,["status"]="running",["stage"]="start",["host_clr"]=Environment.Version.ToString(),["process_id"]=Environment.ProcessId,["environment_processor_count"]=Environment.ProcessorCount,["env_aidotnet_cpu_threads"]=Environment.GetEnvironmentVariable("AIDOTNET_CPU_THREADS"),["production_builds"]=0,["diagnostic_driver_builds"]=1,["tests"]=0,["timed_benchmark_cases"]=0,["warmups"]=100,["profile_seconds"]=10,["pair"]="TaLib.Candles.DarkCloudCover",["bars"]=10000,["workflow_commit"]=Environment.GetEnvironmentVariable("GITHUB_SHA"),["workflow_run"]=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),["attribution"]="Fixture boundaries and observed settings only; sampling semantics come from the collector. No qualification or latency claim.",["symbol_fidelity"]="Frozen PDB retained; no source/PDB match claimed." };
        void Save() { string temp=Path.Combine(output,"replay.json.new"); File.WriteAllText(temp,JsonSerializer.Serialize(receipt,new JsonSerializerOptions {WriteIndented=true})); File.Move(temp,Path.Combine(output,"replay.json"),true); }
        Save();
        try
        {
            Require(Environment.Version.ToString()=="10.0.15","Expected CLR 10.0.15.");
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                Require(!System.Text.RegularExpressions.Regex.IsMatch((string)entry.Key,@"^(DOTNET_|COMPlus_).*(Tiered|TC_|Jit|ReadyToRun|GC|Roll_Forward|RollForward)",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"Inherited runtime override requires review: "+entry.Key);
            string proofPath=Path.Combine(input,"provenance.json");
            Require(Hash(proofPath)=="9013cb468cec2d505c0eb2fb6050517d2a1a5e28b441ade2cd55523be298bf78","Wrong input proof.");
            using var proof=JsonDocument.Parse(File.ReadAllText(proofPath)); var p=proof.RootElement;
            const string coreHash="4d571648599e567bb6df0cda1f823d79e95712241078c2ab1721cf7409c10173", benchHash="48229494a1ff1e4e59bddd5a018cbd590cae24523d52dd2f3effdd20bd802723";
            Require(p.GetProperty("sources").GetProperty("old").GetString()=="6051a95220eefa36e6f837a9a54e036164b76ebe" && p.GetProperty("run_id").GetString()=="44010931924" && p.GetProperty("production_sha256").GetProperty("old").GetString()==coreHash,"Wrong provenance identities.");
            var sourceFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); var runtimeFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string Contained(string root,string relative) { string path=Path.GetFullPath(Path.Combine(root,relative)); Require(path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Escaped pinned root.");return path; }
            string common=Path.Combine(input,"common"); int count=0;
            foreach(var entry in p.GetProperty("common_manifest").EnumerateObject()) { string hash=entry.Value.GetString()!; sourceFiles.Add(Contained(common,entry.Name),hash);runtimeFiles.Add(Contained(runtime,entry.Name),hash);count++; }
            Require(count==94,"Expected 94 common files.");
            string core=Path.Combine(runtime,"OoplesFinance.StockIndicators.dll"), benchmark=Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll");
            Require(runtimeFiles[benchmark]==benchHash,"Wrong frozen harness.");sourceFiles.Add(Path.Combine(input,"production","old.dll"),coreHash);runtimeFiles[core]=coreHash;
            void CheckFiles(Dictionary<string,string> files) { foreach(var item in files) Require(Hash(item.Key)==item.Value,"Changed protected file: "+item.Key); }
            CheckFiles(sourceFiles);CheckFiles(runtimeFiles);
            receipt["protected_input_hashes"]=sourceFiles;receipt["runtime_files"]=runtimeFiles;receipt["protected_input_files"]=sourceFiles.Count;receipt["runtime_file_count"]=runtimeFiles.Count;receipt["production_sha256"]=coreHash;receipt["frozen_benchmark_sha256"]=benchHash;receipt["input_provenance_sha256"]=Hash(proofPath);receipt["source"]="6051a95220eefa36e6f837a9a54e036164b76ebe";receipt["compiled_source"]="a1f0028c601e1c6e7be5eabfe0d5da5cfe1f0758";receipt["frozen_harness_source"]="9a45372530e49718b01b6524f9a0db993514e1e8";
            string framework=Path.GetDirectoryName(typeof(object).Assembly.Location)!;
            var frameworkIdentities=new Dictionary<string,string>(StringComparer.Ordinal);
            foreach(string path in Directory.GetFiles(framework,"*.dll")) { try {frameworkIdentities[AssemblyName.GetAssemblyName(path).FullName]=path;}catch(BadImageFormatException) {} }
            var selected=new Dictionary<string,string>(StringComparer.Ordinal); var shared=new Dictionary<string,string>(StringComparer.Ordinal);var native=new List<string>();var resolver=new AssemblyDependencyResolver(benchmark);
            foreach(string path in runtimeFiles.Keys.Where(x=>Path.GetExtension(x).Equals(".dll",StringComparison.OrdinalIgnoreCase)))
            {
                AssemblyName identity;try {identity=AssemblyName.GetAssemblyName(path);}catch(BadImageFormatException) {native.Add(path);continue;}
                if(frameworkIdentities.TryGetValue(identity.FullName,out string? frameworkPath)) {shared[identity.FullName]=frameworkPath;continue;}
                string resolved=Path.GetFullPath(resolver.ResolveAssemblyToPath(identity) ?? throw new InvalidOperationException("Unresolved asset: "+identity));
                Require(runtimeFiles.TryGetValue(resolved,out string? expected) && Hash(resolved)==expected,"Unauthenticated resolver asset: "+resolved);
                string actual=AssemblyName.GetAssemblyName(resolved).FullName;
                Require(!selected.TryGetValue(actual,out string? previous) || previous==resolved,"Ambiguous assembly identity.");selected[actual]=resolved;
            }
            var context=new AuthenticatedContext(selected,frameworkIdentities,runtimeFiles);
            foreach(var item in selected) {var assembly=context.LoadFromAssemblyPath(item.Value);Require(assembly.FullName==item.Key && AssemblyLoadContext.GetLoadContext(assembly)==context && Hash(assembly.Location)==runtimeFiles[item.Value],"Isolated preload mismatch.");}
            receipt["selected_managed_assets"]=selected;receipt["shared_framework_identities"]=shared;receipt["native_assets_not_explicitly_resolved"]=native;receipt["assembly_load_context"]=context.Name;
            object[] Closure() => context.Assemblies.Select(a=> { Require(!a.IsDynamic && selected.TryGetValue(a.FullName!,out string? path) && Path.GetFullPath(a.Location)==path && Hash(a.Location)==runtimeFiles[path],"Unexpected isolated closure.");return (object)new {name=a.FullName,location=a.Location,sha256=Hash(a.Location)}; }).ToArray();
            Assembly Find(string path)=>context.Assemblies.Single(a=>a.FullName==AssemblyName.GetAssemblyName(path).FullName);
            var production=Find(core);var bench=Find(benchmark);var tensor=Find(Path.Combine(runtime,"AiDotNet.Tensors.dll"));
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance;
            var degree=tensor.GetType("AiDotNet.Tensors.Helpers.CpuParallelSettings",true)!.GetProperty("MaxDegreeOfParallelism",flags)!;
            var workers=production.GetType("OoplesFinance.StockIndicators.Indicators.ValuesBarExecution",true)!.GetMethod("WorkerCount",flags);
            object Workers()=> workers==null ? new {available=false} : (object)new {available=true,maximum_argument=8,value=workers.Invoke(null,new object[]{8})};
            receipt["worker_policy_before_setup"]=degree.GetValue(null);receipt["effective_workers_before_setup"]=Workers();
            var type=bench.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks",true)!;var fixture=Activator.CreateInstance(type)!;
            type.GetProperty("PairId")!.SetValue(fixture,"TaLib.Candles.DarkCloudCover");type.GetProperty("Bars")!.SetValue(fixture,10000);
            var setup=type.GetMethod("Setup")!;var invoke=type.GetMethod("Ooples")!;receipt["stage"]="fixture-setup";Save();setup.Invoke(fixture,null);
            receipt["worker_policy_after_setup"]=degree.GetValue(null);receipt["effective_workers_after_setup"]=Workers();receipt["loaded_closure_after_setup"]=Closure();receipt["stage"]="warmup";Save();
            object? last=null;for(int i=0;i<100;i++) last=invoke.Invoke(fixture,null);
            receipt["stage"]="profile-ready";Save();Console.WriteLine($"PROFILE START DarkCloudCover PID={Environment.ProcessId} bars=10000");receipt["profile_start_utc"]=DateTime.UtcNow;receipt["profile_start_timestamp"]=Stopwatch.GetTimestamp();receipt["timestamp_frequency"]=Stopwatch.Frequency;
            var timer=Stopwatch.StartNew();int iterations=0;do {last=invoke.Invoke(fixture,null);iterations++;}while(timer.Elapsed.TotalSeconds<10);
            receipt["profile_end_timestamp"]=Stopwatch.GetTimestamp();receipt["profile_end_utc"]=DateTime.UtcNow;receipt["iterations"]=iterations;Console.WriteLine($"PROFILE END DarkCloudCover PID={Environment.ProcessId} iterations={iterations}");GC.KeepAlive(last);
            receipt["worker_policy_after_profile"]=degree.GetValue(null);receipt["effective_workers_after_profile"]=Workers();receipt["loaded_closure_after_profile"]=Closure();
            receipt["actual_default_framework_assemblies"]=AssemblyLoadContext.Default.Assemblies.Where(a=>!a.IsDynamic && frameworkIdentities.ContainsKey(a.FullName!)).Select(a=>new {name=a.FullName,location=a.Location,sha256=Hash(a.Location)}).ToArray();
            CheckFiles(sourceFiles);CheckFiles(runtimeFiles);receipt["protected_inputs_unchanged"]=true;receipt["runtime_unchanged"]=true;receipt["status"]="complete";receipt["stage"]="complete";Save();return 0;
        }
        catch(Exception error) {receipt["status"]="failed";receipt["error"]=error.ToString();Save();Console.Error.WriteLine(error);return 1;}
    }
    sealed class AuthenticatedContext(Dictionary<string,string> selected,Dictionary<string,string> framework,Dictionary<string,string> hashes) : AssemblyLoadContext("AuthenticatedDarkCloud6051Frozen482",false)
    {
        protected override Assembly? Load(AssemblyName name)
        {
            if(selected.TryGetValue(name.FullName,out string? path))
            { Require(Hash(path)==hashes[path],"Dependency bytes changed.");return LoadFromAssemblyPath(path); }
            static bool Compatible(AssemblyName actual,AssemblyName requested) => actual.Name==requested.Name && (actual.CultureName??"")== (requested.CultureName??"") && (actual.GetPublicKeyToken()??[]).SequenceEqual(requested.GetPublicKeyToken()??[]) && actual.Version>=requested.Version;
            Require(framework.Keys.Any(identity=>Compatible(new AssemblyName(identity),name)),"Unselected nonframework dependency: "+name);
            Assembly shared=Default.LoadFromAssemblyName(name);
            Require(framework.TryGetValue(shared.FullName!,out string? expected) && Path.GetFullPath(shared.Location).Equals(expected,StringComparison.OrdinalIgnoreCase) && Compatible(shared.GetName(),name),"Unauthenticated shared framework dependency.");
            return shared;
        }
    }
}
