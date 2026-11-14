"""Freeze fresh private execution qualifications on the reconciled runtime."""
import argparse, datetime, hashlib, json, re, shutil, subprocess, sys
from pathlib import Path

p=argparse.ArgumentParser();p.add_argument('record');a=p.parse_args()
r=Path(__file__).parent;checkpoint=r/'continuation-latest-20261028.json'
d=json.loads(checkpoint.read_text());records=d['performance_continuation_20261103'];q=records[a.record]
if q.get('requires_warmup_policy_approval'):
 assert q.get('warmup_policy_approved') is True,'Obtain the recorded warmup-policy decision before freezing qualification'
tooling=Path(q['tooling_checkout']);repo=Path(q['repo'])
base=records[q['baseline_record']]
reconciled='timing_run' not in base
prior=records[q['reference_record']] if q.get('reference_record') else (records['containment-active-windows-20261106'] if reconciled else base)
assert base['fresh_publication_ci_status']=='passed' and q['baseline']==base['published_head']
assert q['gpu_passed']==100 and q['qualified_source']==subprocess.check_output(['git','rev-parse','HEAD'],cwd=repo,text=True).strip()
assert not subprocess.check_output(['git','status','--porcelain'],cwd=repo)
tensor_version=q.get('runtime_tensor_version','0.148.0')
assert tensor_version in ('0.148.0','0.149.0','0.150.0')
variant=records[q.get('runtime_variant_record','variants-runtime-0148-20261106')];assert variant['gpu_passed']==100
if tensor_version in ('0.149.0','0.150.0'):
 assert base['approved_tensor_version']==variant['approved_tensor_version']==tensor_version
body=a.record.startswith('body-shadow')
fixture='SharedBodyShadowBenchmarks' if body else 'SharedMoneyFlowIndexBenchmarks'
pairs=['TaLib.Candles.'+n for n in ['BeltHold','Marubozu','ClosingMarubozu','SpinningTop','HighWave','LongLine','ShortLine']] if body else ['TaLib.Functions.Mfi','Skender.GetMfi']
mapping={v:v.rsplit('.',1)[-1] if body else v for v in pairs}
fixtures={f:dict(m) for f,m in q.get('fixture_mapping',{fixture:mapping}).items()}
for sentinel in q.get('known_composition_sentinels',['TaLib.Candles.Harami']):
 f='SharedRangeDojiBenchmarks' if sentinel=='TaLib.Candles.LongLeggedDoji' else 'SharedContainmentCandleBenchmarks'
 fixtures.setdefault(f,{})[sentinel]=sentinel
shards=[]
methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel']
fixture_methods=q.get('fixture_methods',{})
assert set(fixture_methods)<=set(fixtures), 'Unknown fixture method override'
sys.path.insert(0,str(tooling/'scripts'))
from hosted_timing.pipeline import methods_for
for f,m in fixtures.items():
 for pair,raw in m.items():
  tasks=[dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=pair,pair=pair)],methods=['Ooples','Competitor'],implicit_pair=False),dict(fixture=f,operations=[dict(raw=raw,pair=pair)],methods=fixture_methods.get(f,methods),implicit_pair=False)]
  for task in tasks:
   assert len(task['methods'])==len(set(task['methods']))
   task['cases']=sum(2*len(methods_for(task,index)) for index in range(4))
   task['decisions']=2*len(methods_for(task,0))
  shards.append(dict(index=len(shards),tasks=tasks,cases=sum(task['cases'] for task in tasks),decisions=sum(task['decisions'] for task in tasks)))
cases,decisions=sum(shard['cases'] for shard in shards),sum(shard['decisions'] for shard in shards)
assert (cases,decisions)==(q['planned_cases'],q['planned_decisions'])
publishing=Path(d['publishing_checkout'])
decision=(Path(prior['archive'])/'decision.json').relative_to(publishing).as_posix()
def blob(path):return subprocess.check_output(['git','show',q['baseline']+':'+path],cwd=tooling)
raw=blob(decision);accepted=json.loads(raw);assert accepted['accepted']
reference=dict(scope=a.record,run_id=prior['timing_run'],decision=decision,sha256=hashlib.sha256(raw).hexdigest(),qualified_source=prior['qualified_source'])
destination=None
publication_receipt_changed=False
if reconciled:
 evidence=q.get('runtime_alignment_evidence','benchmarks/results/remaining-indicators/dependency-runtime-alignment-20261106/verification.json')
 receipt=r/('pr266-ci-'+q['baseline'][:9]+'-20261104.json')
 destination=tooling/'benchmarks/plans/evidence'/receipt.name;destination.parent.mkdir(parents=True,exist_ok=True)
 publication_receipt_changed=not destination.exists() or destination.read_bytes()!=receipt.read_bytes()
 shutil.copyfile(receipt,destination)
 reference['dependency_reconciliation']=dict(kind=q.get('runtime_alignment_kind','master-tensors-0.147.0-to-0.148.0'),timings_reused=False,evidence=evidence,sha256=hashlib.sha256(blob(evidence)).hexdigest(),publication_ci=dict(path=destination.relative_to(tooling).as_posix(),sha256=hashlib.sha256(destination.read_bytes()).hexdigest()))
