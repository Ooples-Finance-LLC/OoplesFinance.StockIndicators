"""Narrow master-input and scoped harness-role reconciliation; no timing reuse."""
import copy,gzip,hashlib,importlib.util,json,os,re,shutil,subprocess,tempfile
from pathlib import Path
from . import pipeline as p
from . import triple_body_macro_qualification as original
from . import candle_single_pass_roi as common
SCOPE='triple-body-master-reconciliation-20261117'
OLD='6469c65c50dc2c9211c92a8303734e06ae817c60'
NEW='9e914007c3438d9f251f3fab9ef321d49c22de83'
MASTER='29b0848372435d3ffaaf5c0ea156e65b0b2c59f9'
QUALIFIED='7c9f23365eb08ce20c68554aafc75c8b7ff2f7d5'
FROZEN='9a45372530e49718b01b6524f9a0db993514e1e8'
PATHS={'src/Indicators/ValuesBarExecution.BoundedWindows.cs','src/Indicators/ValuesBarExecution.TripleBodyBlocks.cs'}
UNIT=['TripleBodySharedExecutionTests','TripleBodyParallelExecutionTests','TripleBodyZeroBlockReplayTests','AdoptedColumnTests','UseInputTests','OwnedHistoryBatchTests']
PARITY=['TripleBodyTests','SharedTripleBodyBenchmarkTests']
PAIRS=original.PAIRS
MASTER_PATHS={'src/Builder/Compute/BuilderArmBinding.cs','src/Builder/Evaluation/BatchCompute.cs','src/Builder/Evaluation/SeriesEvaluator.cs','src/Calculations/Inputs/InputsUse.cs','src/Compatibility/SpanCompat.cs','src/Helpers/CalculationsHelper.cs','src/Models/StockData.cs','src/OoplesFinance.StockIndicators.csproj','src/SourceGeneration/IndicatorSeriesAccessorGenerator.cs','src/Validation/BatchInputValidation.cs'}
EXCEPTIONS={'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/SharedClassicalWindowVariantBenchmarks.cs','benchmarks/OoplesFinance.StockIndicators.CompetitorTests/SharedClassicalWindowVariantBenchmarkTests.cs'}
SCOPES=[p.BENCH,p.TESTS,'Directory.Build.props','Directory.Build.targets','Directory.Packages.props','global.json','NuGet.Config','nuget.config']
sha=lambda raw:hashlib.sha256(raw).hexdigest()
def blob(source,path):return subprocess.check_output(['git','show',source+':'+path],cwd=p.REPO)
def tree(source):
    result={}
    for row in subprocess.check_output(['git','ls-tree','-rz',source,'--',*SCOPES],cwd=p.REPO).split(b'\0'):
        if row:
            meta,path=row.split(b'\t',1);result[path.decode()]=meta.split()[2].decode()
    return result

REFERENCE='benchmarks/results/remaining-indicators/hosted-targeted-44199075375-attempt1/decision.json'
REFERENCE_SHA='9e6fb8327d4f1861654047efaea8a56aa344cd4021dc436b1649299ba06fe6d9'
def verify_prior_qualification(reference,decision):
    assert reference['decision']==REFERENCE and reference['sha256']==REFERENCE_SHA and str(reference['run_id'])=='44199075375' and reference['qualified_source']==QUALIFIED
    assert decision['status']=='qualified-for-integration' and decision['accepted'] is True and decision['phase']=='targeted'
    assert (decision['case_count'],decision['decision_count'])==(120,24) and str(decision['run_id'])=='44199075375'
    assert decision['sources']==dict(old=original.OLD,new=QUALIFIED)
    assert decision['source_gate_accepted'] is True and decision['native_progress_gate_passed'] is True
    assert any(row==dict(pair='TaLib.Candles.TwoCrows',bars=10000,own_source_verdict='faster') for row in decision['native_progress_gate']['new_10000_closures'])
    assert decision['production_integrated'] is False and decision['report_imported'] is False # retain historical creation-state flags

