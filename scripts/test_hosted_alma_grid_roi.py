"""Adversarial source-bound orchestration tests; no workload, build or timing."""
import ast,copy,inspect,json,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import alma_grid_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-alma-grid-roi-20261119.json'
class AlmaGridRoiTests(unittest.TestCase):
 def plan(self):return json.loads(PLAN.read_bytes())
 def rows(self):
  sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
  rows=[];decisions=[]
  for pair in q.PAIRS:
   for bars in (1000,10000):
    win=pair==q.TARGETS[0] and bars==10000
    rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sample(95 if win else 70) for k in ('1','2')},native_control=sample(100)))
    decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if win else 'inconclusive'))
  return rows,decisions
 def test_exact_three_pair_scope_and_honest_counts(self):
  p=self.plan();q.validate_scope(p,require_ready=False);self.assertEqual((p['cases'],p['decisions']),(30,6));self.assertEqual((p['old_candidate']['unit_passed'],p['old_candidate']['parity_passed']),(53,48));self.assertEqual((p['candidate']['unit_passed'],p['candidate']['parity_passed']),(49,31));self.assertIsInstance(p['parent_review_approved'],bool)
  self.assertEqual(p['targeted_shards'],q.shards());self.assertNotIn(b'\r',PLAN.read_bytes())
 def test_held_stops_before_sdk_input_io(self):
  held=self.plan();held['parent_review_approved']=False
  with mock.patch.object(q.p,'read',return_value=held),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('forbidden')) as io,mock.patch.object(q,'checked_inputs') as inputs:
   with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('old','new','out')
   io.assert_not_called();inputs.assert_not_called()
 def test_missing_and_mutated_evidence_stops(self):
  for field,fn in [('baseline_cpu_root_review',q.focused_baseline),('compiled_fixture_path_proof',q.compiled_proof)]:
   p=self.plan();p[field]=None
   with self.subTest(field=field),self.assertRaisesRegex(AssertionError,'UNBOUND'):fn(p)
  p=self.plan();p['compiled_fixture_path_proof']['sha256']='0'*64
  with self.assertRaises(AssertionError):q.compiled_proof(p)
 def test_policy_scope_and_control_mapping_fail_closed(self):
  for change in [lambda p:p.update(cases=40),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['native_progress_gate'].update(target_pairs=q.PAIRS),lambda p:p['native_progress_gate'].update(minimum_margin=.1),lambda p:p.update(report_import_permitted=True),lambda p:p['targeted_shards'][0]['tasks'][0].update(fixture='SharedAlmaWindowBenchmarks'),lambda p:p['targeted_shards'][0]['tasks'][0].update(implicit_pair=True)]:
   p=self.plan();change(p)
   with self.subTest(change=change),self.assertRaises(AssertionError):q.validate_scope(p,require_ready=False)
 def test_exact_source_and_actual_alma_route(self):q.verify_source_boundary();q.verify_source_fixture_routes()
 def test_source_bound_compiled_root_evidence(self):
  p=self.plan();q.verify_compiled_fixture_path(p);q.verify_actual_input(p);q.focused_baseline(p)
  proof=q.compiled_proof(p)
  self.assertTrue(all(any(x['raw_locals_equal'] is False for x in pair['protected_methods']) for pair in proof['review']['pairs']))
  self.assertTrue(all(pair['simd_calls']==2 and pair['actual_private_batch_mentions']>0 for pair in proof['review']['pairs']))
 def test_target_separated_win_accepted_without_mean_margin(self):
  rows,ds=self.rows();r=q.adjudicate_feasibility(rows,ds);self.assertTrue(r['passed']);self.assertEqual(r['new_10000_target_wins'][0]['pair'],q.TARGETS[0])
 def test_control_only_win_cannot_pass(self):
  rows,ds=self.rows()
  for row,d in zip(rows,ds):
   if row['bars']==10000 and row['pair'] in q.TARGETS:
    row['candidate_rounds']={k:copy.deepcopy(row['old_rounds']['0']) for k in ('1','2')};d['verdict']='inconclusive'
   if row['bars']==10000 and row['pair']==q.PAIRS[-1]:
    row['old_rounds']={k:dict(mean_ns=110,lower_ns=109,upper_ns=111) for k in ('0','3')};row['candidate_rounds']={k:dict(mean_ns=95,lower_ns=94,upper_ns=96) for k in ('1','2')};d['verdict']='faster'
  self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
 def test_all_six_source_comparisons_protected(self):
  for index in range(6):
   rows,ds=self.rows();ds[index]['verdict']='slower'
   with self.subTest(index=index):self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
 def test_interval_source_eligibility_prior_and_net_gates(self):
  for mutate in [lambda r,d:r[1]['candidate_rounds']['1'].update(upper_ns=99),lambda r,d:r[1]['candidate_rounds']['2'].update(upper_ns=100),lambda r,d:d[1].update(verdict='inconclusive'),lambda r,d:r[1]['old_rounds']['0'].update(mean_ns=90),lambda r,d:r[5]['candidate_rounds']['1'].update(mean_ns=110,lower_ns=109,upper_ns=111)]:
   rows,ds=self.rows();mutate(rows,ds);self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
 def test_diagnostic_cannot_qualify_or_import(self):
  rows,ds=self.rows();result=dict(accepted=False,case_count=30,decision_count=6,decisions=ds)
  with mock.patch.object(q.original,'verify_runtime'),mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(verify,'_collect',autospec=True,return_value=result),mock.patch.object(q,'native_rows',return_value=rows):out=q.collect(Path('unused'))
  self.assertTrue(out['feasibility_screen']['passed']);self.assertFalse(out['qualification']);self.assertFalse(out['accepted']);self.assertFalse(out['report_imported'])
 def test_registry_and_harness_alias_never_fall_through(self):
  p=self.plan()
  with mock.patch.object(q,'validate_scope') as check:roi_scope_dispatch.validate_scope(p);check.assert_called_once()
  with mock.patch.object(verify,'read',return_value=p),mock.patch.object(q,'collect',return_value={'accepted':False}) as check:
   self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);check.assert_called_once()
   with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
  tree=ast.parse(inspect.getsource(pipeline._verify_input_contents));branch=next(n for n in ast.walk(tree) if isinstance(n,ast.If) and ast.unparse(n.test)=="plan.get('diagnostic_kind') == 'alma-grid-native-pilot'");code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),pipeline.__file__,'exec')
  with mock.patch.object(q,'verify_harness_alias') as alias:exec(code,dict(__package__='hosted_timing',plan=p,folder=Path('unused'),proof={},verify_trx=mock.Mock(side_effect=AssertionError('fallback'))));alias.assert_called_once()
 def test_existing_size_filters_and_inventory_contract(self):
  tasks=q.shards()[0]['tasks'];self.assertEqual([q.p.methods_for(t,0) for t in tasks],[['Ooples']]);self.assertEqual([q.p.methods_for(t,2) for t in tasks],[['Ooples','Competitor']])
  text=(q.p.REPO/'.github/workflows/alma-grid-roi.yml').read_text();self.assertIn('  pull-requests: read',text);self.assertIn('Measure 30 cases',text)
 def test_publication_is_held_and_historical_verification_is_offline(self):
  p=self.plan();p['published_baseline']['head']='UNBOUND_GMA_PUBLICATION';p['inputs_ready']=False;p['parent_review_approved']=False;binding=p['published_baseline']
  with self.assertRaises(AssertionError):q.verify_published_baseline(p)
  binding['head']=q.OLD
  with mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('live API forbidden')) as live,mock.patch.object(q.common,'source_fingerprint',return_value=binding['source_fingerprint']),mock.patch.object(q.p,'git',return_value=''):q.verify_publication_provenance(p,dict(published_baseline=binding));live.assert_not_called()
if __name__=='__main__':unittest.main()