definition=dict(fixtures=fixtures,implicit_fixtures=set(),paths=set(q['paths']),methods={f:fixture_methods.get(f,methods) for f in fixtures},totals=(cases,decisions),shards=len(shards),baseline_phase='targeted',baseline_totals=(accepted['case_count'],accepted['decision_count']))
plan=json.loads((tooling/'benchmarks/plans/hosted-renko-price-normalization-20261105.json').read_text())
for key in ('reuse_inputs','correctness_reuse','superseded_plan','prior_rejected_qualification','individual_qualifications'):plan.pop(key,None)
plan['sources']=dict(published=q['baseline'],old=q['baseline'],successor=q['qualified_source'],variants=variant['qualified_source'])
plan['gpu_sources']={k:plan['sources'][k] for k in ('old','successor','variants')}
plan['cpu_test_classes']=dict(old=q.get('baseline_unit',q['unit']).split(','),successor=q['unit'].split(','))
parallel=q.get('max_parallel',3);assert 1<=parallel<=6
plan.update(runtime_dependency_alignment=dict(tensor_version=tensor_version),targeted_reference=reference,targeted_shards=shards,affected_pairs=[v for m in fixtures.values() for v in m],matrix=dict(shards=len(shards),max_parallel=parallel,timeout_minutes_per_shard=180),cases=cases,decisions=decisions)
if (tooling/'scripts/hosted_timing/fixture_inventory.py').is_file():
 plan['require_fixture_inventory']=True
status=json.loads((Path(q['out'])/'status.json').read_text())
plan['source_correctness']=dict(unit=q['unit_passed'],comparator=q['comparator_passed'],actual_local_gpu=100,source_sha256=q['source_fingerprint'],cpu_build_run=status['build_origin']['run_id'],cpu_build_workflow_head=status['build_origin']['workflow_head'])
if q.get('selected_records'):
 plan['individual_qualifications']=[]
 for selected in q['selected_records']:
  item=records[selected];assert item['accepted'] and item['local_hosted_collection_equal']
  original_plan=json.loads((Path(item['tooling_checkout'])/'benchmarks/plans'/item['plan']).read_text())
  original_tensor=original_plan.get('runtime_dependency_alignment',{}).get('tensor_version','0.144.0')
  plan['individual_qualifications'].append(dict(record=selected,source=item['qualified_source'],original_baseline=item['baseline'],run_id=item['timing_run'],verdicts=item['verdicts'],timings_reused=False,original_timing_tensor_version=original_tensor))
