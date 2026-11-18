"""Pinned two-consumer Ultimate qualification with exact artifact reuse and native progress gate."""
import gzip,hashlib,importlib.util,json,os,re,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p
from . import candle_single_pass_roi as common
from . import triple_body_macro_qualification as original
from . import ultimate_direct_long_roi as roi
import xml.etree.ElementTree as ET
SCOPE='ultimate-direct-long-qualification-20261118'
KIND='ultimate-direct-long-qualification'
OLD='9e914007c3438d9f251f3fab9ef321d49c22de83'
NEW='c5bfd4950982aa80dddbfd2db6358ad43ddb9e4c'
PAIRS=['TaLib.Functions.UltOsc','Skender.GetUltimate']
HARNESS_PATH='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'+original.HARNESS_ASSEMBLY
HARNESS_SCOPES=[p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
BASE_UNIT=['TripleBodySharedExecutionTests','TripleBodyParallelExecutionTests','TripleBodyZeroBlockReplayTests','AdoptedColumnTests','UseInputTests','OwnedHistoryBatchTests']
BASE_PARITY=['TripleBodyTests','SharedTripleBodyBenchmarkTests']
UNIT=['WiderUltimateExecutionTests','BoundedUltimateExecutionTests','CertifiedUltimateRatioTests']
PARITY=['TrueRangeRatioComparisonTests']
PATHS={'src/Indicators/ValuesBarExecution.Ultimate.cs','tests/ModelsTests/WiderUltimateExecutionTests.cs'}
sha=lambda raw:hashlib.sha256(raw).hexdigest()
def blob(source,path):return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)