def verify_prior_receipts(reference):
    raw=blob(NEW,REFERENCE);assert sha(raw)==REFERENCE_SHA
    decision=json.loads(raw);verify_prior_qualification(reference,decision)
    def archived(name):
        item=decision['archives'][name];raw=gzip.decompress(blob(NEW,item['archive']));assert sha(raw)==item['sha256'];return raw
    manifest=json.loads(archived('prepared/candidate-authentication/manifest.json'))
    assert manifest['source']==QUALIFIED and manifest['source_sha256']=='c31265a72b8dcd267c40c268fd2f9c98e6419d8439d6882fd5e973e35082d0fb'
    assert manifest['classes']==dict(unit=original.UNIT,parity=original.PARITY)
    for label,classes,count,name in [('unit',original.UNIT,24,'prepared/evidence/successor.trx'),('parity',original.PARITY,85,'prepared/evidence/successor-parity.trx')]:
        with tempfile.TemporaryDirectory(prefix='prior-trx-') as temp:
            path=Path(temp)/'checks.trx';path.write_bytes(archived(name));checks=p.verify_trx(path,classes)
        assert checks==manifest['checks'][label] and checks['passed']==count
    receipt=json.loads(archived('prepared/evidence/successor-original-gpu-receipt.json'))
    assert receipt['source_sha256']==manifest['source_sha256'] and receipt['passed_cases']==100
    import xml.etree.ElementTree as ET
    trx=gzip.decompress(archived('prepared/evidence/successor-original-gpu-gpu.trx.gz'));assert sha(trx)==receipt['trx_sha256']
    results=ET.fromstring(trx).findall('.//{*}UnitTestResult');assert len(results)==100 and all(row.attrib['outcome']=='Passed' for row in results)
    spec=importlib.util.spec_from_file_location('prior_candidate_ci',p.REPO/'scripts/build-candidate-correctness.py');ci=importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)
    assert ci.gpu_module(p.REPO).verify_evidence(receipt,trx,json.loads(blob(QUALIFIED,'scripts/competitor-gpu-methods.json')),manifest['source_sha256'])==100
    assert receipt['build_origin']['source']==QUALIFIED and receipt['build_origin']['run_id']==manifest['run_id'] and receipt['build_origin']['workflow_head']==manifest['workflow_head']

def verify_reconciliation(reference,plan):
    verify_prior_receipts(reference)
    binding=reference['master_reconciliation'];path=p.REPO/binding['path'];assert p.digest(path)==binding['sha256']
    proof=p.read(path)
    assert proof['kind']=='triple-body-master-inputs-and-scoped-harness-alias-20261117'
    assert (proof['canonical'],proof['baseline'],proof['master'],proof['qualified_source'],proof['frozen_reference'])==(NEW,OLD,MASTER,QUALIFIED,FROZEN)
    assert proof['timings_reused'] is False and proof['closure_ledger_new_rows']==0
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    assert blob(OLD,next(x for x in PATHS if x.endswith('BoundedWindows.cs')))==blob(original.OLD,next(x for x in PATHS if x.endswith('BoundedWindows.cs')))
    assert blob(NEW,'src/Indicators/ValuesBarExecution.TripleBodyBlocks.cs')==blob(QUALIFIED,'src/Indicators/ValuesBarExecution.TripleBodyBlocks.cs')
    assert set(p.git('diff','--name-only',QUALIFIED,NEW,'--','src').splitlines())==MASTER_PATHS
    assert {row['path'] for row in proof['master_inputs']}==MASTER_PATHS and len(proof['master_inputs'])==10
    for row in proof['master_inputs']:
        before,after,master=(blob(source,row['path']) for source in (QUALIFIED,NEW,MASTER))
        assert sha(before)==row['qualified_sha256'] and sha(after)==row['canonical_sha256']==row['master_sha256'] and after==master
    assert b'Include="AiDotNet.Tensors" Version="0.151.1"' in blob(NEW,'src/OoplesFinance.StockIndicators.csproj')
    frozen,current=tree(FROZEN),tree(NEW)
    assert set(frozen)-set(current)==EXCEPTIONS and not set(current)-set(frozen)
    assert len(current)==proof['identical_harness_files']==1584 and all(frozen[path]==value for path,value in current.items())
    assert proof['harness_scopes']==SCOPES and set(proof['harness_exceptions'])==EXCEPTIONS
    assert proof['role_alias']==dict(artifact_roles=['successor','variants'],source=NEW,additional_builds=0,additional_cpu_executions=0,additional_gpu_executions=0,meaning='Scoped four-pair benchmark/native role shares the actual canonical artifact and receipts; no claim of original482 binary identity.')
    assert proof['runtime_tensor_version']=='0.151.1'
    return proof

