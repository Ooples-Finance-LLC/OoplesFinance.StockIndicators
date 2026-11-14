"""Recover the next bounded publication batch without accessing the repairing drive."""
import collections
import datetime
import gzip
import hashlib
import importlib.util
import json
import os
from pathlib import Path
import platform
import psutil
import shutil
import subprocess
import time
import xml.etree.ElementTree as ET

repo=Path('C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-periodic-certified-20261021')
root=Path('C:/Users/cheat/StockIndicators-Recovery-20261021')
out=root/'periodic-resume-cases';out.mkdir(exist_ok=True)
temporary=root/'temp';temporary.mkdir(exist_ok=True)
os.environ['TEMP']=os.environ['TMP']=str(temporary)
head=subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
state=dict(input_head=head,source='C-drive recovery; committed source and compressed evidence retained',
    checks={},drive='C:',status='starting')
def save(status,step):
    state.update(status=status,step=step,updated_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
    path=out/'status.tmp';path.write_text(json.dumps(state,indent=2),encoding='utf-8');path.replace(out/'status.json')
    print(status,step,flush=True)
def capacity():
    while True:
        builds=[p.pid for p in psutil.process_iter(['name','cmdline']) if
            p.info['name'].lower()=='dotnet.exe' and 'build' in (p.info['cmdline'] or [])]
        memory_ready=psutil.virtual_memory().available>24*1024**3 and shutil.disk_usage(root).free>4*1024**3
        if memory_ready and (len(builds)<2 or len(builds)==2 and psutil.cpu_percent(interval=5)<25):return
        save('waiting-build-capacity','build');time.sleep(30)
def run(label,args):
    if label.startswith('build-'):capacity()
    assert subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()==head
    save('running',label)
    with (out/(label+'.log')).open('wb') as stream:
        child=subprocess.Popen(args,cwd=repo,env=os.environ.copy(),stdout=stream,stderr=subprocess.STDOUT)
        limit=1200 if label.startswith('unit-case-') else 900 if label=='parity' else 1800
        try: code=child.wait(timeout=limit)
        except subprocess.TimeoutExpired:
            process=psutil.Process(child.pid)
            for owned in process.children(recursive=True):
                if owned.name().lower()=='testhost.exe': owned.terminate()
            try: child.wait(timeout=20)
            except subprocess.TimeoutExpired:
                for owned in process.children(recursive=True): owned.terminate()
                process.terminate();child.wait(timeout=10)
            raise TimeoutError(label+' exceeded bounded limit; incomplete, not qualified')
        if code: raise subprocess.CalledProcessError(code,args)
    state['checks'][label]='passed'
def verify(label,expected):
    path=out/label/(label+'.trx');raw=path.read_bytes();tree=ET.fromstring(raw)
    assert tree.find('.//{*}ResultSummary').attrib['outcome']=='Completed'
    definitions={t.attrib['id']:t.find('.//{*}TestMethod').attrib['className'].split(',')[0].rsplit('.',1)[-1]
        for t in tree.findall('.//{*}UnitTest')}
    results=tree.findall('.//{*}UnitTestResult')
    assert results and all(t.attrib['outcome']=='Passed' for t in results)
    counts=dict(collections.Counter(definitions[t.attrib['testId']] for t in results))
    assert counts.keys()==expected.keys(),(counts,expected)
    assert all(counts[k]>0 and (v is None or counts[k]==v) for k,v in expected.items()),(counts,expected)
    state[label]=dict(passed=len(results),class_counts=counts,trx=str(path),sha256=hashlib.sha256(raw).hexdigest())
def test(label,dll,classes):
    run(label,['dotnet','vstest',str(repo/dll),'/TestCaseFilter:'+'|'.join(('(FullyQualifiedName~.'+x+'.&DisplayName~Skender.GetSmaAnalysis)' if x=='SharedRollingStatisticsBenchmarkTests' else 'FullyQualifiedName~.'+x+'.') for x in classes),
        '/Logger:trx;LogFileName='+label+'.trx','/ResultsDirectory:'+str(out/label)])
    verify(label,classes)
try:
    prior=json.loads((root/'periodic-certified/status.json').read_text())
    assert head==prior['input_head']=='14dab541523d44999c33b131b4c14fa5e5cf7f48'
    assert all(prior['checks'].get(k)=='passed' for k in ['build-net461','build-net10','build-unit','build-comparator'])
    state['checks'].update(prior['checks'])
    state['reused_build_source']=head
    dll=repo/'tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll'
    cases=subprocess.check_output(['pwsh','-NoProfile','-File',str(root/'list-periodic-cases.ps1')],text=True).splitlines()
    assert len(cases)==len(set(cases))==8
    listed=[x.strip() for x in (root/'periodic-certified/unit-discovery.log').read_text().splitlines() if x.strip().startswith('OoplesFinance.')]
    expected=set()
    for name in listed:
        if name.endswith('.EveryRouteMatchesIndependentCenteredMoments'):
            expected.update(name+'(c: '+case+', route: "'+route+'")' for case in cases for route in ['batch','fast','native','streaming'])
        elif name.endswith(('.SelectedSourcePreservesFormula','.EveryOutputRejectsInjectedFaults','.NumericalClassesAreEnrolled')):
            expected.update(name+'(c: '+case+')' for case in cases)
        else: expected.add(name)
    assert len(expected)==86
    rawpath=root/'periodic-certified/unit/unit.trx'
    raw=rawpath.read_bytes();tree=ET.fromstring(raw)
    assert tree.find('.//{*}ResultSummary').get('outcome')=='Failed'
    rows=tree.findall('.//{*}UnitTestResult')
    assert len(rows)==69 and all(x.get('outcome')=='Passed' for x in rows)
    passed={x.get('testName') for x in rows}
    assert len(passed)==69 and passed<=expected
    state['unit_receipts']=[dict(trx=str(rawpath),sha256=hashlib.sha256(raw).hexdigest(),passed=69,run_outcome='Failed (test host aborted at time limit)',note='Only individually completed Passed cases reused; the original run remains aborted.')]
    missing=sorted(expected-passed);assert len(missing)==17
    state['remaining_cases']=missing
    for index,name in enumerate(missing):
        label='unit-case-'+str(index+1).zfill(2)
        case=name.split('(c: ')[1].split(', route: ')[0]
        route=name.split('route: "')[1].split('"')[0]
        run(label,['pwsh','-NoProfile','-File',str(root/'run-periodic-route.ps1'),'-Configuration',case,'-Route',route])
        path=out/(label+'.log');raw=path.read_bytes();receipt=json.loads(raw.decode('utf-8-sig'))
        assert receipt['passed'] is True and receipt['configuration']==case and receipt['route']==route
        assert receipt['method']=='EveryRouteMatchesIndependentCenteredMoments'
        assert receipt['assembly_sha256']==hashlib.sha256(dll.read_bytes()).hexdigest()
        assert name not in passed;passed.add(name)
        state['unit_receipts'].append(dict(file=str(path),sha256=hashlib.sha256(raw).hexdigest(),passed=1,kind='direct invocation of unchanged synchronous xUnit method including all assertions',test_name=name))
        save('running',label+'-passed')
    assert passed==expected
    state['unit']={'passed':len(passed),'expected_test_names':sorted(expected),'note':'Exact union of69completed cases in preserved aborted run and17 direct invocations of unchanged synchronous xUnit route methods with all assertions. Dynamic theory display names are not available to vstest filtering. Original TRX remains Failed; direct invocation receipts are JSON, not synthetic TRX.'}
    state['checks']['unit']='passed-union'
    test('parity','benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll',{'RemainingPeriodicChannelBenchmarkTests':4,'RemainingBenchmarkDiscoveryTests':None})
    spec=importlib.util.spec_from_file_location('gpu',repo/'scripts/verify-competitor-local-gpu.py')
    gpu=importlib.util.module_from_spec(spec);spec.loader.exec_module(gpu)
    before=gpu.source_fingerprint(repo);methods=json.loads(gpu.METHODS.read_text())
    settings=out/'serial.runsettings'
    settings.write_text('<RunSettings><RunConfiguration><MaxCpuCount>1</MaxCpuCount></RunConfiguration><xUnit><ParallelizeTestCollections>false</ParallelizeTestCollections><MaxParallelThreads>1</MaxParallelThreads></xUnit></RunSettings>',encoding='utf-8')
    dll=repo/'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll'
    run('gpu',['dotnet','vstest',str(dll),'/Settings:'+str(settings),'/TestCaseFilter:'+'|'.join('FullyQualifiedName='+m for m in methods),
        '/Logger:trx;LogFileName=gpu.trx','/ResultsDirectory:'+str(out/'gpu')])
    raw=(out/'gpu/gpu.trx').read_bytes();count=gpu.contracts.verify_trx(out/'gpu/gpu.trx',methods)
    assert count==100 and gpu.source_fingerprint(repo)==before
    receipt=dict(source_sha256=before,methods=methods,passed_cases=count,trx_sha256=hashlib.sha256(raw).hexdigest(),
        host=platform.platform(),device='Radeon RX 5500 XT; RequiredGPU fixtures',
        dotnet_info=subprocess.check_output(['dotnet','--info']).decode(),test_assembly_sha256=hashlib.sha256(dll.read_bytes()).hexdigest(),
        note='Actual RequiredGPU execution using already-built source-matched binaries; correctness, not performance qualification.')
    gpu.verify_evidence(receipt,raw,methods,before)
    (gpu.EVIDENCE/'gpu.trx.gz').write_bytes(gzip.compress(raw,mtime=0))
    (gpu.EVIDENCE/'receipt.json').write_text(json.dumps(receipt,indent=2)+'\n',encoding='utf-8')
    state['gpu_passed']=count
    run('discover-contracts',['python','scripts/run-competitor-contracts.py','discover',str(dll),str(out/'all-methods.json')])
    run('gpu-source-verification',['python','scripts/verify-competitor-local-gpu.py','verify',str(out/'all-methods.json'),str(out/'hosted-methods.json')])
    run('inventory',['python','benchmarks/build_remaining_indicator_inventory.py'])
    run('inventory-check',['python','benchmarks/build_remaining_indicator_inventory.py','--check'])
    run('inventory-tests',['python','-m','unittest','discover','-s','benchmarks','-p','test_remaining_indicator_inventory.py'])
    paths=['src','tests','benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks','benchmarks/OoplesFinance.StockIndicators.CompetitorTests']
    state['production_dll_sha256']={str(repo/p/'bin/Release/net10.0/OoplesFinance.StockIndicators.dll'):
        hashlib.sha256((repo/p/'bin/Release/net10.0/OoplesFinance.StockIndicators.dll').read_bytes()).hexdigest() for p in paths}
    assert len(set(state['production_dll_sha256'].values()))==1
    run('diff-check',['git','diff','--check'])
    save('checks-passed-review-and-publication-pending','complete')
except Exception as error:
    state['error']=repr(error);save('failed-needs-inspection',state.get('step','startup'));raise
