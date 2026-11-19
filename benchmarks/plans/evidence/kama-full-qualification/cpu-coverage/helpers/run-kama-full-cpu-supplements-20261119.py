"""Run four reviewed missing CPU groups against pinned retained binaries only.

Original CI manifests, CI counts and GPU receipts are read-only. Every actual
local test name/outcome is checked before combined coverage totals are reported.
No build, restore, GPU execution, timing, retry or source modification occurs.
"""
import argparse,hashlib,json,os,re,shutil,subprocess
from pathlib import Path
from xml.etree import ElementTree as ET

def digest(path):
    with Path(path).open('rb') as stream:return hashlib.file_digest(stream,'sha256').hexdigest()
def read(path):return json.loads(Path(path).read_bytes())
def write(path,value):Path(path).write_bytes((json.dumps(value,indent=2)+'\n').encode())
def runtime_inventory(packet,record_path=None):
    overrides={key:value for key,value in os.environ.items() if key.upper().startswith(('DOTNET_ROLL_FORWARD','COMPLUS_ROLLFORWARD'))}
    assert not overrides,'Inherited roll-forward overrides are not permitted for supplemental checks'
    text=subprocess.check_output([packet['launcher'][0],'--list-runtimes'],text=True)
    versions=sorted({tuple(map(int,value.split('.'))) for value in re.findall(r'^Microsoft\.NETCore\.App (10\.0\.\d+) \[',text,re.M)})
    record=dict(installed_runtimes=text,compatible_net10_versions=['.'.join(map(str,v)) for v in versions],
        selected_latest_patch='.'.join(map(str,versions[-1])) if versions else None)
    if record_path is not None:write(record_path,record)
    assert versions and versions[-1]==(10,0,15),'Highest compatible net10 patch must be exactly10.0.15'
    return record

def run_group(command,cwd,env,folder):
    assert os.name=='nt','This pinned supplemental launcher is Windows-only'
    with (folder/'run.log').open('xb') as log:
        process=subprocess.Popen(command,cwd=cwd,env=env,stdout=log,stderr=subprocess.STDOUT,creationflags=subprocess.CREATE_NEW_PROCESS_GROUP)
        try:return process.wait(timeout=300)
        except subprocess.TimeoutExpired:
            termination=None
            try:
                if process.poll() is None:
                    kill=subprocess.run(['taskkill','/PID',str(process.pid),'/T','/F'],stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=15)
                    termination=dict(pid=process.pid,exit_code=kill.returncode,log=kill.stdout.decode(errors='replace'))
                process.wait(timeout=15)
            finally:
                write(folder/'failure-receipt.json',dict(reason='300-second supplemental group timeout',spawned_pid=process.pid,
                    termination=termination,returncode=process.poll(),automatic_retry=False,other_processes_targeted=False))
            raise RuntimeError('First group timeout preserved; only its spawned process tree was targeted')
def checked_trx(path,group):
    results=ET.parse(path).findall('.//{*}UnitTestResult')
    assert results and all(row.attrib['outcome']=='Passed' for row in results),'No failure or skip accepted'
    counts={name:0 for name in group['classes']};names=[]
    for row in results:
        name=row.attrib['testName'];matches=[key for key in counts if group['namespace']+key+'.' in name]
        assert len(matches)==1,'Unexpected or ambiguous test outside exact filter'
        counts[matches[0]]+=1;names.append(name)
    assert counts==group['expected_counts'] and len(results)==group['expected_cases'],'Actual inventory differs from reviewed expectation'
    return dict(passed=len(results),classes=counts,trx_sha256=digest(path),test_names=names)