def cpu_classes(plan,label):
    assert plan['triple_body_master_reconciliation'] is True
    assert plan['cpu_test_classes'][label]==UNIT
    return UNIT

def validate_scope(plan,*,require_ready=True):
    assert plan.get('triple_body_master_reconciliation') is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=NEW)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=NEW)
    assert (plan['cases'],plan['decisions'])==(120,24)
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=180)
    assert plan['targeted_shards']==original.shards()
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['native_progress_gate']==original.NATIVE_POLICY
    assert plan['required_native_closure_pair']=='TaLib.Candles.TwoCrows' and plan['closure_ledger_new_rows']==0
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1') and plan['require_fixture_inventory'] is True
    assert plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert plan['targeted_reference']['scope']==SCOPE and plan['targeted_reference']['qualified_source']==QUALIFIED
    assert plan['targeted_reference']['decision']==REFERENCE and plan['targeted_reference']['sha256']==REFERENCE_SHA and str(plan['targeted_reference']['run_id'])=='44199075375'
    verify_reconciliation(plan['targeted_reference'],plan)
    for label in ('old','successor','variants'):cpu_classes(plan,label)
    approval=plan['scope_approval'];assert p.digest(p.REPO/approval['path'])==approval['sha256'];approved=p.read(p.REPO/approval['path'])
    assert approved['scope']==SCOPE and (approved['cases'],approved['decisions'])==(120,24) and approved['additional_rows_counted']==0
    for key,source in [('old_candidate',OLD),('candidate',NEW)]:
        c=plan[key];assert c['source']==source and c['unit_classes']==UNIT and c['parity_classes']==PARITY
    assert not any(plan.get(k) for k in ('triple_body_macro_qualification','dependency_reconciliation','qualify_variants','standalone_variants','reuse_inputs','diagnostic_only'))
    if require_ready:
        assert plan['inputs_ready'] is True and approved['approved'] is True,'Reconciliation actual receipts/approval remain UNBOUND'
        assert re.fullmatch('[a-f0-9]{64}',plan['harness_sha256'])
        for key in ('old_candidate','candidate'):
            c=plan[key]
            for field in ('ci_run','artifact_id'):assert re.fullmatch('[1-9][0-9]*',c[field])
            for field in ('producer','evidence_source'):assert re.fullmatch('[a-f0-9]{40}',c[field])
            for field in ('manifest_sha256','gpu_receipt_sha256','production_sha256','source_fingerprint','artifact_zip_sha256'):assert re.fullmatch('[a-f0-9]{64}',c[field])
            assert isinstance(c['unit_passed'],int) and c['unit_passed']>0 and isinstance(c['parity_passed'],int) and c['parity_passed']>0

def checked_inputs(folder,c):
    folder=Path(folder).resolve()
    def read(name):
        path=(folder/name).resolve();assert path.is_relative_to(folder) and path.is_file();return path.read_bytes()
    spec=importlib.util.spec_from_file_location('master_candidate_ci',p.REPO/'scripts/build-candidate-correctness.py');ci=importlib.util.module_from_spec(spec);spec.loader.exec_module(ci)
    source=c['source'];fingerprint=common.source_fingerprint(source)
    assert fingerprint==c['source_fingerprint'] and sha(read('manifest.json'))==c['manifest_sha256']
    manifest=json.loads(read('manifest.json'));assert (manifest['source'],manifest['run_id'],manifest['workflow_head'])==(source,c['ci_run'],c['producer'])
    assert manifest['classes']==dict(unit=UNIT,parity=PARITY)
    production=ci.validate_layout(manifest,read,source,fingerprint);assert production==c['production_sha256']
    for label,classes in [('unit',UNIT),('parity',PARITY)]:
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

