"""Authenticate original sessions; execute only omitted EMA19 and unrun parity39."""
import argparse
import importlib.util
import json
import os
import subprocess
from pathlib import Path
from xml.etree import ElementTree as ET

OPS=Path('C:/Users/cheat/source/stock-indicators-operations')
ORIGINAL=OPS/'kama-full-qualification-cpu-supplement-20261119'
HELPER=OPS/'run-kama-full-cpu-supplements-20261119.py'
HELPER_PIN='654a17f1928bd7f049ad42d0b773cd16a4f6f9ad32b5cfb9db7697417019463f'
PACKET_PIN='73fcc53caaf671e06d3f3e6013b769febce61d2b4a80fdc290e82b716595d2bc'
OLD='4b4e8a385ed1c210998d26de6460e23f5842ffe9'
REPO=Path('C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-kama-full-qualification-20261119')

def authenticate(h):
    assert h.digest(HELPER)==HELPER_PIN and h.digest(ORIGINAL/'packet.json')==PACKET_PIN
    packet=h.read(ORIGINAL/'packet.json')
    assert h.read(ORIGINAL/'started.json')['packet_sha256']==PACKET_PIN
    assert not (ORIGINAL/'supplement-summary.json').exists() and not (ORIGINAL/'baseline-parity').exists()
    assert all(Path(p).is_file() and h.digest(p)==pin for p,pin in packet['launcher_pins'].items())
    assert subprocess.check_output([*packet['launcher'],'--version'],text=True).strip()=='10.0.404'
    h.runtime_inventory(packet)
    manifests={role:h.check_role(spec) for role,spec in packet['roles'].items()}
    retained=[]
    for group in packet['groups'][:2]:
        folder=ORIGINAL/(group['role']+'-'+group['suite'])
        checks=h.checked_trx(folder/'checks.trx',group)
        receipt=h.read(folder/'receipt.json')
        assert receipt['trx_sha256']==checks['trx_sha256'] and receipt['passed']==checks['passed']
        assert receipt['source']==packet['roles']['candidate']['source']
        assert receipt['assembly_sha256']==group['assembly_sha256']
        retained.append(dict(role=group['role'],suite=group['suite'],receipt_sha256=h.digest(folder/'receipt.json'),**checks))
    group=dict(packet['groups'][2],classes=['SeededAdaptiveSharedExecutionTests','TillsonSharedExecutionTests'],
        expected_counts={'SeededAdaptiveSharedExecutionTests':14,'TillsonSharedExecutionTests':12},expected_cases=26)
    checks=h.checked_trx(ORIGINAL/'baseline-unit/checks.trx',group)
    invocation=h.read(ORIGINAL/'baseline-unit/invocation.json')
    assert invocation['source']==OLD and invocation['assembly_sha256']==group['assembly_sha256']
    retained.append(dict(role='baseline',suite='unit',original_inventory_mismatch_preserved=True,**checks))
    namespace='OoplesFinance.StockIndicators.Tests.Unit.ValidationTests.'
    raw=subprocess.check_output(['git','show',OLD+':tests/ValidationTests/EmaRecurrenceTests.cs'],cwd=REPO)
    assert ('namespace '+namespace.rstrip('.')+';').encode() in raw
    candidate=packet['roles']['candidate']
    ci_trx=Path(candidate['manifest_path']).parent/'evidence/unit.trx'
    assert h.digest(ci_trx)==candidate['original_ci_checks']['unit']['sha256']
    tsi_namespace='OoplesFinance.StockIndicators.Tests.Unit.ModelsTests.'
    tsi_raw=subprocess.check_output(['git','show',OLD+':tests/ModelsTests/TsiWordEmaRecurrenceTests.cs'],cwd=REPO)
    assert ('namespace '+tsi_namespace.rstrip('.')+';').encode() in tsi_raw
    class_counts={'ValidationTests.EmaRecurrenceTests':15,'ModelsTests.TsiWordEmaRecurrenceTests':4}
    unit_prefix='OoplesFinance.StockIndicators.Tests.Unit.'
    ci_results=ET.parse(ci_trx).findall('.//{*}UnitTestResult')
    names=[]
    for key,count in class_counts.items():
        selected=[r.attrib['testName'] for r in ci_results if unit_prefix+key+'.' in r.attrib['testName']]
        assert len(selected)==len(set(selected))==count
        names.extend(selected)
    assert len(names)==len(set(names))==19
    ema=dict(packet['groups'][2],classes=list(class_counts),namespace=unit_prefix,
        filter='|'.join('FullyQualifiedName~'+unit_prefix+key+'.' for key in class_counts),expected_counts=class_counts,expected_cases=19)
    return packet,manifests,retained,[ema,packet['groups'][3]],dict(namespace=namespace,source=OLD,
        source_blob_sha256=__import__('hashlib').sha256(raw).hexdigest(),tsi_source_blob_sha256=__import__('hashlib').sha256(tsi_raw).hexdigest(),
        original_ci_19_is_15_exact_ema_plus_4_tsi_word=True,candidate_ci_trx_sha256=h.digest(ci_trx),candidate_ci_names=names)