plan.update(scope=q['publication_summary'],call_graph=['All mapped consumers of the changed execution path plus recorded composition-risk sentinels, in batch/Full/LatestOnly.'],limits=['At least one faster and zero slower; complete decisions, no pooling or selected imports.',f'Unchanged sampling/invocation/CPU gates; {parallel} concurrent runners within the six-runner total cap.',f'All tested tensor dependency closures must match the {tensor_version} measurement runtime.'],approval='User authorized continued performance repairs and dependency/runtime reconciliation; fresh complete correctness/GPU100 and qualification required.')
if q.get('requires_warmup_policy_approval'):
 selected=q.get('selected_records',[])
 if selected:
  assert 'body-shadow-retained-differences-20261107' in selected and 2<=len(selected)<=4
  assert set(selected)<={'hilbert-floating-ring-cache-20261107','adl-private-wide-publication-20261107','body-shadow-retained-differences-20261107','sum-directional-exact-movement-20261107'}
  if 'sum-directional-exact-movement-20261107' in selected:
   assert records['sum-directional-exact-movement-20261107']['verification_scope_approved'] is True
  assert all(records[name]['accepted'] and records[name]['local_hosted_collection_equal'] for name in selected)
  assert (cases,decisions) in ((270,54),(330,66),(360,72),(420,84),(450,90))
 else:
  assert a.record in ('body-shadow-retained-differences-20261107','body-shadow-compact-slot-20261108') and (cases,decisions)==(240,48)
  if a.record=='body-shadow-compact-slot-20261108':assert q['verification_scope_approved'] is True
 assert q['warmup_policy_approved'] is True and plan['measurement_policy']['fixed_invocation_warmups']==1000
 plan['measurement_policy']['fixed_invocation_pair_warmups']={'TaLib.Candles.Harami':20000}
 plan['qualification_warmup_approval']=dict(approved=True,proposal=q['warmup_policy_proposal'],approval=q['warmup_policy_approval'],diagnostic_run='42335244138',diagnostic_evidence_commit='b119122e3a7e5aa201aa540e3154af0699347805',fixture='LibraryPairBenchmarks',methods=['Ooples','Competitor'],bars=[1000,10000],builder_policy='unchanged')
 plan['limits'].append('Approved Harami-only batch exception: 20,000 unmeasured warmups for Ooples and Competitor at both bar counts; all other batch warmups and all builder policies unchanged.')
plan['created_utc']=plan['frozen_utc']=datetime.datetime.now(datetime.timezone.utc).isoformat()
sys.path.insert(0,str(tooling/'scripts'))
from hosted_timing.targeted import SCOPES,verify_reference
assert a.record not in SCOPES;SCOPES[a.record]=definition;verify_reference(plan)
path=tooling/'benchmarks/plans'/('hosted-'+a.record+'.json');assert not path.exists();path.write_text(json.dumps(plan,indent=2)+'\n',newline='\n')
target=tooling/'scripts/hosted_timing/targeted.py';s=target.read_text();needle='def expected_totals(plan):';assert s.count(needle)==1;target.write_text(s.replace(needle,'SCOPES['+repr(a.record)+'] = '+repr(definition)+'\n\n\n'+needle),newline='\n')
workflow=tooling/'.github/workflows/cpu-kernel-performance.yml';s=workflow.read_text()
options=re.findall(r'        options: \[benchmarks/plans/[^\r\n]*\]',s);assert len(options)==1
needle=options[0];assert path.relative_to(tooling).as_posix() not in needle
workflow.write_text(s.replace(needle,needle[:-1]+', '+path.relative_to(tooling).as_posix()+']'),newline='\n')
attrs=tooling/'.gitattributes';attrs.write_text(attrs.read_text()+path.relative_to(tooling).as_posix()+' text eol=lf\n'+(destination.relative_to(tooling).as_posix()+' -text\n' if destination else ''),newline='\n')
test=f'''from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AlignedPrivatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = {(cases,decisions)!r}
    target_shard_index = {0 if len(shards)==1 else 1}
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/{path.relative_to(tooling).as_posix()!r})
        p.start();self.addCleanup(p.stop)
    def test_runtime_and_risk_sentinel(self):
        p=read(REPO/{path.relative_to(tooling).as_posix()!r})
        self.assertEqual({tensor_version!r},p['runtime_dependency_alignment']['tensor_version'])
        self.assertTrue(set({q.get('known_composition_sentinels',['TaLib.Candles.Harami'])!r}).issubset(p['affected_pairs']))
        self.assertEqual({q.get('baseline_unit',q['unit']).split(',')!r},p['cpu_test_classes']['old'])
        self.assertEqual(({q['unit_passed']},{q['comparator_passed']},100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p);self.assertNotIn('correctness_reuse',p)
'''
name='scripts/test_hosted_'+a.record.removesuffix('-20261106').replace('-','_')+'.py';(tooling/name).write_text(test,newline='\n')
d=json.loads(checkpoint.read_text());q=d['performance_continuation_20261103'][a.record]
q.update(plan=path.name,plan_test_path=name,status='Aligned complete plan frozen; orchestration verification pending')
if destination:
 q['publication_receipt_path']=destination.relative_to(tooling).as_posix()
 q['publication_receipt_changed']=publication_receipt_changed
checkpoint.write_text(json.dumps(d,indent=2)+'\n');print(cases,decisions,hashlib.sha256(path.read_bytes()).hexdigest())
