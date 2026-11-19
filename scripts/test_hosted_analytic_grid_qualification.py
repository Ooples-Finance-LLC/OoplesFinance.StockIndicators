"""Focused full-qualification gates and source/fixture bounds; no workloads."""
import ast,copy,inspect,json,tempfile,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import analytic_grid_qualification as q
from hosted_timing import pipeline,targeted,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-analytic-grid-qualification-20261119.json'
class FullAnalyticTests(unittest.TestCase):
 def plan(self):return json.loads(PLAN.read_bytes())
 def rows(self):
  sm=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
  rows=[];ds=[]
  for shard in q.shards():
   for task in shard['tasks']:
    for op in task['operations']:
     for bars in (1000,10000):
      for method in [m for m in task['methods'] if m in q.p.OWNED]:
       win=task['fixture']=='LibraryPairBenchmarks' and op['pair']==q.TARGETS[0] and bars==10000
       rows.append(dict(fixture=task['fixture'],pair=op['pair'],bars=bars,method=method,old_rounds={k:sm(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sm(95 if win else 70) for k in ('1','2')},native_controls={m:sm(100) for m in task['methods'] if m not in q.p.OWNED}))
       ds.append(dict(fixture=task['fixture'],pair=op['pair'],bars=bars,method=method,verdict='faster' if win else 'inconclusive'))
  return rows,ds
 def collect(self,rows=None,ds=None):
  r,d=self.rows();r=r if rows is None else rows;d=d if ds is None else ds
  result=dict(accepted=True,case_count=216,decision_count=44,decisions=d)
  with mock.patch.object(q.original,'verify_runtime'),mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(verify,'_collect',autospec=True,return_value=result),mock.patch.object(q,'sample_rows',return_value=r):return q.collect(Path('unused'))
 def test_exact_held_scope(self):
  q.validate_scope(self.plan(),require_ready=False);self.assertEqual([(x['cases'],x['decisions']) for x in q.shards()],[(110,22),(106,22)]);self.assertNotIn(b'\r',PLAN.read_bytes())
 def test_actual_fixture_source_contracts(self):q.verify_fixture_contracts(self.plan())
 def test_projection_reuses_unchanged_actual_receipts(self):
  p=self.plan();r=q.roi_projection(p);q.prior.validate_scope(r,require_ready=False);self.assertEqual(r['candidate'],p['candidate']);self.assertEqual(r['old_candidate'],p['old_candidate'])
 def test_parent_hold_before_sdk_or_inputs(self):
  p=self.plan();p['parent_review_approved']=False
  with mock.patch.object(q.p,'read',return_value=p),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('forbidden')) as io,mock.patch.object(q,'checked_inputs') as inputs:
   with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('old','new','out')
   io.assert_not_called();inputs.assert_not_called()
 def test_actual_root_authenticated_screen(self):q.verify_screen(self.plan())
 def test_missing_screen_review_blocks(self):
  p=self.plan();p['screen_gate']['independently_authenticated']=False
  with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.verify_screen(p)
 def test_scope_policy_mutations_block(self):
  for mutate in [lambda p:p.update(cases=215),lambda p:p['matrix'].update(max_parallel=3),lambda p:p['matrix'].update(shards=1),lambda p:p['native_progress_gate'].update(target_pairs=q.LP),lambda p:p.update(diagnostic_only=True),lambda p:p['targeted_shards'][0]['tasks'][1].update(implicit_pair=True)]:
   p=self.plan();mutate(p)
   with self.assertRaises(AssertionError):q.validate_scope(p,require_ready=False)
 def test_sentinel_both_sizes_all_profiles(self):
  rows,ds=self.rows()
  for pair in ['TaLib.Functions.Ema','TaLib.Functions.T3']:self.assertEqual({(x['bars'],x['method']) for x in ds if x['pair']==pair},{(n,m) for n in (1000,10000) for m in ['Ooples','BuilderFull','BuilderLatestOnly']})
 def test_snapshot_batches_excluded_convolution_included(self):
  rows,ds=self.rows()
  for pair in ['QuanTAlib.Fwma','QuanTAlib.Afirma']:self.assertEqual({x['method'] for x in ds if x['pair']==pair},{'BuilderFull','BuilderLatestOnly'})
  self.assertEqual({x['method'] for x in ds if x['pair']=='QuanTAlib.Convolution'},{'Ooples','BuilderFull','BuilderLatestOnly'})
 def test_primary_win_can_qualify_without_import(self):
  r=self.collect();self.assertTrue(r['accepted']);self.assertTrue(r['qualification']);self.assertFalse(r['production_integrated']);self.assertFalse(r['report_imported'])
 def test_all44_source_regressions_reject(self):
  for i in range(44):
   rows,ds=self.rows();ds[i]['verdict']='slower';self.assertFalse(self.collect(rows,ds)['accepted'])
 def test_prior_builder_native_win_protected(self):
  rows,ds=self.rows();r=next(r for r in rows if r['fixture']=='SharedFibonacciWeightedBenchmarks' and r['bars']==1000 and r['method']=='BuilderFull');r['candidate_rounds']['1']=dict(mean_ns=110,lower_ns=109,upper_ns=111);self.assertFalse(self.collect(rows,ds)['accepted'])
 def test_control_only_new_win_cannot_pass(self):
  rows,ds=self.rows()
  for r,d in zip(rows,ds):
   if r['fixture']=='LibraryPairBenchmarks' and r['pair'] in q.TARGETS and r['bars']==10000:r['candidate_rounds']={k:copy.deepcopy(r['old_rounds']['0']) for k in ('1','2')};d['verdict']='inconclusive'
   if r['fixture']=='LibraryPairBenchmarks' and r['pair']=='TaLib.Functions.Ema' and r['bars']==10000:r['old_rounds']={k:dict(mean_ns=110,lower_ns=109,upper_ns=111) for k in ('0','3')};r['candidate_rounds']={k:dict(mean_ns=95,lower_ns=94,upper_ns=96) for k in ('1','2')};d['verdict']='faster'
  self.assertFalse(self.collect(rows,ds)['accepted'])
 def test_target_scope_and_collect_registry(self):
  p=self.plan()
  with mock.patch.object(q,'validate_scope') as check:targeted.verify_reference(p);check.assert_called_once()
  self.assertEqual(targeted.expected_totals(p),(216,44))
  with mock.patch.object(verify,'read',return_value=p),mock.patch.object(q,'collect',return_value={'accepted':True}) as check:
   self.assertTrue(verify.collect(Path('unused'),'targeted')['accepted']);check.assert_called_once()
   with self.assertRaises(AssertionError):verify.collect(Path('unused'),'diagnostic')
 def test_canonical_prepare_two_measure_workflow(self):
  s=(q.p.REPO/'.github/workflows/analytic-grid-qualification.yml').read_text();self.assertIn('max-parallel: 2',s);self.assertIn('shard: [0, 1]',s);self.assertIn('    needs: prepare',s);self.assertIn('    needs: measure',s);self.assertNotIn('dotnet build',s);self.assertNotIn('dotnet test',s)
 def test_two_shard_raw_operation_mapping_and_native_contexts(self):
  with tempfile.TemporaryDirectory() as tmp:
   root=Path(tmp);data={}
   for shard in q.shards():
    folder=root/('shard'+str(shard['index']));folder.mkdir();(folder/'provenance.json').touch();data[folder/'provenance.json']=dict(shard=shard['index'],raw_root='raw');rounds=[]
    for task in shard['tasks']:
     for i in range(4):
      report=root/(str(shard['index'])+'-'+task['fixture']+'-'+str(i)+'.json');cases=[]
      for op in task['operations']:
       for bars in (1000,10000):
        for method in q.p.methods_for(task,i):cases.append(dict(key=(op['raw'],bars,method),value=dict(mean_ns=1,lower_ns=0,upper_ns=2)))
      data[report]=dict(Benchmarks=cases);rounds.append(dict(fixture=task['fixture'],index=i,report=str(report)))
    data[folder/'status.json']=dict(rounds=rounds)
   with mock.patch.object(q.p,'read',side_effect=lambda p:data[Path(p)]),mock.patch.object(verify,'archived',side_effect=lambda root,raw,name:Path(name)),mock.patch.object(verify,'sample',side_effect=lambda c:c['value']),mock.patch('hosted_timing.checkpoints.case_key',side_effect=lambda c:c['key']):rows=q.sample_rows(root)
   self.assertEqual(len(rows),44);self.assertTrue(any(r['pair']=='QuanTAlib.Gma' and r['fixture']=='SharedWeightedWindowBenchmarks' for r in rows));self.assertTrue(any(r['pair']=='TaLib.Functions.Ema' and r['fixture']=='SharedRecursiveStateBenchmarks' for r in rows))
 def test_each_input_verification_hook_selects_qualification_once(self):
  for function,method in [(pipeline.verify_inputs,'verify_authentication'),(pipeline._verify_input_contents,'verify_harness_alias')]:
   tree=ast.parse(inspect.getsource(function));branches=[n for n in ast.walk(tree) if isinstance(n,ast.If) and ast.unparse(n.test)=="plan.get('analytic_grid_qualification')"];self.assertEqual(len(branches),1)
   code=compile(ast.fix_missing_locations(ast.Module(body=[branches[0]],type_ignores=[])),pipeline.__file__,'exec')
   with mock.patch.object(q,method) as call:exec(code,dict(__package__='hosted_timing',plan=self.plan(),folder=Path('unused'),proof={}));call.assert_called_once()
if __name__=='__main__':unittest.main()