def check_role(role):
    assert digest(role['manifest_path'])==role['manifest_sha256']
    assert digest(role['cpu_root_path'])==role['cpu_root_sha256'] and digest(role['gpu_root_path'])==role['gpu_root_sha256']
    manifest=read(role['manifest_path']);cpu=read(role['cpu_root_path']);gpu=read(role['gpu_root_path'])
    assert manifest['source']==cpu['source']==gpu['source']==role['source']
    assert manifest['source_sha256']==cpu['source_fingerprint']==role['source_fingerprint']
    assert manifest['checks']==role['original_ci_checks'] and manifest['sdk']==role['ci_sdk']=='10.0.403'
    assert cpu['status']=='root-independent-cpu-authentication-passed' and cpu['manifest_sha256']==role['manifest_sha256']
    assert gpu['status']=='root-independent-actual-gpu-authentication-passed' and gpu['passed_cases']==100 and gpu['methods']==12
    repo=Path(role['runtime_repo']).resolve()
    for name,pin in manifest['runtime_files'].items():
        path=(repo/name).resolve();assert path.is_relative_to(repo) and path.is_file() and digest(path)==pin
    assert len(manifest['runtime_files'])==444
    scoped=['src','tests','benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks','benchmarks/OoplesFinance.StockIndicators.CompetitorTests','scripts/competitor-gpu-methods.json']
    assert not subprocess.check_output(['git','diff',role['source'],'HEAD','--',*scoped],cwd=repo)
    assert not subprocess.check_output(['git','status','--porcelain','--',*scoped],cwd=repo)
    return manifest

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--packet',required=True);parser.add_argument('--packet-sha256',required=True)
    parser.add_argument('--out',required=True);parser.add_argument('--execute-root-reviewed-supplements',action='store_true')
    args=parser.parse_args();packet_path=Path(args.packet).resolve();out=Path(args.out).resolve()
    assert args.execute_root_reviewed_supplements,'HELD: explicit root review before supplemental tests'
    assert re.fullmatch('[a-f0-9]{64}',args.packet_sha256) and digest(packet_path)==args.packet_sha256
    packet=read(packet_path);assert packet['schema']==1 and len(packet['groups'])==4
    intended={('candidate','unit'):134,('candidate','parity'):86,('baseline','unit'):45,('baseline','parity'):39}
    assert {(g['role'],g['suite']):g['expected_cases'] for g in packet['groups']}==intended
    assert all(Path(p).is_file() and digest(p)==pin for p,pin in packet['launcher_pins'].items())
    assert subprocess.check_output([*packet['launcher'],'--version'],text=True).strip()=='10.0.404'
    initial_runtime=runtime_inventory(packet)
    manifests={role:check_role(spec) for role,spec in packet['roles'].items()}
    assert out.is_relative_to(Path('C:/Users/cheat/source/stock-indicators-operations').resolve()) and not out.exists()
    assert shutil.disk_usage(out.parent).free>128*1024**2
    out.mkdir(parents=True);(out/'packet.json').write_bytes(packet_path.read_bytes())
    write(out/'started.json',dict(packet_sha256=args.packet_sha256,runtime_inventory=initial_runtime,source_builds=0,gpu_executions=0,timings=0,automatic_retry=False))
    settings=out/'serial.runsettings'
    settings.write_bytes(b'<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration><xUnit><ParallelizeTestCollections>false</ParallelizeTestCollections><MaxParallelThreads>1</MaxParallelThreads></xUnit></RunSettings>')
    results=[]
    for group in packet['groups']:
        role=packet['roles'][group['role']];manifest=manifests[group['role']]
        assert set(group['classes']).isdisjoint(manifest['classes'][group['suite']]),'Never repeat original CI classes'
        expected_filter='|'.join('FullyQualifiedName~'+group['namespace']+name+'.' for name in group['classes'])
        assert group['filter']==expected_filter and len(group['classes'])==len(set(group['classes']))
        dll=Path(role['runtime_repo'])/group['assembly_path']
        assert digest(dll)==group['assembly_sha256']==manifest['runtime_files'][group['assembly_path']]
        runtime=read(dll.with_suffix('.runtimeconfig.json'))['runtimeOptions']
        assert runtime['tfm']=='net10.0' and runtime['framework']['name']=='Microsoft.NETCore.App'
        assert runtime['framework']['version'] in ('10.0.0','10.0.15') and runtime.get('rollForward') in (None,'LatestPatch') and runtime.get('applyPatches') is not False
        folder=out/(group['role']+'-'+group['suite']);folder.mkdir()
        command=[*packet['launcher'],'vstest',str(dll),'/Settings:'+str(settings),'/TestCaseFilter:'+group['filter'],
            '/Logger:trx;LogFileName=checks.trx','/ResultsDirectory:'+str(folder)]
        write(folder/'invocation.json',dict(command=command,source=role['source'],assembly_sha256=group['assembly_sha256'],
            ci_compiler_sdk=role['ci_sdk'],local_launcher_sdk='10.0.404',local_execution=True,build=False,gpu=False))
        before=runtime_inventory(packet,folder/'runtime-before.json')
        env=dict(os.environ,AIDOTNET_DISABLE_GPU='1',DOTNET_ROOT='C:/Program Files/dotnet',DOTNET_MULTILEVEL_LOOKUP='0',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_ROLL_FORWARD='LatestPatch')
        code=run_group(command,role['runtime_repo'],env,folder)
        after=runtime_inventory(packet,folder/'runtime-after.json')
        write(folder/'runtime-selection.json',dict(before=before,after=after,test_launch_roll_forward='LatestPatch'))
        if code!=0:write(folder/'failure-receipt.json',dict(reason='supplemental group returned failure',returncode=code,automatic_retry=False))
        assert code==0,'First supplemental failure preserved; no retry'
        checks=checked_trx(folder/'checks.trx',group)
        original=Path(role['manifest_path']).parent/'evidence'/(group['suite']+'.trx')
        original_names={row.attrib['testName'] for row in ET.parse(original).findall('.//{*}UnitTestResult')}
        assert original_names.isdisjoint(checks['test_names'])
        result=dict(role=group['role'],suite=group['suite'],source=role['source'],source_fingerprint=role['source_fingerprint'],
            assembly_sha256=group['assembly_sha256'],local_execution=True,original_ci_receipt_unchanged=True,
            runtime_inventory_before=before,runtime_inventory_after=after,test_launch_roll_forward='LatestPatch',**checks)
        write(folder/'receipt.json',result);results.append(result)
        print(json.dumps(dict(role=group['role'],suite=group['suite'],passed=checks['passed'])),flush=True)
    totals={role:{suite:manifests[role]['checks'][suite]['passed']+next(r['passed'] for r in results if r['role']==role and r['suite']==suite)
        for suite in ('unit','parity')} for role in packet['roles']}
    assert totals==packet['expected_totals']
    for role,spec in packet['roles'].items():check_role(spec)
    final_runtime=runtime_inventory(packet)
    assert all(digest(p)==pin for p,pin in packet['launcher_pins'].items())
    write(out/'supplement-summary.json',dict(status='supplemental-cpu-passed-root-authentication-pending',packet_sha256=args.packet_sha256,
        original_ci_checks_reused={role:spec['original_ci_checks'] for role,spec in packet['roles'].items()},
        supplemental_local_checks=results,combined_coverage_totals=totals,unchanged_gpu100_reused_per_source=True,
        initial_runtime_inventory=initial_runtime,final_runtime_inventory=final_runtime,
        source_builds=0,benchmark_builds=0,gpu_executions=0,timings=0,qualification=False,automatic_retry=False))
    print(json.dumps(totals))

if __name__=='__main__':main()
