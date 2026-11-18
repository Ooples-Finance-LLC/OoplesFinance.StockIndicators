"""Conditional seven-pair qualification using existing fixtures and authenticated inputs."""
import json,re
from pathlib import Path
from . import pipeline as p
from . import candle_isolated_roi as roi
from . import triple_body_macro_qualification as native

SCOPE='seven-candle-word-qualification-20261118'
MODE='authenticated-seven-candle-word-qualification'
OLD,NEW=roi.OLD,roi.NEW
TRIPLE=native.PAIRS
EXTENDED=['TaLib.Candles.Breakaway','TaLib.Candles.ConcealingBabySwallow','TaLib.Candles.LadderBottom']
PAIRS=TRIPLE+EXTENDED
REQUIRED=['TaLib.Candles.Breakaway','TaLib.Candles.ThreeInside']
ROI_PLAN='benchmarks/plans/hosted-candle-isolated-roi-20261118.json'
# This frozen file binds artifact identities only. Timing admission is separate.
ROI_PLAN_SHA='3e993ec5b5dc56e429e8fc7807b30b8129aa88b9fca96ba4bd2eeaa24ab7c667'
REFERENCE=dict(scope=SCOPE,decision='benchmarks/results/remaining-indicators/hosted-targeted-44222776323-attempt1/decision.json',run_id='44222776323',qualified_source=OLD,archive_source='510198e84424b4874f7318fbe75611d411286fb9',sha256='98ed72c49054ce733d2bcea090f88c05ca8e37f557ea541d8d6280606ba35b3c')

def shards():
    def task(fixture,pairs,methods,cases,decisions):
        return dict(fixture=fixture,operations=[dict(raw=pair,pair=pair) for pair in pairs],methods=methods,implicit_pair=False,cases=cases,decisions=decisions)
    return [dict(index=0,cases=198,decisions=42,tasks=[
        task('LibraryPairBenchmarks',PAIRS,['Ooples','Competitor'],70,14),
        task('SharedTripleBodyBenchmarks',TRIPLE,native.METHODS,80,16),
        task('SharedMultiBarCandleBenchmarks',EXTENDED,['BuilderFull','BuilderLatestOnly'],48,12)])]

def cpu_classes(plan,label):
    expected=roi.UNIT if label=='successor' else roi.BASE_UNIT
    assert plan['seven_candle_word_qualification'] is True and plan['cpu_test_classes'][label]==expected
    return expected

def verify_reference(plan):
    reference=plan['targeted_reference'];assert reference==REFERENCE
    assert not p.git('diff','--name-only',OLD,reference['archive_source'],'--',*native.previous.SOURCE_IDENTITY_PATHS)
    raw=roi.blob(reference['archive_source'],reference['decision']);assert roi.sha(raw)==reference['sha256']
    decision=json.loads(raw)
    assert decision['status']=='qualified-for-integration' and decision['accepted'] is True and decision['phase']=='targeted'
    assert (decision['case_count'],decision['decision_count'])==(120,24) and str(decision['run_id'])=='44222776323'
    assert decision['workflow_head']=='304d97f61654ad4159b36b4ca6082667ea74df78'
    assert decision['sources']==dict(old='6469c65c50dc2c9211c92a8303734e06ae817c60',new=OLD)
    assert decision['source_gate_accepted'] is True and decision['native_progress_gate_passed'] is True
    assert decision['master_reconciliation'] is True and decision['closure_ledger_new_rows']==0
    assert decision['native_progress_gate']['required_two_crows_closure_preserved'] is True
    return decision

def approval_documents(plan):
    result=[]
    for key in ('scope_approval','roi_approval'):
        binding=plan[key];path=p.REPO/binding['path'];assert p.digest(path)==binding['sha256'];result.append(p.read(path))
    parent,evidence=result
    assert parent['scope']==SCOPE and (parent['cases'],parent['decisions'])==(198,42) and parent['required_native_closure_pairs']==REQUIRED
    assert evidence['scope']==SCOPE and evidence['baseline']==OLD and evidence['candidate']==NEW
    return parent,evidence

