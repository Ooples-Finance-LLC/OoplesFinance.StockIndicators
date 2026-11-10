"""Frozen existing-binary observations; deliberately has no acceptance/import path."""
import argparse, collections, datetime, fnmatch, hashlib, json, os, platform, re, shutil, subprocess, time, uuid
from pathlib import Path
import psutil
from hosted_timing import pipeline as p
from hosted_timing.checkpoints import case_key
from hosted_timing.verify import check_log, sample

PLAN=p.REPO/'benchmarks/plans/log10-profile-diagnostic-20261110.json'
PLAN_SHA='17c8289b39d10902952c0f71b64b1289f9dec6d6577487cd15c60d57fbfe15f2'
POLICY_NAMES=('TieredPGO','TieredCompilation','ReadyToRun','TC_QuickJit','TC_QuickJitForLoops','TC_OnStackReplacement','JitStress','GCStress','JitDisasm','JitDisasmAssemblies','JitStdOutFile')

def controls(env):
    return {k:v for k,v in env.items() if any(k.lower()==(prefix+name).lower() for prefix in ('DOTNET_','COMPlus_') for name in POLICY_NAMES)}

def validate_plan():
    assert p.digest(PLAN)==PLAN_SHA,'Frozen plan changed'
    d=p.read(PLAN)
    assert d['diagnostic_only'] and d['status']=='frozen' and d['runtime_policy_overrides']=={}
    assert d['order']==['old','new','new','old'] and d['jobs']==d['max_parallel']==2
    assert d['policy']==dict(default_runtime=True,quiet_samples=12,max_cpu_percent=15,fixed_invocation_warmups=1000,builder_warmups=10,measured_iterations=10)
    assert d['trace_replay']['chunks']==10 and d['trace_replay']['invocations_per_chunk']==1000
    assert d['trace_limit_seconds']==60 and d['bars']==[10000]
    assert p.PLAN.resolve()==(p.REPO/d['original_plan']).resolve() and p.digest(p.PLAN)==d['original_plan_sha256']
    return d

def spec(profile):
    assert profile in ('batch','builder')
    return ('LibraryPairBenchmarks','TaLib.Functions.Log10',['Ooples','Competitor'],'Bars',1000) if profile=='batch' else ('SharedPointwiseBenchmarks','Log10',['BuilderFull','NativeSingle','NativeParallel'],'Count',10)

def filters(profile):
    fixture,pair,methods,param,_=spec(profile)
    return [s for m in methods for s in (f'*{fixture}.{m}*"{pair}"*{param}: 10000)*',f'*{fixture}.{m}*{param}: 10000,*"{pair}"*')]

def validate_cases(profile,reports,log):
    fixture,pair,methods,_,warmups=spec(profile)
    cases=[c for report in reports for c in report['Benchmarks']]
    keys=[case_key(c) for c in cases]
    assert len(keys)==len(set(keys))==len(methods) and set(keys)=={(pair,10000,m) for m in methods}
    assert all(fixture+'.' in c['FullName'] for c in cases)
    check_log(log,len(methods),warmups=warmups)
    return [dict(pair=pair,bars=10000,method=c['Method'],**sample(c)) for c in cases]

def builds():
    found=[]
    for proc in psutil.process_iter(['name','cmdline']):
        try:
            if (proc.info['name'] or '').lower() in ('dotnet','dotnet.exe') and any(x in ('build','test','vstest','restore','pack','publish') for x in (proc.info['cmdline'] or [])): found.append(proc.pid)
        except (psutil.AccessDenied,psutil.NoSuchProcess): pass
    return found

def quiet(folder):
    samples=[];limit=time.monotonic()+15*60
    while len(samples)<12:
        assert time.monotonic()<limit,'No quiet window; gates unchanged'
        active=builds();value=psutil.cpu_percent(interval=5);active+=builds()
        samples=[] if active or value>=15 else samples+[value]
        p.write(folder/'quiet-progress.json',dict(samples=samples,build_pids=active))
    return samples

def checked_process(args,cwd,env,log,timeout=1800):
    assert not controls(env),'BDN must use default runtime without logging overrides'
    with log.open('wb') as stream:
        child=subprocess.Popen(args,cwd=cwd,env=env,stdout=stream,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0)
        deadline=time.monotonic()+timeout
        try:
            while child.poll() is None:
                assert not builds(),'Concurrent build/test invalidated diagnostic; do not retry silently'
                assert time.monotonic()<deadline,'Owned diagnostic process timed out'
                time.sleep(1)
            assert child.returncode==0,'Diagnostic process failed; inspect log'
        finally:
            if child.poll() is None: child.terminate();child.wait(timeout=15)

