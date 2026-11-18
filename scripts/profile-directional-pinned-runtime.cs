using System.Diagnostics;
using System.Buffers.Binary;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
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
        if (args.Length != 5) throw new ArgumentException("Expected input, runtime, fresh output, pair and bars.");
        string pairId=args[3];int bars=int.Parse(args[4],System.Globalization.CultureInfo.InvariantCulture);
        Require(pairId is "TaLib.Functions.PlusDI" or "TaLib.Functions.Adx", "Unapproved pair.");Require(bars is 1000 or 10000,"Unapproved bar count.");
        string input=Path.GetFullPath(args[0]), runtime=Path.GetFullPath(args[1]), output=Path.GetFullPath(args[2]);
        Require(!Directory.Exists(output), "Output must be fresh.");
        Directory.CreateDirectory(output);
        var receipt=new Dictionary<string,object?> { ["diagnostic_only"]=true,["qualification"]=false,["performance_claim"]=false,["status"]="running",["stage"]="start",["host_clr"]=Environment.Version.ToString(),["process_id"]=Environment.ProcessId,["environment_processor_count"]=Environment.ProcessorCount,["env_aidotnet_cpu_threads"]=Environment.GetEnvironmentVariable("AIDOTNET_CPU_THREADS"),["production_builds"]=0,["diagnostic_driver_builds"]=1,["tests"]=0,["timed_benchmark_cases"]=0,["warmups"]=1000,["profile_seconds"]=10,["pair"]=pairId,["bars"]=bars,["workflow_commit"]=Environment.GetEnvironmentVariable("GITHUB_SHA"),["workflow_run"]=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),["attribution"]="Fixture boundaries and observed settings only; sampling semantics come from the collector. No qualification or latency claim.",["symbol_fidelity"]="Actual canonical9e core/PDB identity verified; no runtime PDB replaced." };
        void Save() { string temp=Path.Combine(output,"replay.json.new"); File.WriteAllText(temp,JsonSerializer.Serialize(receipt,new JsonSerializerOptions {WriteIndented=true})); File.Move(temp,Path.Combine(output,"replay.json"),true); }
        Save();
        try
        {
            Require(Environment.Version.ToString()=="10.0.15","Expected CLR 10.0.15.");
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
                Require(!System.Text.RegularExpressions.Regex.IsMatch((string)entry.Key,@"^(DOTNET_|COMPlus_).*(Tiered|TC_|Jit|ReadyToRun|GC|Roll_Forward|RollForward)",System.Text.RegularExpressions.RegexOptions.IgnoreCase),"Inherited runtime override requires review: "+entry.Key);
            string proofPath=Path.Combine(input,"manifest.json");
            Require(Hash(proofPath)=="192cba71853935788aba889e0d4cb9bbd6b75545e007e7871824ef4c0348536c","Wrong input proof.");
            using var proof=JsonDocument.Parse(File.ReadAllText(proofPath)); var p=proof.RootElement;
            const string coreHash="ed8a61ad920d1745a426b572db2e0dfb67764e4328304dbf9c5ad4b3583f80b7", benchHash="07e764bd27110b4452e1f181f466a6ea0e60b4ae4667d01b6c75f468ae8aee07";
            Require(p.GetProperty("source").GetString()=="9e914007c3438d9f251f3fab9ef321d49c22de83" && p.GetProperty("run_id").GetString()=="44217513798" && p.GetProperty("workflow_head").GetString()=="2a1737f30e5e1164189d0209b39ea46bf38c1239" && p.GetProperty("source_sha256").GetString()=="79ccf727c22cb156a0cb75cdd0f764da037603d7b74c8cd9cb075da2c6498815" && p.GetProperty("status").GetString()=="cpu-correctness-complete" && p.GetProperty("sdk").GetString()=="10.0.403","Wrong canonical CPU identities.");
            var sourceFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); var runtimeFiles=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            string Contained(string root,string relative) { string path=Path.GetFullPath(Path.Combine(root,relative)); Require(path.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"Escaped pinned root.");return path; }
            const string prefix="benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/";int count=0;
            foreach(var entry in p.GetProperty("runtime_files").EnumerateObject()) {
                if(!entry.Name.StartsWith(prefix,StringComparison.Ordinal))continue;
                string hash=entry.Value.GetString()!;sourceFiles[Contained(input,"blobs/"+hash)]=hash;runtimeFiles.Add(Contained(runtime,entry.Name[prefix.Length..]),hash);count++;
            }
            Require(count==100,"Expected 100 actual canonical benchmark runtime files.");
            sourceFiles[proofPath]="192cba71853935788aba889e0d4cb9bbd6b75545e007e7871824ef4c0348536c";
            string core=Path.Combine(runtime,"OoplesFinance.StockIndicators.dll"), benchmark=Path.Combine(runtime,"OoplesFinance.StockIndicators.CompetitorBenchmarks.dll");
            Require(runtimeFiles[benchmark]==benchHash && runtimeFiles[core]==coreHash,"Wrong canonical core/harness.");
            void CheckFiles(Dictionary<string,string> files) { foreach(var item in files) Require(Hash(item.Key)==item.Value,"Changed protected file: "+item.Key); }
            const string matchingPdbHash="00704ff0b4c525d0aeb66f720c59230189ed76609ff664cdfb8d1c5c8d2c17f1";
            string matchingPdb=Path.Combine(runtime,"OoplesFinance.StockIndicators.pdb");
            Require(Hash(matchingPdb)==matchingPdbHash,"Actual canonical production PDB required.");
            Require(runtimeFiles[matchingPdb]==matchingPdbHash,"PDB absent from actual runtime manifest.");
            using(var peStream=File.OpenRead(core))
            using(var pe=new PEReader(peStream))
            using(var pdbStream=File.OpenRead(matchingPdb))
            using(var provider=MetadataReaderProvider.FromPortablePdbStream(pdbStream))
            {
                var entries=pe.ReadDebugDirectory().Where(entry=>entry.Type==DebugDirectoryEntryType.CodeView).ToArray();
                Require(entries.Length==1,"Expected one CodeView entry.");
                var cv=pe.ReadCodeViewDebugDirectoryData(entries[0]);var id=provider.GetMetadataReader().DebugMetadataHeader!.Id;
                Require(id.Length==20,"Expected portable PDB ID.");
                var guid=new Guid(id.AsSpan().Slice(0,16));uint stamp=BinaryPrimitives.ReadUInt32LittleEndian(id.AsSpan().Slice(16,4));
                Require(cv.Guid==guid && entries[0].Stamp==stamp && cv.Age==1,"Canonical PE/PDB identity mismatch.");
                receipt["symbol_identity"]=new {guid=guid.ToString("N"),stamp=stamp.ToString("x8"),age=cv.Age,pdb_sha256=matchingPdbHash,production_sha256=coreHash,protected_runtime_pdb_replaced=false};
            }
            CheckFiles(sourceFiles);CheckFiles(runtimeFiles);
            receipt["protected_input_hashes"]=sourceFiles;receipt["runtime_files"]=runtimeFiles;receipt["protected_input_files"]=sourceFiles.Count;receipt["runtime_file_count"]=runtimeFiles.Count;receipt["production_sha256"]=coreHash;receipt["frozen_benchmark_sha256"]=benchHash;receipt["input_provenance_sha256"]=Hash(proofPath);receipt["source"]="9e914007c3438d9f251f3fab9ef321d49c22de83";receipt["compiled_source"]=receipt["source"];receipt["frozen_harness_source"]=receipt["source"];
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
            object Workers() { Require(workers!=null && Convert.ToInt32(degree.GetValue(null))==2 && Environment.ProcessorCount==4,"Expected unchanged DOP2/CPU4."); int value=Convert.ToInt32(workers!.Invoke(null,new object[]{8}));Require(value==2,"Expected two effective workers.");return new {available=true,maximum_argument=8,value}; }
            receipt["worker_policy_before_setup"]=degree.GetValue(null);receipt["effective_workers_before_setup"]=Workers();
            var type=bench.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks",true)!;var fixture=Activator.CreateInstance(type)!;
            type.GetProperty("PairId")!.SetValue(fixture,pairId);type.GetProperty("Bars")!.SetValue(fixture,bars);
            var setup=type.GetMethod("Setup")!;var invoke=type.GetMethod("Ooples")!;receipt["stage"]="fixture-setup";Save();setup.Invoke(fixture,null);
            object pair=type.GetField("_pair",flags)!.GetValue(fixture)!;
            var library=(Delegate)pair.GetType().GetProperty("Library",flags)!.GetValue(pair)!;
            object factory=library.Target??throw new InvalidOperationException("Directional factory closure missing.");
            int Parameter(string name)=>Convert.ToInt32(factory.GetType().GetField(name,flags)!.GetValue(factory));
            int measure=pairId=="TaLib.Functions.PlusDI"?2:5;
            Require(library.Method.DeclaringType!.FullName!.StartsWith("OoplesFinance.StockIndicators.CompetitorBenchmarks.TaDirectionalComparison",StringComparison.Ordinal),"Wrong directional adapter.");
            Require(Parameter("measure")==measure && Parameter("unstable")==0 && Convert.ToInt32(type.GetField("Period",flags)!.GetRawConstantValue())==20,"Directional factory parameters changed.");
            receipt["factory_parameters"]=new {measure,unstable=0,period=20,adapter="TaDirectionalComparison",selection="Value"};
            receipt["worker_policy_after_setup"]=degree.GetValue(null);receipt["effective_workers_after_setup"]=Workers();receipt["loaded_closure_after_setup"]=Closure();receipt["stage"]="warmup";Save();
            object? last=null;for(int i=0;i<1000;i++) last=invoke.Invoke(fixture,null);
            receipt["stage"]="profile-ready";Save();Console.WriteLine($"PROFILE START Directional pair={pairId} PID={Environment.ProcessId} bars={bars}");receipt["profile_start_utc"]=DateTime.UtcNow;receipt["profile_start_timestamp"]=Stopwatch.GetTimestamp();receipt["timestamp_frequency"]=Stopwatch.Frequency;
            var timer=Stopwatch.StartNew();int iterations=0;do {last=invoke.Invoke(fixture,null);iterations++;}while(timer.Elapsed.TotalSeconds<10);
            receipt["profile_end_timestamp"]=Stopwatch.GetTimestamp();receipt["profile_end_utc"]=DateTime.UtcNow;receipt["iterations"]=iterations;Console.WriteLine($"PROFILE END Directional pair={pairId} PID={Environment.ProcessId} bars={bars} iterations={iterations}");GC.KeepAlive(last);
            receipt["worker_policy_after_profile"]=degree.GetValue(null);receipt["effective_workers_after_profile"]=Workers();receipt["loaded_closure_after_profile"]=Closure();
            receipt["actual_default_framework_assemblies"]=AssemblyLoadContext.Default.Assemblies.Where(a=>!a.IsDynamic && frameworkIdentities.ContainsKey(a.FullName!)).Select(a=>new {name=a.FullName,location=a.Location,sha256=Hash(a.Location)}).ToArray();
            CheckFiles(sourceFiles);CheckFiles(runtimeFiles);receipt["protected_inputs_unchanged"]=true;receipt["runtime_unchanged"]=true;receipt["status"]="complete";receipt["stage"]="complete";Save();return 0;
        }
        catch(Exception error) {receipt["status"]="failed";receipt["error"]=error.ToString();Save();Console.Error.WriteLine(error);return 1;}
    }
    sealed class AuthenticatedContext(Dictionary<string,string> selected,Dictionary<string,string> framework,Dictionary<string,string> hashes) : AssemblyLoadContext("AuthenticatedDirectional9eCanonical07e",false)
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