def verify_roi_approval(evidence):
    assert evidence['approved'] is True and evidence['root_authenticated'] is True,'UNBOUND root-authenticated ROI approval'
    assert re.fullmatch('[1-9][0-9]*',evidence['run_id']) and re.fullmatch('[a-f0-9]{40}',evidence['workflow_head'])
    binding=evidence['summary'];assert re.fullmatch('[a-f0-9]{64}',binding['sha256']),'UNBOUND ROI summary hash'
    path=p.REPO/binding['path'];assert p.digest(path)==binding['sha256'];result=p.read(path)
    assert result['diagnostic_only'] is True and result['qualification'] is False and result['accepted'] is False
    assert (result['case_count'],result['decision_count'])==(30,6) and result['phase']=='diagnostic'
    assert result['sources']==dict(old=OLD,new=NEW) and result['closure_ledger_new_rows']==0
    gate=result['promotion_gate'];assert gate['passed'] is True and gate['all_six_source_comparisons_no_regression'] is True and gate['ladder_bottom_no_source_regression'] is True
    assert gate['qualification_authorized'] is False and gate['closure_credit']==0 and gate['minimum_native_margin']==.1
    wins=gate['target_10000_robust_native_wins'];assert len(wins)==2 and {row['pair'] for row in wins}==set(REQUIRED)
    assert all(row['bars']==10000 and row['passed'] is True for row in wins)
    assert len(result['decisions'])==6
    assert {(row['pair'],row['bars']) for row in result['decisions']}=={(pair,bars) for pair in roi.PAIRS for bars in (1000,10000)}
    for row in result['decisions']:
        assert row['fixture']=='LibraryPairBenchmarks' and row['method']=='Ooples' and row['verdict']!='slower'
        assert str(row['provenance']['host']['run_id'])==evidence['run_id'] and row['provenance']['workflow_head']==evidence['workflow_head']
    assert all(next(row for row in result['decisions'] if row['pair']==pair and row['bars']==10000)['verdict']=='faster' for pair in REQUIRED)

def validate_scope(plan,*,require_ready=True):
    assert plan.get('seven_candle_word_qualification') is True
    assert plan['sources']==dict(published=OLD,old=OLD,successor=NEW,variants=OLD)
    assert plan['gpu_sources']==dict(old=OLD,successor=NEW,variants=OLD)
    assert (plan['cases'],plan['decisions'])==(198,42) and plan['targeted_shards']==shards()
    assert plan['matrix']==dict(shards=1,max_parallel=1,timeout_minutes_per_shard=180)
    assert plan['measurement_policy']==roi.common.POLICY and plan['runtime_policy_overrides']=={}
    assert plan['native_progress_gate']==native.NATIVE_POLICY and plan['required_native_closure_pairs']==REQUIRED
    assert plan['existing_two_crows_recounted'] is False and plan['runtime_dependency_alignment']==dict(tensor_version='0.151.1')
    assert plan['closure_ledger_new_rows']==0
    assert plan['require_fixture_inventory'] is True and plan['proposed_dotnet_sdk']=='10.0.403' and plan['expected_runtime_version']=='.NET 10.0.15'
    assert not any(plan.get(key) for key in ('diagnostic_only','diagnostic_kind','triple_body_master_reconciliation','triple_body_macro_qualification','qualify_variants','standalone_variants','reuse_inputs','coverage_reference','trix_preflight'))
    assert p.digest(p.REPO/ROI_PLAN)==ROI_PLAN_SHA
    pinned=p.read(p.REPO/ROI_PLAN)
    for key in ('old_candidate','candidate','harness_alias_policy','harness_sha256'):assert plan[key]==pinned[key]
    assert (plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed'])==(73,85)
    assert (plan['candidate']['unit_passed'],plan['candidate']['parity_passed'])==(117,329)
    for label in ('old','successor','variants'):cpu_classes(plan,label)
    roi.verify_source_boundary();verify_reference(plan)
    parent,evidence=approval_documents(plan)
    if require_ready:
        assert plan['inputs_ready'] is True and plan['parent_review_approved'] is True and parent['approved'] is True,'UNBOUND qualification readiness or parent approval'
        verify_roi_approval(evidence)

def verify_authentication(folder,plan,proof):
    return roi.verify_authentication(folder,plan,proof,validator=validate_scope,preparation_mode=MODE)

def prepare(old_candidate,candidate,out):
    def boundary(plan):
        validate_scope(plan)
        from .targeted import verify_reference as verify_targeted
        verify_targeted(plan)
    return roi.prepare(old_candidate,candidate,out,validator=boundary,preparation_mode=MODE)

def adjudicate_native(rows,decisions):
    result=native.adjudicate_native(rows,decisions,pairs=PAIRS,new_closure_pairs=REQUIRED)
    present={row['pair'] for row in result['new_10000_closures']}
    result['required_native_closure_pairs']=REQUIRED
    result['both_required_closures_passed']=set(REQUIRED)<=present
    result['passed']=result['passed'] and result['both_required_closures_passed']
    return result

def collect(folder,inputs=None):
    from .verify import _collect
    if inputs is not None:p.verify_inputs(Path(inputs))
    result=_collect(Path(folder),'targeted',preflight=False,candle_predicate_gate=True)
    assert (result['case_count'],result['decision_count'])==(198,42)
    if inputs is not None:assert result['input_provenance_sha256']==p.digest(Path(inputs)/'provenance.json')
    source=result['accepted'];gate=adjudicate_native(native.native_rows(folder,pairs=PAIRS),result['decisions'])
    result.update(source_gate_accepted=source,native_progress_gate_passed=gate['passed'],native_progress_gate=gate,
        accepted=source and gate['passed'],existing_two_crows_recounted=False,new_closure_credit_pairs=REQUIRED if source and gate['passed'] else [])
    return result
