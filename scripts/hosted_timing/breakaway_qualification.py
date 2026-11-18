"""Isolated Breakaway qualification; reuse existing builds, fixtures and gates."""
import json,os,re,shutil,subprocess
from pathlib import Path
from . import pipeline as p
from . import master_reconciliation as auth
from . import triple_body_macro_qualification as native
from . import candle_single_pass_roi as common

SCOPE='breakaway-isolated-qualification-20261118'
OLD='9e914007c3438d9f251f3fab9ef321d49c22de83'
NEW='c5c5b3cb2dd2f37bb5c0d8b5d1d38e368a063b67'
BREAK='TaLib.Candles.Breakaway'
TWO='TaLib.Candles.TwoCrows'
EXTENDED=[BREAK,'TaLib.Candles.ConcealingBabySwallow','TaLib.Candles.LadderBottom']
PAIRS=EXTENDED+[TWO]
UNIT=['MultiBarCandleSharedExecutionTests','BreakawayZeroBlockReplayTests']
PARITY=['ExtendedReversalTests','SharedTaReversalContinuationBenchmarkTests']
PATHS={'src/Indicators/ValuesBarExecution.BoundedWindows.cs','src/Indicators/ValuesBarExecution.BreakawayBlocks.cs','tests/ModelsTests/BreakawayZeroBlockReplayTests.cs'}
HARNESS='07e764bd27110b4452e1f181f466a6ea0e60b4ae4667d01b6c75f468ae8aee07'

def shards():
    def task(fixture,pairs,methods,cases,decisions):
        return dict(fixture=fixture,operations=[dict(raw=pair,pair=pair) for pair in pairs],methods=methods,implicit_pair=False,cases=cases,decisions=decisions)
    return [dict(index=0,cases=108,decisions=24,tasks=[
        task('LibraryPairBenchmarks',PAIRS,['Ooples','Competitor'],40,8),
        task('SharedMultiBarCandleBenchmarks',EXTENDED,['BuilderFull','BuilderLatestOnly'],48,12),
        task('SharedTripleBodyBenchmarks',[TWO],native.METHODS,20,4)])]

def cpu_classes(plan,label):
    expected=UNIT if label=='successor' else auth.UNIT
    assert plan['breakaway_isolated_qualification'] is True and plan['cpu_test_classes'][label]==expected
    return expected

def verify_source(plan):
    assert set(p.git('diff','--name-only',OLD,NEW,'--','src','tests',p.BENCH,p.TESTS).splitlines())==PATHS
    before,after=auth.tree(OLD),auth.tree(NEW)
    assert before==after and len(before)==1584
    proof_binding=plan['harness_source_proof'];assert p.digest(p.REPO/proof_binding['path'])==proof_binding['sha256']
    proof=p.read(p.REPO/proof_binding['path'])
    assert proof==dict(baseline=OLD,candidate=NEW,identical_harness_files=1584,harness_scopes=auth.SCOPES,
        common_artifact_source=OLD,additional_builds=0,additional_cpu_executions=0,additional_gpu_executions=0,
        selected_fixtures={task['fixture']:task['methods'] for task in shards()[0]['tasks']})

def verify_reference(plan):
    reference=plan['targeted_reference']
    assert reference['scope']==SCOPE and reference['run_id']=='44222776323' and reference['qualified_source']==OLD
    assert reference['decision']=='benchmarks/results/remaining-indicators/hosted-targeted-44222776323-attempt1/decision.json'
    assert re.fullmatch('[a-f0-9]{40}',reference['archive_source']) and re.fullmatch('[a-f0-9]{64}',reference['sha256']),'Current master qualification reference remains UNBOUND'
    assert not p.git('diff','--name-only',OLD,reference['archive_source'],'--',*native.previous.SOURCE_IDENTITY_PATHS)
    raw=auth.blob(reference['archive_source'],reference['decision']);assert auth.sha(raw)==reference['sha256']
    decision=json.loads(raw)
    assert decision['status']=='qualified-for-integration' and decision['accepted'] is True and decision['phase']=='targeted'
    assert (decision['case_count'],decision['decision_count'])==(120,24) and str(decision['run_id'])=='44222776323'
    assert decision['workflow_head']=='304d97f61654ad4159b36b4ca6082667ea74df78'
    assert decision['sources']==dict(old=auth.OLD,new=OLD)
    assert decision['source_gate_accepted'] is True and decision['native_progress_gate_passed'] is True
    assert decision['master_reconciliation'] is True and decision['closure_ledger_new_rows']==0
    assert decision['native_progress_gate']['required_two_crows_closure_preserved'] is True
    return decision