def source_authentication(plan):return dict(old=plan['old_candidate'],successor=plan['candidate'],variants_alias_of='successor',additional_cpu_executions=0,additional_gpu_executions=0)

def verify_harness_alias(folder,plan,proof):
    assert proof['source_authentication']==source_authentication(plan)
    for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
        assert (folder/'evidence'/('successor'+suffix)).read_bytes()==(folder/'evidence'/('variants'+suffix)).read_bytes()
    assert proof['production_sha256']['variants']==proof['production_sha256']['successor']
    assert proof['checks']['variants']==proof['checks']['successor']
    assert proof['tested_runtime_dependencies']['variants']==proof['tested_runtime_dependencies']['successor']

def verify_authentication(folder,plan,proof):
    validate_scope(plan);assert proof['preparation_mode']=='authenticated-triple-body-master-reconciliation'
    verify_harness_alias(folder,plan,proof)
    assert p.digest(folder/'common'/original.HARNESS_ASSEMBLY)==plan['harness_sha256']
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        auth=checked_inputs(folder/(label+'-authentication'),plan[key])
        assert proof['production_sha256'][label]==auth['production'] and proof['tested_runtime_dependencies'][label]==auth['closure']
        assert proof['checks'][label]==dict(auth['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    manifest=p.read(folder/'successor-authentication/manifest.json');prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    from .targeted import verify_reference
    verify_reference(plan) # Exercise the real preparation boundary before SDK or I/O.
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out=map(lambda x:Path(x).resolve(),(old_candidate,candidate,out));assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new=checked_inputs(old_candidate,plan['old_candidate']),checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    for label,folder,auth in [('old',old_candidate,old),('successor',candidate,new)]:shutil.copytree(folder,out/(label+'-authentication'))
    prefix='benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/'
    for name,digest in new['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path=out/'common'/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(new['read']('blobs/'+digest))
    assert p.digest(out/'common'/original.HARNESS_ASSEMBLY)==plan['harness_sha256']
    checks={};production={};dependencies={}
    for label,auth,source in [('old',old,OLD),('successor',new,NEW),('variants',new,NEW)]:
        (out/'production'/(label+'.dll')).write_bytes(auth['read']('blobs/'+auth['production']))
        checks[label]=dict(auth['manifest']['checks']['unit'],original_gpu_source=source);production[label]=auth['production'];dependencies[label]=auth['closure']
        for suffix,raw in [('.trx',auth['read']('evidence/unit.trx')),('-parity.trx',auth['read']('evidence/parity.trx')),('-original-gpu-receipt.json',auth['gpu_receipt']),('-original-gpu-gpu.trx.gz',auth['gpu_trx'])]:(out/'evidence'/(label+suffix)).write_bytes(raw)
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-triple-body-master-reconciliation',source_authentication=source_authentication(plan),common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN));p.write(out/'provenance.json',proof);p.verify_inputs(out);return proof

def verify_runtime(folder,expected):original.verify_runtime(folder,expected)
def adjudicate_native(rows,decisions):
    result=original.adjudicate_native(rows,decisions)
    preserved=any(row['pair']=='TaLib.Candles.TwoCrows' and row['bars']==10000 for row in result['new_10000_closures'])
    result.update(required_two_crows_closure_preserved=preserved,ledger_new_rows=0,passed=result['passed'] and preserved)
    return result

def collect(folder,inputs=None):
    from .verify import _collect
    if inputs is not None:p.verify_inputs(Path(inputs))
    result=_collect(Path(folder),'targeted',preflight=False,candle_predicate_gate=True)
    assert (result['case_count'],result['decision_count'])==(120,24)
    if inputs is not None:assert result['input_provenance_sha256']==p.digest(Path(inputs)/'provenance.json')
    native=adjudicate_native(original.native_rows(folder),result['decisions']);source=result['accepted']
    result.update(source_gate_accepted=source,native_progress_gate_passed=native['passed'],native_progress_gate=native,accepted=source and native['passed'],closure_ledger_new_rows=0)
    return result
