using System.Diagnostics;
using System.Collections;
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
        if (args.Length != 3) throw new ArgumentException("Expected input, runtime and fresh output directories.");
        string input=Path.GetFullPath(args[0]), runtime=Path.GetFullPath(args[1]), output=Path.GetFullPath(args[2]);
        Require(!Directory.Exists(output), "Output must be fresh.");
        Directory.CreateDirectory(output);
        var receipt=new Dictionary<string,object?> { ["diagnostic_only"]=true,["qualification"]=false,["performance_claim"]=false,["status"]="running",["stage"]="start",["host_clr"]=Environment.Version.ToString(),["process_id"]=Environment.ProcessId,["environment_processor_count"]=Environment.ProcessorCount,["env_aidotnet_cpu_threads"]=Environment.GetEnvironmentVariable("AIDOTNET_CPU_THREADS"),["production_builds"]=0,["diagnostic_driver_builds"]=1,["tests"]=0,["timed_benchmark_cases"]=0,["warmups"]=20000,["profile_seconds"]=10,["phase_target_seconds"]=2.5,["phase_invocation_counts"]=new[]{1,64,64,1},["all_underlying_outputs_verified"]=false,["pair"]="TaLib.Candles.UpsideGapTwoCrows",["bars"]=10000,["workflow_commit"]=Environment.GetEnvironmentVariable("GITHUB_SHA"),["workflow_run"]=Environment.GetEnvironmentVariable("GITHUB_RUN_ID"),["attribution"]="Fixture boundaries and observed settings only; sampling semantics come from the collector. No qualification or latency claim.",["symbol_fidelity"]="External matching old4d/PDB5c identity verified; frozen runtime PDB retained." };
        void Save() { string temp=Path.Combine(output,"replay.json.new"); File.WriteAllText(temp,JsonSerializer.Serialize(receipt,new JsonSerializerOptions {WriteIndented=true,IncludeFields=true})); File.Move(temp,Path.Combine(output,"replay.json"),true); }
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
            const string matchingPdbHash="5c0cbf0fd670fce8a396794a86aaff7043a9d0a9e9fc516eb679e8dd5180c76f";
            string matchingPdb=Path.Combine(Environment.GetEnvironmentVariable("_NT_SYMBOL_PATH")??"","OoplesFinance.StockIndicators.pdb");
            Require(Hash(matchingPdb)==matchingPdbHash && !Path.GetFullPath(matchingPdb).StartsWith(runtime+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"External matching PDB required.");
            sourceFiles.Add(matchingPdb,matchingPdbHash);
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
                Require(cv.Guid==guid && guid.ToString("N")=="73be8da336c04136b9b4b3bb140df441" && entries[0].Stamp==stamp && stamp==0xbf4018fc && cv.Age==1,"Old PE/PDB identity mismatch.");
                receipt["symbol_identity"]=new {guid=guid.ToString("N"),stamp=stamp.ToString("x8"),age=cv.Age,pdb_sha256=matchingPdbHash,production_sha256=coreHash,protected_runtime_pdb_replaced=false};
            }
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
            object Workers() { Require(workers!=null && Convert.ToInt32(degree.GetValue(null))==2 && Environment.ProcessorCount==4,"Expected unchanged DOP2/CPU4."); int value=Convert.ToInt32(workers!.Invoke(null,new object[]{8}));Require(value==2,"Expected two effective workers.");return new {available=true,maximum_argument=8,value}; }
            receipt["worker_policy_before_setup"]=degree.GetValue(null);receipt["effective_workers_before_setup"]=Workers();
            var type=bench.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.LibraryPairBenchmarks",true)!;var fixture=Activator.CreateInstance(type)!;
            type.GetProperty("PairId")!.SetValue(fixture,"TaLib.Candles.UpsideGapTwoCrows");type.GetProperty("Bars")!.SetValue(fixture,10000);
            var setup=type.GetMethod("Setup")!;var invoke=type.GetMethod("Ooples")!;receipt["stage"]="fixture-setup";Save();setup.Invoke(fixture,null);
            object pair=type.GetField("_pair",flags)!.GetValue(fixture)!;
            var library=(Delegate)pair.GetType().GetProperty("Library",flags)!.GetValue(pair)!;
            object factory=library.Target??throw new InvalidOperationException("Frozen candle factory closure missing.");
            string name=(string)factory.GetType().GetField("n",flags)!.GetValue(factory)!;
            Require(name=="UpsideGapTwoCrows","Frozen candle factory changed.");
            var factoryType=bench.GetType("OoplesFinance.StockIndicators.CompetitorBenchmarks.TripleBodyComparison",true)!;
            var parameters=factoryType.GetMethod("Create",flags)!.GetParameters();
            Require(parameters.Length==3 && Convert.ToInt32(parameters[1].DefaultValue)==10 && Convert.ToInt32(parameters[2].DefaultValue)==10,"Frozen candle body periods changed.");
            receipt["factory_parameters"]=new {name,long_period=10,short_period=10};
            receipt["worker_policy_after_setup"]=degree.GetValue(null);receipt["effective_workers_after_setup"]=Workers();receipt["loaded_closure_after_setup"]=Closure();receipt["stage"]="warmup";Save();
            const int capacity=65536;
            var records=new BlockRecord[capacity];var phases=new PhaseRecord[4];var endpoints=new object[4];
            int[] invocationCounts={1,64,64,1};
            object? last=null;for(int i=0;i<20000;i++) last=invoke.Invoke(fixture,null);
            object warmReference=last!;
            receipt["stage"]="profile-ready";Save();
            Console.WriteLine($"PROFILE START CandlePhase PID={Environment.ProcessId} bars=10000");
            receipt["profile_start_utc"]=DateTime.UtcNow;receipt["profile_start_timestamp"]=Stopwatch.GetTimestamp();receipt["timestamp_frequency"]=Stopwatch.Frequency;
            int used=0;long totalCalls=0;long targetTicks=(long)(Stopwatch.Frequency*2.5);
            for(int phase=0;phase<4;phase++)
            {
                int invocationsPerBlock=invocationCounts[phase],firstRecord=used;long calls=0,timedTicks=0;
                long phaseStart=Stopwatch.GetTimestamp();
                do
                {
                    if(used>=capacity)
                    {
                        receipt["record_capacity_exceeded"]=true;receipt["block_records"]=records;receipt["phases"]=phases;
                        receipt["completed_phase_count"]=phase;receipt["partial_phase_underlying_calls"]=calls;
                        throw new InvalidOperationException("Fixed preallocated block-record capacity exhausted; no retry.");
                    }
                    long start=Stopwatch.GetTimestamp();
                    for(int call=0;call<invocationsPerBlock;call++) last=invoke.Invoke(fixture,null);
                    long end=Stopwatch.GetTimestamp();
                    records[used++]=new BlockRecord {start_qpc=start,end_qpc=end,phase=phase};
                    timedTicks+=end-start;calls+=invocationsPerBlock;
                }while(Stopwatch.GetTimestamp()-phaseStart<targetTicks);
                long phaseEnd=Stopwatch.GetTimestamp();
                endpoints[phase]=last!;totalCalls+=calls;
                phases[phase]=new PhaseRecord {phase=phase,invocation_count=invocationsPerBlock,start_qpc=phaseStart,end_qpc=phaseEnd,
                    first_record=firstRecord,record_count=used-firstRecord,underlying_calls=calls,timed_block_ticks=timedTicks,
                    observer_gap_ticks=phaseEnd-phaseStart-timedTicks};
            }
            receipt["profile_end_timestamp"]=Stopwatch.GetTimestamp();receipt["profile_end_utc"]=DateTime.UtcNow;
            Console.WriteLine($"PROFILE END CandlePhase PID={Environment.ProcessId} calls={totalCalls}");
            receipt["iterations"]=used;receipt["measured_underlying_calls"]=totalCalls;receipt["block_record_capacity"]=capacity;
            receipt["realized_profile_seconds"]=(Convert.ToInt64(receipt["profile_end_timestamp"])-Convert.ToInt64(receipt["profile_start_timestamp"]))/(double)Stopwatch.Frequency;
            receipt["phases"]=phases;
            receipt["block_records"]=records.Take(used).ToArray();
            receipt["observer_limit"]="Record writes and phase bookkeeping are outside invocation block timers but inside CPU phase windows; observer_gap_ticks is reported, never subtracted as a correction.";
            var warmSnapshot=Snapshot(warmReference);var snapshots=endpoints.Select(Snapshot).ToArray();
            foreach(var snapshot in snapshots)
            {
                Require(snapshot.Count==warmSnapshot.Count,"Endpoint column count changed.");
                foreach(var entry in warmSnapshot)
                    Require(snapshot.TryGetValue(entry.Key,out var actual) && actual.FirstValid==entry.Value.FirstValid
                        && actual.Values.AsSpan().SequenceEqual(entry.Value.Values)
                        && ((actual.Present==null && entry.Value.Present==null) || (actual.Present!=null && entry.Value.Present!=null && actual.Present.AsSpan().SequenceEqual(entry.Value.Present))),
                        "Phase endpoint output differs from warm reference.");
            }
            receipt["source_outputs"]=snapshots;receipt["endpoint_outputs_verified"]=4;
            GC.KeepAlive(warmReference);GC.KeepAlive(endpoints);
            receipt["worker_policy_after_profile"]=degree.GetValue(null);receipt["effective_workers_after_profile"]=Workers();receipt["loaded_closure_after_profile"]=Closure();
            receipt["actual_default_framework_assemblies"]=AssemblyLoadContext.Default.Assemblies.Where(a=>!a.IsDynamic && frameworkIdentities.ContainsKey(a.FullName!)).Select(a=>new {name=a.FullName,location=a.Location,sha256=Hash(a.Location)}).ToArray();
            CheckFiles(sourceFiles);CheckFiles(runtimeFiles);receipt["protected_inputs_unchanged"]=true;receipt["runtime_unchanged"]=true;receipt["status"]="complete";receipt["stage"]="complete";Save();return 0;
        }
        catch(Exception error) {receipt["status"]="failed";receipt["error"]=error.ToString();Save();Console.Error.WriteLine(error);return 1;}
    }
    struct BlockRecord { public long start_qpc,end_qpc; public int phase; }
    struct PhaseRecord
    {
        public int phase,invocation_count,first_record,record_count;
        public long start_qpc,end_qpc,underlying_calls,timed_block_ticks,observer_gap_ticks;
    }
    record Column(int FirstValid,byte[] Values,byte[]? Present);
    static Dictionary<string,Column> Snapshot(object result)
    {
        var outputs=(IDictionary)result.GetType().GetProperty("Outputs")!.GetValue(result)!;
        var snapshot=new Dictionary<string,Column>();
        foreach(DictionaryEntry entry in outputs)
        {
            object column=entry.Value!;var type=column.GetType();var values=(double[])type.GetProperty("Values")!.GetValue(column)!;
            byte[] bytes=new byte[values.Length*8];Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
            var present=(bool[]?)type.GetProperty("Present")!.GetValue(column);byte[]? mask=present==null?null:new byte[present.Length];
            if(present!=null) Buffer.BlockCopy(present,0,mask!,0,mask!.Length);
            snapshot.Add((string)entry.Key,new Column((int)type.GetProperty("FirstValid")!.GetValue(column)!,bytes,mask));
        }
        return snapshot;
    }
    sealed class AuthenticatedContext(Dictionary<string,string> selected,Dictionary<string,string> framework,Dictionary<string,string> hashes) : AssemblyLoadContext("AuthenticatedCandlePhase6051Frozen482",false)
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