def validate_scope(plan,*,require_ready=True):
    assert plan.get('breakaway_isolated_qualification') is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(108,24) and plan['targeted_shards']==shards()
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=180)
    assert plan['measurement_policy']==common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['native_progress_gate']==native.NATIVE_POLICY and plan['required_native_closure_pair']==BREAK
    assert plan['existing_two_crows_recounted'] is False
    reference=plan['targeted_reference']
    assert reference['scope']==SCOPE and reference['run_id']=='44222776323' and reference['qualified_source']==OLD
    assert reference['decision']=='benchmarks/results/remaining-indicators/hosted-targeted-44222776323-attempt1/decision.json'
    assert plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1') and plan['require_fixture_inventory'] is True
    assert plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15' and plan['harness_sha256']==HARNESS
    assert not any(plan.get(key) for key in ('triple_body_master_reconciliation','triple_body_macro_qualification','dependency_reconciliation','qualify_variants','standalone_variants','reuse_inputs','diagnostic_only','coverage_reference','trix_preflight'))
    for label in ('old','successor','variants'):cpu_classes(plan,label)
    for key,source,unit,parity,counts,run,artifact in [('old_candidate',OLD,auth.UNIT,auth.PARITY,(73,85),'44217513798','14389198881'),('candidate',NEW,UNIT,PARITY,(18,244),'44222085527','14392185497')]:
        item=plan[key];assert item['source']==source and item['unit_classes']==unit and item['parity_classes']==parity
        assert (item['unit_passed'],item['parity_passed'])==counts and item['ci_run']==run and item['artifact_id']==artifact
        assert item['producer']=='2a1737f30e5e1164189d0209b39ea46bf38c1239'
        assert re.fullmatch('[a-f0-9]{40}',item['evidence_source'])
        for field in ('manifest_sha256','gpu_receipt_sha256','production_sha256','source_fingerprint','artifact_zip_sha256'):assert re.fullmatch('[a-f0-9]{64}',item[field])
    verify_source(plan)
    approval=plan['scope_approval'];assert p.digest(p.REPO/approval['path'])==approval['sha256'];approved=p.read(p.REPO/approval['path'])
    assert approved['scope']==SCOPE and (approved['cases'],approved['decisions'])==(108,24) and approved['only_new_closure_pair']==BREAK
    if require_ready:
        assert plan['inputs_ready'] is True and approved['approved'] is True,'Break qualification readiness remains UNBOUND'
        verify_reference(plan)

def source_authentication(plan):
    return dict(old=plan['old_candidate'],successor=plan['candidate'],variants_alias_of='old',additional_cpu_executions=0,additional_gpu_executions=0)

def checked_inputs(folder,item):
    return auth.checked_inputs(folder,item,unit=item['unit_classes'],parity=item['parity_classes'])

def verify_harness_alias(folder,plan,proof):
    assert proof['source_authentication']==source_authentication(plan)
    for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
        assert (folder/'evidence'/('old'+suffix)).read_bytes()==(folder/'evidence'/('variants'+suffix)).read_bytes()
    for field in ('production_sha256','checks','tested_runtime_dependencies'):assert proof[field]['old']==proof[field]['variants']

def verify_authentication(folder,plan,proof):
    validate_scope(plan);assert proof['preparation_mode']=='authenticated-breakaway-isolated-qualification'
    verify_harness_alias(folder,plan,proof)
    assert p.digest(folder/'common'/native.HARNESS_ASSEMBLY)==HARNESS
    for label,key in [('old','old_candidate'),('successor','candidate')]:
        checked=checked_inputs(folder/(label+'-authentication'),plan[key])
        assert proof['production_sha256'][label]==checked['production'] and proof['tested_runtime_dependencies'][label]==checked['closure']
        assert proof['checks'][label]==dict(checked['manifest']['checks']['unit'],original_gpu_source=plan[key]['source'])
        for suffix,raw in [('.trx',checked['read']('evidence/unit.trx')),('-parity.trx',checked['read']('evidence/parity.trx')),('-original-gpu-receipt.json',checked['gpu_receipt']),('-original-gpu-gpu.trx.gz',checked['gpu_trx'])]:assert (folder/'evidence'/(label+suffix)).read_bytes()==raw
    manifest=p.read(folder/'old-authentication/manifest.json');prefix=p.BENCH+'/bin/Release/net10.0/'
    assert proof['common_manifest']=={name[len(prefix):]:digest for name,digest in manifest['runtime_files'].items() if name.startswith(prefix)}