def summarize_speedscope(path):
    data=p.read(path);frames=[f['name'] for f in data['shared']['frames']];inc=collections.Counter();exc=collections.Counter();total=0
    for profile in data['profiles']:
        assert profile['type']=='evented' and profile['unit']=='milliseconds'
        stack=[];previous=profile['startValue']
        for event in profile['events']:
            elapsed=event['at']-previous
            relevant=any('OoplesFinance.StockIndicators!' in frames[i] or 'OoplesFinance.StockIndicators.dll!' in frames[i] or 'OoplesFinance.StockIndicators.Indicators.' in frames[i] or 'TranscendentalComparison.Owned' in frames[i] or 'SharedPointwiseBenchmarks.BuilderFull' in frames[i] for i in stack)
            if elapsed>0 and relevant:
                total+=elapsed
                for i in set(stack): inc[frames[i]]+=elapsed
                leaf=next((i for i in reversed(stack) if frames[i]!='CPU_TIME'),None)
                if leaf is not None: exc[frames[leaf]]+=elapsed
            if event['type']=='O':stack.append(event['frame'])
            else:assert stack.pop()==event['frame']
            previous=event['at']
        assert not stack
    assert total>0,'No relevant stack samples; retain raw trace without causal claim'
    return dict(kind='Sampled wall/thread-time attribution, NOT CPU share or qualification',attributed_milliseconds=total,inclusive=dict(inc.most_common()),exclusive=dict(exc.most_common()))

