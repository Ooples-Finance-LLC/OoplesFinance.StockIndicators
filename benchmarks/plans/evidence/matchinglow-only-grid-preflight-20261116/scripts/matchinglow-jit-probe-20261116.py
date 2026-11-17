"""Source-bound diagnostic JIT replay of existing candle benchmark builder paths."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

OPS = Path(r'C:\Users\cheat\source\stock-indicators-operations')
ZROOT = Path(r'Z:\StockIndicators-Archives\active-operations')
REMOTE = 'repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators'
RUNTIME_PREFIX = 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
OPERATIONS = {'SeparatingLines': 'SharedMatchedLinesBenchmarks',
    'MatchingLow': 'SharedSequenceCandleBenchmarks', 'StickSandwich': 'SharedSequenceCandleBenchmarks'}
CHILD = r'''
param([string]$Runtime,[string]$ExpectedProduction,[string]$Operation,[int]$Workers,[string]$Receipt)
$ErrorActionPreference='Stop'
$hostProof=[ordered]@{runtime_version=[Environment]::Version.ToString();framework_description=[Runtime.InteropServices.RuntimeInformation]::FrameworkDescription;corelib_product=[Diagnostics.FileVersionInfo]::GetVersionInfo([object].Assembly.Location).ProductVersion;host_path=[Diagnostics.Process]::GetCurrentProcess().MainModule.FileName;pwsh_version=$PSVersionTable.PSVersion.ToString()}
if($hostProof.runtime_version -ne '10.0.15' -or $hostProof.framework_description -ne '.NET 10.0.15') {
 [IO.File]::WriteAllText($Receipt,([ordered]@{status='inconclusive-runtime-mismatch';host=$hostProof;qualification=$false;replay_executed=$false}|ConvertTo-Json -Depth 6));exit 0
}
if((Get-FileHash -LiteralPath (Join-Path $Runtime 'OoplesFinance.StockIndicators.dll') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $ExpectedProduction){throw 'Wrong production DLL'}
$resolve=[Func[Runtime.Loader.AssemblyLoadContext,Reflection.AssemblyName,Reflection.Assembly]]{
 param($ctx,$name)
 $candidate=Join-Path $Runtime ($name.Name+'.dll')
 if(Test-Path -LiteralPath $candidate){return $ctx.LoadFromAssemblyPath($candidate)}
 return $null
}
[Runtime.Loader.AssemblyLoadContext]::Default.add_Resolving($resolve)
$flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
$ns='OoplesFinance.StockIndicators.CompetitorBenchmarks.'
$fixture=$null;$policy=$null;$degree=$null;$prior=$null
try{
 $prod=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $Runtime 'OoplesFinance.StockIndicators.dll'))
 $bench=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $Runtime 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'))
 $tensor=[Runtime.Loader.AssemblyLoadContext]::Default.LoadFromAssemblyPath((Join-Path $Runtime 'AiDotNet.Tensors.dll'))
 $degree=$tensor.GetType('AiDotNet.Tensors.Helpers.CpuParallelSettings',$true).GetProperty('MaxDegreeOfParallelism',$flags)
 $prior=$degree.GetValue($null)
 # TA-Lib compatibility/unstable EMA settings, not a GPU/backend switch.
 $policy=[Activator]::CreateInstance($bench.GetType($ns+'TripleRateComparison+Settings',$true),$flags,$null,[object[]]@($false,[int]0),$null)
 $typeName=if($Operation -eq 'SeparatingLines'){'SharedMatchedLinesBenchmarks'}else{'SharedSequenceCandleBenchmarks'}
 $type=$bench.GetType($ns+$typeName,$true);$fixture=[Activator]::CreateInstance($type)
 $type.GetProperty('Operation').SetValue($fixture,'TaLib.Candles.'+$Operation)
 $type.GetProperty('Count').SetValue($fixture,[int]8193)
 $null=$type.GetMethod('Setup').Invoke($fixture,$null)
 # Setup itself forces8; requested replay configuration is applied AFTER setup.
 $degree.SetValue($null,[int]$Workers)
 if([int]$degree.GetValue($null) -ne $Workers){throw 'Worker API configuration did not stick'}
 $rows=[Collections.Generic.List[object]]::new()
 foreach($name in @('BuilderFull','BuilderLatestOnly')){
  $method=$type.GetMethod($name)
  if($null -eq $method){throw 'Expected actual fixture builder method absent'}
  if([int]$degree.GetValue($null) -ne $Workers){throw 'Worker configuration changed'}
  for($i=0;$i -lt 1000;$i++){$run=$method.Invoke($fixture,$null);$run.Dispose()}
  # Diagnostic promotion opportunity only; never report elapsed time as a benchmark.
  $clock=[Diagnostics.Stopwatch]::StartNew();$calls=0
  do{$run=$method.Invoke($fixture,$null);$run.Dispose();$calls++}while($clock.ElapsedMilliseconds -lt 5000)
  if([int]$degree.GetValue($null) -ne $Workers){throw 'Worker configuration changed during replay'}
  $rows.Add([pscustomobject]@{method=$name;warmup_calls=1000;promotion_calls=$calls;configured_worker_limit=$Workers})
 }
 $result=[ordered]@{status='replayed';host=$hostProof;fixture=$type.FullName;operation=$Operation;count=8193;configured_worker_limit=$Workers;rows=@($rows.ToArray());production_sha256=$ExpectedProduction;qualification=$false;performance_claim=$false;new_compilation=$false;worker_thread_execution_proven=$false}
 [IO.File]::WriteAllText($Receipt,($result|ConvertTo-Json -Depth 8))
}finally{
 if($null -ne $fixture){$null=$fixture.GetType().GetMethod('Cleanup').Invoke($fixture,$null)}
 if($null -ne $policy){$policy.Dispose()}
 if($null -ne $degree){$degree.SetValue($null,$prior)}
 [Runtime.Loader.AssemblyLoadContext]::Default.remove_Resolving($resolve)
}
'''


def digest(path):
    with Path(path).open('rb') as stream: return hashlib.file_digest(stream, 'sha256').hexdigest()


def write(path, data):
    temporary = path.with_suffix(path.suffix+'.tmp')
    assert not temporary.exists()
    temporary.write_bytes((json.dumps(data, indent=2)+'\n').encode()); os.replace(temporary, path)


def bodies(text):
    starts = list(re.finditer(r'^; Assembly listing for method ', text, re.M))
    for index, match in enumerate(starts):
        body = text[match.start():starts[index+1].start() if index+1 < len(starts) else len(text)]
        header = body.splitlines()[0]
        if re.search(r'\(Tier1(?:-OSR)?\)$', header) and '; Total bytes of code' in body:
            yield header, body


def inspect_codegen(log, folder):
    text = log.read_text(encoding='utf-8', errors='replace') if log.exists() else ''
    complete = list(bodies(text)); methods = {}
    forbidden = ('MatchingLowGridEligible', 'TryMatchingLowGridBlock', 'ComputeMatchingLowGrid', 'MatchingLowCandle')
    for name in ('FillBoundedWindow', 'ComputeBoundedRegion'):
        selected = [(h,b) for h,b in complete if ':'+name+'[' in h and '+MatchedLinesKernel]' in h]
        evidence = []
        for index,(header,body) in enumerate(selected):
            target = folder/(name+'-MatchedLinesKernel-Tier1-'+str(index)+'.asm')
            target.write_bytes(body.encode())
            evidence.append(dict(header=header, file=target.name, sha256=digest(target),
                matchinglow_symbols_present=[token for token in forbidden if token in body]))
        methods[name] = evidence
    callers = [h for h,b in complete if 'MatchedLinesKernel' in h and 'FillBoundedWindow' in h and ':FillBoundedWindow[' not in h]
    return dict(methods=methods, captured_tier1_caller_headers=callers,
        both_concrete_bodies_complete=all(methods.values()),
        matchinglow_symbols_absent=all(not e['matchinglow_symbols_present'] for entries in methods.values() for e in entries),
        matchinglow_route_complete_tier1=any(':ComputeMatchingLowGrid(' in h for h,b in complete),
        inlined_or_absent_body_is_inconclusive=True)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    for name in ('manifest','runtime','source-repo','out'): parser.add_argument('--'+name,type=Path,required=True)
    for name in ('manifest-sha','source','producer','run','artifact-id'): parser.add_argument('--'+name,required=True)
    parser.add_argument('--pwsh',default='pwsh')
    args=parser.parse_args()
    assert re.fullmatch('[a-f0-9]{40}',args.source) and re.fullmatch('[a-f0-9]{64}',args.manifest_sha)
    assert re.fullmatch('[a-f0-9]{40}',args.producer)
    assert args.run.isdigit() and args.artifact_id.isdigit()
    runtime=args.runtime.resolve();out=args.out.resolve()
    assert any(out.is_relative_to(root.resolve()) and out != root.resolve() for root in (OPS,ZROOT))
    assert not out.exists() and not out.is_relative_to(runtime)
    assert digest(args.manifest)==args.manifest_sha
    manifest=json.loads(args.manifest.read_text()); assert manifest['source']==args.source and str(manifest['run_id'])==args.run
    def api(path):return json.loads(subprocess.check_output(['gh','api',REMOTE+path]))
    run=api('/actions/runs/'+args.run);artifact=api('/actions/artifacts/'+args.artifact_id)
    assert run['head_sha']==manifest['workflow_head']==args.producer
    assert (run['status'],run['conclusion'])==('completed','success')
    assert artifact['workflow_run']['id']==int(args.run) and not artifact['expired']
    expected={name[len(RUNTIME_PREFIX):]:sha for name,sha in manifest['runtime_files'].items() if name.startswith(RUNTIME_PREFIX)}
    assert {'OoplesFinance.StockIndicators.dll','OoplesFinance.StockIndicators.CompetitorBenchmarks.dll','AiDotNet.Tensors.dll'} <= expected.keys()
    assert expected and all(digest(runtime/name)==sha for name,sha in expected.items())
    source_path='src/Indicators/ValuesBarExecution.BoundedWindows.cs'
    source=subprocess.check_output(['git','show',args.source+':'+source_path],cwd=args.source_repo,text=True)
    # Source-bound proof of the compile-time generic discriminator, before the dynamic test.
    guards=re.findall(r'typeof\(T\)\s*==\s*typeof\(SequenceKernel\)\s*&&\s*MatchingLowGridEligible\(',source)
    assert len(guards)==2, 'Expected first-operand SequenceKernel guards in both actual callers'
    out.mkdir(parents=True)
    child_folder=OPS/'matchinglow-jit-children'/out.name
    assert not child_folder.exists(), 'Fresh local child directory required; preserve earlier attempts'
    child_folder.mkdir(parents=True)
    child=child_folder/'reflection-child.ps1'
    child_temporary=child.with_suffix('.ps1.tmp')
    child_temporary.write_bytes(CHILD.encode());os.replace(child_temporary,child)
    archived_child=out/'reflection-child.ps1';archived_child.write_bytes(CHILD.encode())
    assert digest(child)==digest(archived_child)
    (out/'bounded-windows-source.cs').write_bytes(source.encode())
    removed=[key for key in os.environ if key.upper().startswith(('COMPLUS_','DOTNET_TIERED','DOTNET_TC_','DOTNET_JIT','DOTNET_READYTORUN','DOTNET_ENABLE','DOTNET_GC')) or key.upper()=='DOTNET_PROCESSOR_COUNT']
    clean={key:value for key,value in os.environ.items() if key not in removed}
    rows=[];runtime_mismatch=False
    for operation,fixture in OPERATIONS.items():
        if runtime_mismatch:break
        for workers in (1,2,8):
            folder=out/(operation+'-workers'+str(workers));folder.mkdir()
            jit=folder/'jit.log';receipt=folder/'replay.json'
            env=dict(clean,DOTNET_JitDisasm='*BoundedWindow* *ComputeBoundedRegion* *ComputeMatchingLowGrid*',
                DOTNET_JitDisasmAssemblies='OoplesFinance.StockIndicators',DOTNET_JitDisasmDiffable='1',DOTNET_JitStdOutFile=str(jit))
            with (folder/'child.log').open('wb') as log:
                subprocess.run([args.pwsh,'-NoProfile','-File',str(child),'-Runtime',str(runtime),
                    '-ExpectedProduction',expected['OoplesFinance.StockIndicators.dll'],'-Operation',operation,
                    '-Workers',str(workers),'-Receipt',str(receipt)],env=env,stdout=log,stderr=subprocess.STDOUT,check=True,timeout=180)
            replay=json.loads(receipt.read_text());runtime_mismatch=replay['status']=='inconclusive-runtime-mismatch'
            if not runtime_mismatch:
                assert replay['fixture'].endswith('.'+fixture) and replay['operation']==operation and replay['configured_worker_limit']==workers
                assert {r['method'] for r in replay['rows']}=={'BuilderFull','BuilderLatestOnly'}
            rows.append(dict(operation=operation,workers=workers,replay=replay,codegen=inspect_codegen(jit,folder)))
            if runtime_mismatch:break
    assert all(digest(runtime/name)==sha for name,sha in expected.items()),'Authenticated runtime changed'
    assert digest(args.manifest)==args.manifest_sha
    assert digest(child)==digest(archived_child)==hashlib.sha256(CHILD.encode()).hexdigest()
    matched=[r for r in rows if r['operation']=='SeparatingLines']
    structural=not runtime_mismatch and len(rows)==9 and len(matched)==3 and all(
        r['codegen']['both_concrete_bodies_complete'] and r['codegen']['matchinglow_symbols_absent'] for r in matched)
    proof=dict(kind='matchinglow-source-bound-default-runtime-jit',source=args.source,source_fingerprint=manifest['source_sha256'],
        run_id=args.run,producer_workflow_head=args.producer,compiled_source=args.source,
        artifact=artifact,manifest_sha256=args.manifest_sha,runtime_files=expected,
        production_sha256=expected['OoplesFinance.StockIndicators.dll'],source_guard_count=len(guards),
        child_path=str(child),child_sha256=digest(child),archived_child_path=str(archived_child),
        archived_child_sha256=digest(archived_child),source_file=source_path,source_capture_sha256=digest(out/'bounded-windows-source.cs'),
        removed_override_names=removed,diagnostic_settings=['DOTNET_JitDisasm','DOTNET_JitDisasmAssemblies','DOTNET_JitDisasmDiffable','DOTNET_JitStdOutFile'],
        rows=rows,structural_candidate_for_parent_review=structural,accepted=False,qualification=False,performance_claim=False,
        new_compilation=False,worker_thread_execution_proven=False,
        limitation='Absence-only grep cannot pass. Both complete concrete default-runtime Tier1 matched-lines bodies plus source-bound compile-time guards are required; inlined/absent/truncated bodies require separate explicit caller review. Requested API worker limits are not observed thread counts. Runtime mismatch is inconclusive. No timing/native-win claim.')
    write(out/'jit-proof.json',proof)
    print(json.dumps(dict(structural_candidate_for_parent_review=structural,runtime_mismatch=runtime_mismatch,rows=len(rows),accepted=False)))


if __name__=='__main__':main()
