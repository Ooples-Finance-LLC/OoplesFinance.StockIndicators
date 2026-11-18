"""Held scope and adversarial activation/native-policy tests; no builds or timing."""
import copy,json,unittest,ast,inspect
from pathlib import Path
from unittest import mock
from hosted_timing import modified_hikkake_sparse_replay_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-modified-hikkake-sparse-replay-roi-20261118.json'
class HikkakeHeldRoiTests(unittest.TestCase):
 def plan(self):return json.loads(PLAN.read_bytes())
 def rows(self):
  sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
  rows=[];decisions=[]
  for pair in q.PAIRS:
   for bars in (1000,10000):
    win=pair==q.PAIRS[1] and bars==10000
    rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sample(95 if win else 70) for k in ('1','2')},native_control=sample(100)))
    decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if win else 'inconclusive'))
  return rows,decisions
 def test_held_scope_and_actual_original_cpu_classes(self):
  p=self.plan();q.validate_scope(p,require_ready=False);self.assertEqual((p['cases'],p['decisions']),(20,4));self.assertNotIn(b'\r',PLAN.read_bytes())
  self.assertEqual((p['old_candidate']['unit_passed'],p['old_candidate']['parity_passed']),(55,8));self.assertEqual((p['candidate']['unit_passed'],p['candidate']['parity_passed']),(18,55))
 def test_unreviewed_stops_before_sdk_input_io(self):
  held=copy.deepcopy(self.plan());held.update(inputs_ready=False,parent_review_approved=False)
  with mock.patch.object(q.p,'read',return_value=held),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('forbidden')) as io,mock.patch.object(q,'checked_inputs') as inputs:
   with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('old','new','out')
   io.assert_not_called();inputs.assert_not_called()
 def test_missing_proofs_cannot_activate(self):
  for field,fn in [('baseline_focused_receipt',q.focused_baseline),('compiled_fixture_path_proof',q.verify_compiled_fixture_path),('actual_input_provenance',q.verify_actual_input)]:
   p=self.plan();p[field]=None
   with self.subTest(field=field):
    with self.assertRaisesRegex(AssertionError,'UNBOUND'):fn(p)
  p=copy.deepcopy(self.plan());p.update(inputs_ready=True,parent_review_approved=True);p['compiled_fixture_path_proof']=None
  with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(p)
 def test_policy_hash_and_full_scope_fail_closed(self):
  for mutate in [lambda p:p.update(cases=10),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['native_progress_gate'].update(minimum_margin=.1),lambda p:p['native_progress_gate'].update(minimum_new_closures=0),lambda p:p['native_win_policy_approval'].update(sha256='0'*64),lambda p:p.update(report_import_permitted=True)]:
   p=self.plan();mutate(p)
   with self.assertRaises(AssertionError):q.validate_scope(p,require_ready=False)
 def test_five_percent_separated_both_rounds_accepted(self):
  rows,ds=self.rows();self.assertTrue(q.adjudicate_feasibility(rows,ds)['passed'])
 def test_interval_source_eligibility_prior_and_net_gates(self):
  mutations=[lambda r,d:r[3]['candidate_rounds']['1'].update(upper_ns=99),lambda r,d:r[3]['candidate_rounds']['2'].update(upper_ns=100),lambda r,d:d[3].update(verdict='inconclusive'),lambda r,d:d[0].update(verdict='slower'),lambda r,d:r[3]['old_rounds']['0'].update(mean_ns=90),lambda r,d:r[1]['candidate_rounds']['1'].update(mean_ns=110,lower_ns=109,upper_ns=111)]
  for mutate in mutations:
   r,d=self.rows();mutate(r,d);self.assertFalse(q.adjudicate_feasibility(r,d)['passed'])
 def test_diagnostic_result_cannot_qualify_or_import(self):
  rows,ds=self.rows();result=dict(accepted=False,case_count=20,decision_count=4,decisions=ds)
  with mock.patch.object(q.original,'verify_runtime'),mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q,'native_rows',return_value=rows):out=q.collect(Path('unused'))
  self.assertTrue(out['feasibility_screen']['passed']);self.assertFalse(out['accepted']);self.assertFalse(out['qualification']);self.assertFalse(out['report_imported'])
 def test_registry_and_alias_do_not_fall_through(self):
  p=self.plan()
  with mock.patch.object(q,'validate_scope') as check:roi_scope_dispatch.validate_scope(p);check.assert_called_once()
  with mock.patch.object(verify,'read',return_value=p),mock.patch.object(q,'collect',return_value={'accepted':False}) as check:
   self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);check.assert_called_once()
   with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
  tree=ast.parse(inspect.getsource(pipeline._verify_input_contents));branch=next(n for n in ast.walk(tree) if isinstance(n,ast.If) and ast.unparse(n.test)=="plan.get('diagnostic_kind') == 'modified-hikkake-sparse-replay-native-pilot'")
  code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),pipeline.__file__,'exec')
  forbidden=mock.Mock(side_effect=AssertionError('fallback'))
  with mock.patch.object(q,'verify_harness_alias') as alias:exec(code,dict(__package__='hosted_timing',plan=p,folder=Path('unused'),proof={},verify_trx=forbidden));alias.assert_called_once()
  forbidden.assert_not_called()
 def test_baseline_supplement_binds_original_renko_manifest_and_hikkake_assemblies(self):
  p=self.plan();files=q.focused_baseline(p);receipt=json.loads(files['baseline-focused-receipt.json'])
  manifest=dict(runtime_files={
   'tests/bin/Release/net10.0/OoplesFinance.StockIndicators.Tests.Unit.dll':receipt['checks']['unit']['assembly_sha256'],
   'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll':receipt['checks']['parity']['assembly_sha256']})
  q.focused_baseline(p,manifest)
  for name in manifest['runtime_files']:
   bad=copy.deepcopy(manifest);bad['runtime_files'][name]='0'*64
   with self.assertRaises(AssertionError):q.focused_baseline(p,bad)
 def test_published_c20_head_and_source_identity_fail_closed(self):
  p=self.plan();binding=p['published_baseline']
  for head,diff,ok in [(binding['head'],'',True),('0'*40,'',False),(binding['head'],'src/Indicators/RenkoSnapshot.cs',False)]:
   actual=dict(head=dict(sha=head,ref=binding['branch']))
   with mock.patch.object(q.subprocess,'check_output',return_value=json.dumps(actual).encode()),mock.patch.object(q.common,'source_fingerprint',return_value=binding['source_fingerprint']),mock.patch.object(q.p,'git',return_value=diff):
    if ok:q.verify_current_publication(p)
    else:
     with self.assertRaises(AssertionError):q.verify_current_publication(p)
 def test_prepare_api_auth_is_explicit_and_historical_collection_is_offline(self):
  text=(q.p.REPO/'.github/workflows/modified-hikkake-sparse-replay-roi.yml').read_text()
  self.assertIn('  pull-requests: read',text)
  prepare=text.split('      - name: Authenticate two existing builds and explicit baseline native harness role alias')[1].split('      - name: Measure')[0]
  self.assertIn('GH_TOKEN: ${{ github.token }}',prepare)
  p=self.plan();proof=dict(published_baseline=p['published_baseline'])
  with mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('live API forbidden')) as live,mock.patch.object(q.common,'source_fingerprint',return_value=p['published_baseline']['source_fingerprint']),mock.patch.object(q.p,'git',return_value=''):
   q.verify_publication_provenance(p,proof);live.assert_not_called()
 def test_actual_bound_proof_preserves_observation_limits(self):
  p=self.plan()
  self.assertIsNotNone(p['compiled_fixture_path_proof'],'final actual proof binding required')
  q.verify_compiled_fixture_path(p);q.verify_actual_input(p)
  proof=q.compiled_proof(p)
  small=next(row for row in proof['actual_rows'] if row['name']=='HikkakeModified' and row['bars']==1000)
  self.assertEqual(small['computed_factory_probe_regions'][0]['factory_count'],125)
  self.assertTrue(all(region['sparse_present'] for region in small['computed_factory_probe_regions']))
  for mutate in [lambda d:d.update(actual_full_call_wrapper_update_jit_observed=False),lambda d:d.update(actual_scheduler_execution_observed=True),lambda d:d['actual_rows'][0].update(near_period=5),lambda d:d['actual_rows'][-1]['computed_factory_probe_regions'][0].update(sparse_present=False),lambda d:d['protected_normalized_il_equal'][0].update(method='wrong method'),lambda d:d['protected_normalized_il_equal'][0].update(il_length=1)]:
   changed=copy.deepcopy(proof);mutate(changed)
   with mock.patch.object(q,'compiled_proof',return_value=changed):
    with self.assertRaises(AssertionError):q.verify_compiled_fixture_path(p)
if __name__=='__main__':unittest.main()