def trace_replay(d,profile,runtime,folder,sha):
    folder.mkdir();samples=quiet(folder);env=dict(os.environ);assert not controls(env)
    env.update(DOTNET_JitDisasm='*ComputePointwiseRegion* *FillPointwiseKernel*',DOTNET_JitDisasmAssemblies='OoplesFinance.StockIndicators',DOTNET_JitStdOutFile=str(folder/'jit.log'))
    pwsh_dll=Path(shutil.which('pwsh')).with_name('pwsh.dll');assert pwsh_dll.is_file()
    args=['dotnet','exec','--runtimeconfig',str(runtime/'OoplesFinance.StockIndicators.CompetitorBenchmarks.runtimeconfig.json'),str(pwsh_dll),'-NoLogo','-NoProfile','-File',str(p.REPO/'scripts/profile-log10-worker.ps1'),'-Runtime',str(runtime),'-Profile',profile,'-Output',str(folder),'-ProductionSha',sha]
    children=[]
    def wait_for(test,limit):
        deadline=time.monotonic()+limit
        while not test():
            assert time.monotonic()<deadline and all(c.poll() in (None,0) for c in children),'Owned trace process failed or timed out'
            assert not builds(),'Concurrent build/test invalidated trace'
            time.sleep(.1)
    try:
        with (folder/'worker.log').open('wb') as wl,(folder/'trace.log').open('wb') as tl:
            worker=subprocess.Popen(args,cwd=runtime,env=env,stdout=wl,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0);children.append(worker)
            wait_for(lambda:(folder/'ready.json').exists(),120)
            ready=p.read(folder/'ready.json');assert ready['pid']==worker.pid and ready['production_sha256']==sha and ready['benchmark_sha256']==p.digest(runtime/'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll')
            tracer=subprocess.Popen(['dotnet-trace','collect','--process-id',str(worker.pid),'--profile',d['trace_profile'],'--format','Speedscope','--duration','00:01:00','--buffersize','64','--output',str(folder/'profile.nettrace')],stdout=tl,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NO_WINDOW if os.name=='nt' else 0);children.append(tracer)
            wait_for(lambda:'trace duration' in (folder/'trace.log').read_text(errors='replace').lower(),30)
            (folder/'go').write_text('attached')
            wait_for(lambda:all(c.poll() is not None for c in children),120)
            assert all(c.returncode==0 for c in children)
        completed=p.read(folder/'completed.json');assert completed['completed'] and completed['invocations']==10000 and len(completed['chunks'])==10 and completed['production_sha256']==sha
        result=summarize_speedscope(folder/'profile.speedscope.json')
        result.update(ready=ready,quiet_samples=samples,instrumentation_only=controls(env),replay=completed,files={f.name:p.digest(f) for f in folder.iterdir() if f.is_file()})
        p.write(folder/'summary.json',result);return result
    finally:
        for child in children:
            if child.poll() is None:child.terminate();child.wait(timeout=15)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--profile',choices=['batch','builder'],required=True);parser.add_argument('--inputs',type=Path,required=True);parser.add_argument('--out',type=Path,required=True);a=parser.parse_args()
    d=validate_plan();assert not controls(os.environ);assert subprocess.check_output(['dotnet','--version'],text=True).strip()==d['sdk']
    version=subprocess.check_output(['dotnet-trace','--version'],text=True).strip();assert version.startswith(d['trace_tool_version']+'+') or version==d['trace_tool_version']
    inputs=a.inputs.resolve();out=a.out.resolve();assert not out.exists();out.mkdir()
    assert p.digest(inputs/'provenance.json')==d['input_provenance_sha256'];proof=p.verify_inputs(inputs)
    assert proof['sources']==d['sources'] and proof['production_sha256']==d['production_sha256'] and str(proof['run_id'])==str(d['input_artifact']['run_id']) and proof['workflow_head']==d['input_artifact']['workflow_head']
    runtimes={};manifests={}
    for arm,label in [('old','old'),('new','successor')]:
        runtime=out.parent/(out.name+'-runtime-'+arm);assert not runtime.exists();shutil.copytree(inputs/'common',runtime);shutil.copy2(inputs/'production'/(label+'.dll'),runtime/p.DLL)
        runtimes[arm]=runtime;manifests[arm]=p.manifest(runtime);assert manifests[arm][p.DLL]==d['production_sha256'][label]
    assert manifests['old'].keys()==manifests['new'].keys() and [k for k in manifests['old'] if manifests['old'][k]!=manifests['new'][k]]==[p.DLL]
    origin=dict(diagnostic_only=True,tooling_head=os.getenv('GITHUB_SHA'),tooling_files={str(x.relative_to(p.REPO)):p.digest(x) for x in [Path(__file__),p.REPO/'scripts/profile-log10-worker.ps1',p.REPO/'.github/workflows/cpu-kernel-performance.yml']},raw_root=str(out),plan_sha256=p.digest(PLAN),input_provenance_sha256=p.digest(inputs/'provenance.json'),host=dict(machine=platform.node(),platform=platform.platform(),logical_cpus=psutil.cpu_count(),runner_image=os.getenv('ImageVersion'),run_id=os.getenv('GITHUB_RUN_ID'),run_attempt=os.getenv('GITHUB_RUN_ATTEMPT'),instance_nonce=uuid.uuid4().hex),sources=d['sources'],binary_manifests=manifests,trace_tool_version=version,limitations=d['limitations'])
    p.write(out/'provenance.json',origin);rounds=[];fixture,pair,methods,_,warmups=spec(a.profile)
    env=dict(os.environ)
    for key in ('COMPARISON_PAIR','COMPARISON_SHARD','COMPARISON_SHARDS','COMPARISON_FAMILY','SHARED_POINTWISE'):env.pop(key,None)
    for index,arm in enumerate(d['order']):
        folder=out/f'round{index}-{arm}';folder.mkdir();assert shutil.disk_usage(out).free>2*1024**3
        assert all(p.manifest(runtimes[k])==manifests[k] for k in runtimes)
        samples=quiet(folder);runtime=runtimes[arm]
        args=['dotnet',str(runtime/'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'),'--filter',*filters(a.profile),'--warmupCount',str(warmups),'--iterationCount','10','--exporters','json','--artifacts',str(folder/'bdn')]
        checked_process(args,runtime,env,folder/'bdn.log')
        reports=list((folder/'bdn').rglob('*-report-full.json'));assert len(reports)==1
        rows=validate_cases(a.profile,[p.read(x) for x in reports],(folder/'bdn.log').read_text(errors='replace'))
        replay=trace_replay(d,a.profile,runtime,folder/'trace',manifests[arm][p.DLL])
        assert all(p.manifest(runtimes[k])==manifests[k] for k in runtimes)
        record=dict(round=index,arm=arm,profile=a.profile,quiet_samples=samples,rows=rows,report=str(reports[0]),report_sha256=p.digest(reports[0]),log_sha256=p.digest(folder/'bdn.log'),trace_summary_sha256=p.digest(folder/'trace/summary.json'))
        rounds.append(record);p.write(out/'status.json',dict(status='round-complete',rounds=rounds));print(a.profile,index,arm,'diagnostic round complete',flush=True)
    assert len(rounds)==4
    p.write(out/'summary.json',dict(diagnostic_only=True,qualification_eligible=False,profile=a.profile,rounds=rounds,origin=origin))
    p.write(out/'files.json',{str(x.relative_to(out)):p.digest(x) for x in out.rglob('*') if x.is_file() and x.name!='files.json'})
    print('Complete diagnostic; no qualification/source/report import permitted',flush=True)
if __name__=='__main__':main()
