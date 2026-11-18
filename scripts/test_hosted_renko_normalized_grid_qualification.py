"""Focused full-return qualification scope and adversarial native gates; no builds."""
import ast,copy,inspect,json,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import renko_normalized_grid_qualification as q
from hosted_timing import pipeline,targeted,verify
PLAN=q.p.REPO/'benchmarks/plans/hosted-renko-normalized-grid-qualification-20261118.json'
class RenkoQualificationTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for index,pair in enumerate(q.PAIRS):
            for bars in (1000,10000):
                win=index==0 and bars==10000
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sample(80 if win else 70) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if win else 'inconclusive'))
        return rows,decisions
    def test_actual_held_scope_and_screen(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual((plan['cases'],plan['decisions']),(44,8))
        self.assertNotIn(b'\r',PLAN.read_bytes())
        held=copy.deepcopy(plan);held.update(inputs_ready=False,parent_review_approved=False)
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(held)
        self.assertEqual((plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed']),(31,15))
        self.assertEqual((plan['candidate']['unit_passed'],plan['candidate']['parity_passed']),(55,8))
    def test_inventory_matches_previous_44_case_family_without_standalone_substitution(self):
        prior=json.loads((q.p.REPO/'benchmarks/plans/hosted-renko-endpoint-cache-20261105.json').read_bytes())
        def inventory(shards):return sorted((task['fixture'],operation['pair'],tuple(task['methods'])) for shard in shards for task in shard['tasks'] for operation in task['operations'])
        self.assertEqual(inventory(prior['targeted_shards']),inventory(q.shards()))
        tasks=q.shards()[0]['tasks'];self.assertEqual(sum(task['cases'] for task in tasks),44)
        self.assertEqual(sum(task['decisions'] for task in tasks),8)
        self.assertEqual(tasks[1]['fixture'],'SharedSkenderRenkoChartNativeBenchmarks')
        self.assertEqual(tasks[1]['methods'],['Snapshot','NativeSingle','NativeParallel'])
    def test_scope_and_measurement_policy_fail_closed(self):
        for mutate in [lambda p:p.update(cases=20),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p['candidate']['unit_classes'].pop(),lambda p:p['targeted_shards'][0]['tasks'][1].update(fixture='SharedRenkoSnapshotBenchmarks'),lambda p:p['native_progress_gate'].update(minimum_margin=.01)]:
            plan=self.plan();mutate(plan)
            with self.assertRaises((AssertionError,KeyError)):q.validate_scope(plan,require_ready=False)
    def test_native_win_requires_both_rounds_margin_intervals_and_own_faster(self):
        rows,decisions=self.rows();self.assertTrue(q.adjudicate_feasibility(rows,decisions)['passed'])
        for mutate in [lambda r,d:r[1]['candidate_rounds']['1'].update(mean_ns=91),lambda r,d:r[1]['candidate_rounds']['2'].update(upper_ns=100),lambda r,d:d[1].update(verdict='inconclusive'),lambda r,d:r[1]['old_rounds']['0'].update(mean_ns=95)]:
            rows,decisions=self.rows();mutate(rows,decisions)
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_prior_wins_and_strict_net_progress_protected(self):
        rows,decisions=self.rows();rows[3]['candidate_rounds']['1'].update(mean_ns=105,lower_ns=104,upper_ns=106)
        gate=q.adjudicate_feasibility(rows,decisions)
        self.assertFalse(gate['passed']);self.assertTrue(gate['prior_native_win_reopenings']);self.assertFalse(gate['net_10000_point_progress'])
    def test_any_snapshot_source_regression_blocks_even_native_win(self):
        for verdict in ('inconclusive','slower'):
            rows,lp=self.rows();snapshots=[dict(decision,fixture='SharedSkenderRenkoChartNativeBenchmarks',method='Snapshot',verdict=verdict) for decision in lp]
            result=dict(accepted=verdict!='slower',case_count=44,decision_count=8,decisions=lp+snapshots)
            with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q,'native_rows',return_value=rows):got=q.collect(Path('unused'))
            self.assertEqual(got['accepted'],verdict!='slower');self.assertTrue(got['qualification']);self.assertFalse(got['report_imported'])
    def test_registry_and_dedicated_collector_route(self):
        plan=self.plan()
        with mock.patch.object(q,'validate_scope') as scope:targeted.verify_reference(plan);scope.assert_called_once()
        self.assertEqual(targeted.expected_totals(plan),(44,8))
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(q,'collect',return_value={'accepted':True}) as collect:
            self.assertTrue(verify.collect(Path('unused'),'targeted')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'diagnostic')
    def test_qualified_alias_cannot_execute_generic_variant_fallback(self):
        tree=ast.parse(inspect.getsource(pipeline._verify_input_contents))
        branch=next(node for node in tree.body[0].body if isinstance(node,ast.If) and ast.unparse(node.test)=="plan.get('renko_normalized_grid_qualification')")
        code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),pipeline.__file__,'exec')
        forbidden=mock.Mock(side_effect=AssertionError('generic variant fallback'))
        with mock.patch.object(q,'verify_harness_alias') as alias:exec(code,dict(__package__='hosted_timing',plan=self.plan(),folder=Path('unused'),proof={},verify_trx=forbidden));alias.assert_called_once()
        forbidden.assert_not_called()
    def test_workflow_preserves_one_runner_sdk_and_compatible_runtime(self):
        text=(q.p.REPO/'.github/workflows/renko-normalized-grid-qualification.yml').read_text()
        self.assertNotIn('strategy:',text);self.assertIn('--phase targeted',text)
        self.assertIn("$actualSdk -ne '10.0.403'",text);self.assertIn("$compatibleRuntimes[-1].ToString() -ne '10.0.15'",text)
if __name__=='__main__':unittest.main()