def prepare(old_candidate,candidate,out):
    plan=p.read(p.PLAN);validate_scope(plan)
    from .targeted import verify_reference as verify_targeted
    verify_targeted(plan)
    assert subprocess.check_output(['dotnet','--version'],text=True).strip()==plan['proposed_dotnet_sdk']
    old_candidate,candidate,out=map(lambda path:Path(path).resolve(),(old_candidate,candidate,out))
    assert not out.exists() and not out.is_relative_to(old_candidate) and not out.is_relative_to(candidate)
    old,new=checked_inputs(old_candidate,plan['old_candidate']),checked_inputs(candidate,plan['candidate'])
    assert old['closure']==new['closure']
    out.mkdir();(out/'common').mkdir();(out/'production').mkdir();(out/'evidence').mkdir()
    for label,folder in [('old',old_candidate),('successor',candidate)]:shutil.copytree(folder,out/(label+'-authentication'))
    prefix=p.BENCH+'/bin/Release/net10.0/'
    for name,digest in old['manifest']['runtime_files'].items():
        if name.startswith(prefix):
            path=out/'common'/name[len(prefix):];path.parent.mkdir(parents=True,exist_ok=True);path.write_bytes(old['read']('blobs/'+digest))
    checks={};production={};dependencies={}
    for label,item,source in [('old',old,OLD),('successor',new,NEW),('variants',old,OLD)]:
        (out/'production'/(label+'.dll')).write_bytes(item['read']('blobs/'+item['production']))
        checks[label]=dict(item['manifest']['checks']['unit'],original_gpu_source=source);production[label]=item['production'];dependencies[label]=item['closure']
        for suffix,raw in [('.trx',item['read']('evidence/unit.trx')),('-parity.trx',item['read']('evidence/parity.trx')),('-original-gpu-receipt.json',item['gpu_receipt']),('-original-gpu-gpu.trx.gz',item['gpu_trx'])]:(out/'evidence'/(label+suffix)).write_bytes(raw)
    proof=dict(plan_sha256=p.digest(p.PLAN),sources=plan['sources'],source_trees={label:p.git('rev-parse',plan['sources'][label]+':src') for label in ('old','successor','variants')},sdk=plan['proposed_dotnet_sdk'],dotnet_info=subprocess.check_output(['dotnet','--info'],text=True),workflow_head=p.git('rev-parse','HEAD'),run_id=os.environ['GITHUB_RUN_ID'],run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],preparation_mode='authenticated-breakaway-isolated-qualification',source_authentication=source_authentication(plan),common_manifest=p.manifest(out/'common'),evidence_manifest=p.manifest(out/'evidence'),production_sha256=production,checks=checks,tested_runtime_dependencies=dependencies)
    from .fixture_inventory import record_inventory
    proof['fixture_inventory_sha256']=record_inventory(out,plan,p.digest(p.PLAN));p.write(out/'provenance.json',proof);p.verify_inputs(out);return proof

def adjudicate_native(rows,decisions):
    result=native.adjudicate_native(rows,decisions,pairs=PAIRS,new_closure_pairs=[BREAK])
    row=next(row for row in rows if row['pair']==TWO and row['bars']==10000);control=row['native_control']
    result['two_crows_robust_10000_diagnostic']=all(value['upper_ns']<control['lower_ns'] and value['mean_ns']<=0.90*control['mean_ns'] for value in row['candidate_rounds'].values())
    return result

def collect(folder,inputs=None):
    from .verify import _collect
    if inputs is not None:p.verify_inputs(Path(inputs))
    result=_collect(Path(folder),'targeted',preflight=False,candle_predicate_gate=True)
    assert (result['case_count'],result['decision_count'])==(108,24)
    if inputs is not None:assert result['input_provenance_sha256']==p.digest(Path(inputs)/'provenance.json')
    source=result['accepted'];gate=adjudicate_native(native.native_rows(folder,pairs=PAIRS),result['decisions'])
    result.update(source_gate_accepted=source,native_progress_gate_passed=gate['passed'],native_progress_gate=gate,accepted=source and gate['passed'],existing_two_crows_recounted=False,new_closure_credit_pairs=[BREAK] if source and gate['passed'] else [])
    return result