def shards():
    def task(fixture,methods,cases,decisions):
        return dict(fixture=fixture,operations=[dict(raw=x,pair=x) for x in PAIRS],methods=methods,implicit_pair=False,cases=cases,decisions=decisions)
    return [dict(index=0,tasks=[task('LibraryPairBenchmarks',['Ooples','Competitor'],20,4),
        task('SharedUltimateBenchmarks',['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],40,8)],cases=60,decisions=12)]


def validate_scope(plan,*,require_ready=True):
    assert plan['ultimate_direct_long_qualification'] is True
    assert not plan.get('diagnostic_kind') and not plan.get('diagnostic_only')
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(60,12)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=120)
    assert plan['targeted_shards']==shards()
    assert plan['targeted_reference']==dict(scope=SCOPE)
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['require_fixture_inventory'] is True and plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['closure_ledger_new_rows']==0 and plan['automatic_qualification'] is False
    assert plan['require_net_10000_point_progress'] is True
    assert plan['native_progress_gate']==NATIVE_POLICY
    assert plan['screen_gate']['run']=='44345359923'
    assert plan['screen_gate']['plan_sha256']=='740b6a696ea1e288de714221b5010be3a09ebcb2b60b283c9d4564790ff8dedb'
    assert plan['screen_gate']['workflow_head']=='d206d07904dde715f8717e2eb89fa585f4fe143a'
    assert plan['screen_gate']['summary_sha256']=='b43157f63a54fae1d65816d55985c288f919e80e04a04044b57280c03988897b'
    assert plan['harness_alias_policy']==dict(common_artifact_role='old',variants_artifact_role='old',baseline_source=OLD,candidate_source=NEW,require_scoped_source_identity=True,claim_artifact_binary_identity=False)
    assert not any(plan.get(k) for k in ('coverage_reference','standalone_variants','qualify_variants','trix_preflight','reuse_inputs'))
    for label,key,source in [('old','old_candidate',OLD),('successor','candidate',NEW),('variants','old_candidate',OLD)]:
        c=plan[key];assert c['source']==source and plan['cpu_test_classes'][label]==c['unit_classes']
        for category in ('unit','parity'):
            values=c[category+'_classes'];assert values and len(set(values))==len(values) and all(v.isidentifier() for v in values)
    assert plan['old_candidate']['unit_classes']==BASE_UNIT and plan['old_candidate']['parity_classes']==BASE_PARITY
    assert plan['candidate']['unit_classes']==UNIT and plan['candidate']['parity_classes']==PARITY
    assert plan['candidate']['ci_run']=='44341191246' and plan['candidate']['producer']=='2a1737f30e5e1164189d0209b39ea46bf38c1239'
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True,'UNBOUND actual receipts or parent review'
        assert plan['screen_gate']['independently_authenticated'] is True
        verify_focused_checks(plan)
        assert re.fullmatch('[a-f0-9]{64}',plan['harness_sha256'])
        for key in ('old_candidate','candidate'):
            c=plan[key]
            for field in ('ci_run','artifact_id'):assert re.fullmatch('[1-9][0-9]*',c[field])
            for field in ('producer','evidence_source'):assert re.fullmatch('[a-f0-9]{40}',c[field])
            for field in ('manifest_sha256','gpu_receipt_sha256','production_sha256','source_fingerprint','artifact_zip_sha256','actual_harness_sha256'):assert re.fullmatch('[a-f0-9]{64}',c[field])
            assert type(c['unit_passed']) is int and c['unit_passed']>0 and type(c['parity_passed']) is int and c['parity_passed']>0
    verify_source_boundary()

def verify_source_boundary():
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert not p.git('diff','--name-only',OLD,NEW,'--',*HARNESS_SCOPES)

def checked_inputs(folder,c):
    folder=Path(folder).resolve()
    def read(name):
        path=(folder/name).resolve();assert path.is_relative_to(folder) and path.is_file();return path.read_bytes()
    spec=importlib.util.spec_from_file_location('master_candidate_ci',p.REPO/'scripts/build-candidate-correctness.py');ci=importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)
    source=c['source'];fingerprint=common.source_fingerprint(source)
    assert fingerprint==c['source_fingerprint'] and sha(read('manifest.json'))==c['manifest_sha256']
    manifest=json.loads(read('manifest.json'));assert (manifest['source'],manifest['run_id'],manifest['workflow_head'])==(source,c['ci_run'],c['producer'])
    assert manifest['classes']==dict(unit=c['unit_classes'],parity=c['parity_classes'])
    production=ci.validate_layout(manifest,read,source,fingerprint);assert production==c['production_sha256']
    assert manifest['runtime_files'][HARNESS_PATH]==c['actual_harness_sha256']
    for label,classes in [('unit',c['unit_classes']),('parity',c['parity_classes'])]:
        with tempfile.TemporaryDirectory(prefix='master-trx-') as temp:
            path=Path(temp)/'checks.trx';path.write_bytes(read('evidence/'+label+'.trx'));checks=p.verify_trx(path,classes)
        assert checks==manifest['checks'][label] and checks['passed']==c[label+'_passed']
    assert not p.git('diff','--name-only',source,c['evidence_source'],'--',*original.previous.SOURCE_IDENTITY_PATHS)
    gpu_path='benchmarks/results/shared-rollout/local-gpu-contracts/'
    receipt_raw,trx_raw=(blob(c['evidence_source'],gpu_path+n) for n in ('receipt.json','gpu.trx.gz'));assert sha(receipt_raw)==c['gpu_receipt_sha256']
    receipt=json.loads(receipt_raw);assert ci.gpu_module(p.REPO).verify_evidence(receipt,gzip.decompress(trx_raw),json.loads(blob(source,'scripts/competitor-gpu-methods.json')),fingerprint)==100
    assert receipt['build_origin']['source']==source and receipt['build_origin']['run_id']==c['ci_run'] and receipt['build_origin']['workflow_head']==c['producer'] and str(receipt['build_origin']['artifact_id'])==c['artifact_id']
    assert receipt['test_assembly_sha256']==manifest['runtime_files'][ci.ROOTS[-1]+'/OoplesFinance.StockIndicators.CompetitorTests.dll']
    from .runtime_dependencies import tensor_closure
    with tempfile.TemporaryDirectory(prefix='master-closure-') as temp:
        runtime=Path(temp);prefix=ci.ROOTS[1]+'/'
        for name,digest in manifest['runtime_files'].items():
            if name.startswith(prefix):
                path=runtime/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(read('blobs/'+digest))
        closure=tensor_closure(runtime,'OoplesFinance.StockIndicators.Tests.Unit.deps.json')
    assert closure['packages']['AiDotNet.Tensors']=='0.151.1'
    return dict(manifest=manifest,read=read,production=production,closure=closure,gpu_receipt=receipt_raw,gpu_trx=trx_raw)
def harness_role_alias(plan,old,new):
    """Select one authenticated baseline harness, preserving both actual identities."""
    verify_source_boundary()
    actual={role:auth['manifest']['runtime_files'][HARNESS_PATH] for role,auth in [('old',old),('successor',new)]}
    assert actual==dict(old=plan['old_candidate']['actual_harness_sha256'],successor=plan['candidate']['actual_harness_sha256'])
    assert actual['old']==plan['harness_sha256']
    trees={role:p.git('rev-parse',source+':'+p.BENCH) for role,source in [('old',OLD),('successor',NEW)]}
    assert trees['old']==trees['successor']
    return dict(policy=plan['harness_alias_policy'],actual_artifact_harness_sha256=actual,
                actual_artifact_manifest_sha256=dict(old=plan['old_candidate']['manifest_sha256'],successor=plan['candidate']['manifest_sha256']),
                common_harness_sha256=actual['old'],common_artifact_role='old',
                benchmark_source_trees=trees,
                source_scopes=HARNESS_SCOPES,scoped_source_identical=True,
                actual_artifact_binary_identical=actual['old']==actual['successor'])

def source_authentication(plan):return dict(old=plan['old_candidate'],successor=plan['candidate'],variants_alias_of='old',additional_cpu_executions=0,additional_gpu_executions=0)

def verify_harness_alias(folder,plan,proof):
    assert proof['source_authentication']==source_authentication(plan)
    for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
        assert (folder/'evidence'/('old'+suffix)).read_bytes()==(folder/'evidence'/('variants'+suffix)).read_bytes()
    assert proof['production_sha256']['variants']==proof['production_sha256']['old']
    assert proof['checks']['variants']==proof['checks']['old']
    assert proof['tested_runtime_dependencies']['variants']==proof['tested_runtime_dependencies']['old']

def verify_authentication(folder,plan,proof):
    validate_scope(plan);assert proof['preparation_mode']=='authenticated-ultimate-direct-long-qualification'
    verify_harness_alias(folder,plan,proof)
    focused=verify_focused_checks(plan)
    assert proof['focused_fixture_checks']=={label:sha(raw) for label,raw in focused.items()}
    for label,raw in focused.items():assert (folder/'evidence'/(label+'-shared-fixture.trx')).read_bytes()==raw
    receipts={label:focused_receipt_bytes(plan,label) for label in focused}
    assert proof['focused_fixture_receipts']=={label:sha(raw) for label,raw in receipts.items()}
    for label,raw in receipts.items():assert (folder/'evidence'/(label+'-shared-fixture.receipt.json')).read_bytes()==raw
    assert p.digest(folder/'common'/original.HARNESS_ASSEMBLY)==plan['harness_sha256']
    authenticated={}
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        auth=checked_inputs(folder/(label+'-authentication'),plan[key])
        authenticated[label]=auth
        assert proof['production_sha256'][label]==auth['production'] and proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    assert proof['harness_role_alias']==harness_role_alias(plan,authenticated['old'],authenticated['successor'])
    manifest=p.read(folder/'old-authentication/manifest.json');prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out=map(lambda x:Path(x).resolve(),(old_candidate,candidate,out));assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new=checked_inputs(old_candidate,plan['old_candidate']),checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    focused=verify_focused_checks(plan)
    alias=harness_role_alias(plan,old,new)
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    for label,folder,auth in [('old',old_candidate,old),('successor',candidate,new)]:shutil.copytree(folder,out/(label+'-authentication'))
    prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    for name,digest in old['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path=out/'common'/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(old['read']('blobs/'+digest))
    assert p.digest(out/'common'/original.HARNESS_ASSEMBLY)==plan['harness_sha256']
    checks={};production={};dependencies={}
    for label,auth,source in [('old',old,OLD),('successor',new,NEW),('variants',old,OLD)]:
        (out/'production'/(label+'.dll')).write_bytes(auth['read']('blobs/'+auth['production']))
        checks[label]=dict(auth['manifest']['checks']['unit'],original_gpu_source=source);production[label]=auth['production'];dependencies[label]=auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:(out/'evidence'/(label+suffix)).write_bytes(raw)
    for label,raw in focused.items():(out/'evidence'/(label+'-shared-fixture.trx')).write_bytes(raw)
    receipts={label:focused_receipt_bytes(plan,label) for label in focused}
    for label,raw in receipts.items():(out/'evidence'/(label+'-shared-fixture.receipt.json')).write_bytes(raw)
    proof=dict(focused_fixture_checks={label:sha(raw) for label,raw in focused.items()},focused_fixture_receipts={label:sha(raw) for label,raw in receipts.items()},plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-ultimate-direct-long-qualification',source_authentication=source_authentication(plan),harness_role_alias=alias,common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN));p.write(out/'provenance.json',proof);p.verify_inputs(out);return proof
NATIVE_POLICY=dict(fixture='LibraryPairBenchmarks',method='Ooples',native_method='Competitor',native_round=2,
    old_rounds=[0,3],candidate_rounds=[1,2],minimum_margin=.10,new_closure_bars=10000,
    minimum_new_closures=1,require_own_source_faster=True,require_net_10000_point_progress=True,
    protect_prior_confident_wins_both_sizes=True,required_new_closure_pair='Skender.GetUltimate')

def focused_receipt_bytes(plan,label):
    entry=plan['focused_fixture_checks'][label]
    assert entry['receipt_path']==f'benchmarks/plans/evidence/ultimate-direct-long-shared-{label}-20261118.receipt.json'
    assert re.fullmatch('[a-f0-9]{64}',entry['receipt_sha256'])
    raw=(p.REPO/entry['receipt_path']).read_bytes();assert sha(raw)==entry['receipt_sha256']
    return raw

def verify_focused_checks(plan):
    checks=plan['focused_fixture_checks'];assert set(checks)=={'old','successor'}
    result={}
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        receipt=checks[label]
        assert receipt['source']==plan[key]['source'] and receipt['production_sha256']==plan[key]['production_sha256']
        assert receipt['cases']==4 and re.fullmatch('[a-f0-9]{64}',receipt['sha256'])
        relative=Path(receipt['path']);assert not relative.is_absolute() and '..' not in relative.parts
        assert relative.as_posix()==f'benchmarks/plans/evidence/ultimate-direct-long-shared-{label}-20261118.trx'
        raw=(p.REPO/relative).read_bytes();assert sha(raw)==receipt['sha256']
        auxiliary=json.loads(focused_receipt_bytes(plan,label));candidate=plan[key]
        assert auxiliary['status']=='actual-ultimate-shared-fixture-auxiliary-passed'
        assert auxiliary['arm']==('old' if label=='old' else 'new')
        for field in ('source','source_fingerprint','ci_run','manifest_sha256'):
            assert auxiliary[field]==candidate[field]
        assert auxiliary['trx_sha256']==receipt['sha256'] and auxiliary['passed_cases']==4
        assert auxiliary['test_method']=='OoplesFinance.StockIndicators.CompetitorTests.SharedAllocationProbeTests.UltimateFixtureHandlesSingleOutputAndWorkersAndRestoresSettings'
        assert auxiliary['runtime_pins']['OoplesFinance.StockIndicators.dll']==candidate['production_sha256']
        assert auxiliary['runtime_pins']['OoplesFinance.StockIndicators.CompetitorBenchmarks.dll']==candidate['actual_harness_sha256']
        assert auxiliary['runtime_closure_files_verified']==185 and auxiliary['host_runtime']=='10.0.15'
        assert auxiliary['auxiliary_bootstrap']=={'AIDOTNET_DISABLE_GPU':'1'}
        assert auxiliary['new_local_builds']==auxiliary['new_measurements']==0
        assert auxiliary['gpu_reexecuted'] is False and auxiliary['original_cpu_gpu_receipts_changed'] is False
        assert auxiliary['auxiliary_only'] is True and auxiliary['qualification'] is False
        assert auxiliary['vstest_timeout_seconds']==120
        assert {(case['count'],case['operation']) for case in auxiliary['cases']}=={(count,pair) for count in (29,513) for pair in PAIRS}
        assert len(auxiliary['cases'])==4
        tests=ET.fromstring(raw).findall('.//{*}UnitTestResult')
        assert len(tests)==4 and all(t.attrib['outcome']=='Passed' and 'SharedAllocationProbeTests.UltimateFixtureHandlesSingleOutputAndWorkersAndRestoresSettings' in t.attrib['testName'] for t in tests)
        names=[t.attrib['testName'] for t in tests];assert len(set(names))==4
        combinations=set()
        for name in names:
            count=re.search(r'\bcount:\s*(29|513)\b',name);assert count
            pair=[pair for pair in PAIRS if pair in name];assert len(pair)==1
            combinations.add((int(count.group(1)),pair[0]))
        assert combinations=={(count,pair) for count in (29,513) for pair in PAIRS}
        result[label]=raw
    return result

def collect(folder):
    from .verify import _collect
    original.verify_runtime(folder,p.read(p.PLAN)['expected_runtime_version'])
    result=_collect(Path(folder),'targeted',preflight=False,ultimate_gate=True)
    assert (result['case_count'],result['decision_count'])==(60,12)
    lp=[d for d in result['decisions'] if d['fixture']=='LibraryPairBenchmarks' and d['method']=='Ooples']
    native=roi.adjudicate_feasibility(roi.native_rows(folder),lp)
    native['required_new_closure_pair']='Skender.GetUltimate'
    native['required_pair_passed']=any(row['pair']=='Skender.GetUltimate' for row in native['new_10000_target_wins'])
    native['passed']=native['passed'] and native['required_pair_passed']
    source=result['accepted'] and all(d['verdict']!='slower' for d in result['decisions'])
    result.update(source_gate_accepted=source,native_progress_gate_passed=native['passed'],native_progress_gate=native,
        accepted=source and native['passed'],qualification=True,production_integrated=False,report_imported=False)
    return result