def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--execute-root-reviewed-filter-repair',action='store_true')
    parser.add_argument('--out',required=True)
    args=parser.parse_args()
    spec=importlib.util.spec_from_file_location('kama_original_supplement',HELPER)
    h=importlib.util.module_from_spec(spec);spec.loader.exec_module(h)
    packet,manifests,retained,groups,namespace=authenticate(h)
    if not args.execute_root_reviewed_filter_repair:
        print(json.dumps(dict(status='authenticated-no-test-execution',omitted_ema_cases=19,previously_unrun_parity_cases=39)))
        return
    out=Path(args.out).resolve()
    assert out.parent==OPS and not out.exists()
    out.mkdir()
    h.write(out/'started.json',dict(original_packet_sha256=PACKET_PIN,original_helper_sha256=HELPER_PIN,
        repair_helper_sha256=h.digest(__file__),retained_actual_sessions=retained,namespace_proof=namespace,
        automatic_retry=False,passed_groups_repeated=False,source_builds=0,gpu_executions=0,timings=0))
    settings=out/'serial.runsettings';settings.write_bytes((ORIGINAL/'serial.runsettings').read_bytes())
    results=[];role=packet['roles']['baseline']
    for group in groups:
        dll=Path(role['runtime_repo'])/group['assembly_path']
        assert h.digest(dll)==group['assembly_sha256']==manifests['baseline']['runtime_files'][group['assembly_path']]
        runtime=h.read(dll.with_suffix('.runtimeconfig.json'))['runtimeOptions']
        assert runtime['tfm']=='net10.0' and runtime['framework']['name']=='Microsoft.NETCore.App'
        assert runtime['framework']['version'] in ('10.0.0','10.0.15') and runtime.get('rollForward') in (None,'LatestPatch') and runtime.get('applyPatches') is not False
        folder=out/('baseline-'+group['suite']);folder.mkdir()
        command=[*packet['launcher'],'vstest',str(dll),'/Settings:'+str(settings),'/TestCaseFilter:'+group['filter'],
            '/Logger:trx;LogFileName=checks.trx','/ResultsDirectory:'+str(folder)]
        h.write(folder/'invocation.json',dict(command=command,group=group,source=OLD,assembly_sha256=group['assembly_sha256'],
            ci_compiler_sdk='10.0.403',local_launcher_sdk='10.0.404',local_execution=True,build=False,gpu=False))
        before=h.runtime_inventory(packet,folder/'runtime-before.json')
        env=dict(os.environ,AIDOTNET_DISABLE_GPU='1',DOTNET_ROOT='C:/Program Files/dotnet',DOTNET_MULTILEVEL_LOOKUP='0',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_ROLL_FORWARD='LatestPatch')
        code=h.run_group(command,role['runtime_repo'],env,folder)
        after=h.runtime_inventory(packet,folder/'runtime-after.json')
        if code!=0:h.write(folder/'failure-receipt.json',dict(returncode=code,automatic_retry=False))
        assert code==0,'First repair failure preserved; no retry'
        checks=h.checked_trx(folder/'checks.trx',group)
        original=Path(role['manifest_path']).parent/'evidence'/(group['suite']+'.trx')
        already={r.attrib['testName'] for r in ET.parse(original).findall('.//{*}UnitTestResult')}
        for prior in retained:
            if prior['role']=='baseline' and prior['suite']==group['suite']:already.update(prior['test_names'])
        assert already.isdisjoint(checks['test_names']),'No passed baseline case may be repeated'
        result=dict(role='baseline',suite=group['suite'],source=OLD,assembly_sha256=group['assembly_sha256'],
            runtime_inventory_before=before,runtime_inventory_after=after,local_execution=True,**checks)
        h.write(folder/'receipt.json',result);results.append(result)
        print(json.dumps(dict(suite=group['suite'],passed=checks['passed'])),flush=True)
    for spec in packet['roles'].values():h.check_role(spec)
    h.runtime_inventory(packet)
    totals=dict(candidate=dict(unit=62+134,parity=43+86),baseline=dict(unit=134+26+19,parity=86+39))
    assert totals==packet['expected_totals']
    h.write(out/'repair-summary.json',dict(status='filter-repair-cpu-passed-root-authentication-pending',
        original_packet_sha256=PACKET_PIN,retained_actual_sessions=retained,repair_actual_sessions=results,
        combined_coverage_totals=totals,coverage_assembled_from_multiple_actual_sessions=True,
        original_ci_manifests_and_counts_unchanged=True,unchanged_gpu100_reused_per_source=True,
        source_builds=0,gpu_executions=0,timings=0,qualification=False,automatic_retry=False))
    print(json.dumps(totals))

if __name__=='__main__':main()
