"""Focused full-return qualification scope and adversarial native gates; no builds."""
import ast,copy,inspect,json,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import renko_constant_tr_private_qualification as q
from hosted_timing import pipeline,targeted,verify
PLAN=q.p.REPO/'benchmarks/plans/hosted-renko-constant-tr-private-qualification-20261118.json'
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
        self.assertEqual((plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed']),(55,8))
        self.assertEqual((plan['candidate']['unit_passed'],plan['candidate']['parity_passed']),(84,8))
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
    def test_native_win_requires_both_rounds_intervals_and_own_faster(self):
        rows,decisions=self.rows();self.assertTrue(q.adjudicate_feasibility(rows,decisions)['passed'])
        for mutate in [lambda r,d:r[1]['candidate_rounds']['2'].update(upper_ns=100),lambda r,d:d[1].update(verdict='inconclusive'),lambda r,d:r[1]['old_rounds']['0'].update(mean_ns=95)]:
            rows,decisions=self.rows();mutate(rows,decisions)
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_separated_five_percent_native_win_is_accepted(self):
        rows,decisions=self.rows()
        for value in rows[1]['candidate_rounds'].values():value.update(mean_ns=95,lower_ns=94,upper_ns=96)
        gate=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(gate['passed']);self.assertTrue(gate['at_least_one_new_10000_target_win'])
        self.assertIsNone(gate['minimum_native_margin'])
        self.assertEqual(gate['native_policy_id'],q.POLICY_APPROVAL['policy_id'])
    def test_touching_or_overlapping_native_interval_in_either_round_rejects(self):
        for round_index in ('1','2'):
            for upper in (99,100):
                with self.subTest(round=round_index,upper=upper):
                    rows,decisions=self.rows()
                    rows[1]['candidate_rounds'][round_index].update(upper_ns=upper)
                    self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_approved_policy_binding_bytes_and_margin_fail_closed(self):
        plan=self.plan();q.verify_native_policy(plan)
        changed=copy.deepcopy(plan);changed['native_win_policy_approval']['sha256']='0'*64
        with self.assertRaises(AssertionError):q.verify_native_policy(changed)
        raw=(q.p.REPO/q.POLICY_APPROVAL['path']).read_bytes()
        with mock.patch.object(Path,'read_bytes',return_value=raw+b' '):
            with self.assertRaises(AssertionError):q.verify_native_policy(plan)
        changed=copy.deepcopy(plan);changed['native_progress_gate']['minimum_margin']=.10
        with self.assertRaises(AssertionError):q.validate_scope(changed,require_ready=False)
    def test_current_published_pr_head_and_source_binding_fail_closed(self):
        plan=self.plan();binding=plan['published_baseline']
        def check(actual_head=None,actual_ref=None,fingerprint=None,diff=''):
            actual=dict(head=dict(sha=actual_head or binding['head'],ref=actual_ref or binding['branch']))
            with mock.patch.object(q.subprocess,'check_output',return_value=json.dumps(actual).encode()) as api,mock.patch.object(q.common,'source_fingerprint',return_value=fingerprint or binding['source_fingerprint']),mock.patch.object(q.p,'git',return_value=diff):
                self.assertEqual(q.verify_current_publication(plan),binding)
                api.assert_called_once_with(['gh','api','repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pulls/266'],cwd=q.p.REPO)
        check()
        for changes in (dict(actual_head='0'*40),dict(actual_ref='different-branch'),dict(fingerprint='0'*64),dict(diff='src/Indicators/RenkoSnapshot.cs')):
            with self.subTest(changes=changes):
                with self.assertRaises(AssertionError):check(**changes)
    def test_changed_current_head_blocks_preparation_before_artifact_io(self):
        plan=self.plan();binding=plan['published_baseline']
        actual=dict(head=dict(sha=q.NEW,ref=binding['branch']))
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q,'validate_scope'),mock.patch.object(q.common,'source_fingerprint',return_value=binding['source_fingerprint']),mock.patch.object(q.p,'git',return_value=''),mock.patch.object(q.subprocess,'check_output',return_value=json.dumps(actual).encode()) as api,mock.patch.object(q,'checked_inputs') as artifacts:
            with self.assertRaisesRegex(AssertionError,'Published PR head changed'):q.prepare('unused-old','unused-new','unused-out')
            api.assert_called_once_with(['gh','api','repos/Ooples-Finance-LLC/OoplesFinance.StockIndicators/pulls/266'],cwd=q.p.REPO)
            artifacts.assert_not_called()
    def test_historical_verification_uses_captured_binding_without_live_api(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True)
        captured=dict(published_baseline=copy.deepcopy(plan['published_baseline']))
        original_output=q.subprocess.check_output;queries=[]
        def advanced_publication(args,*positional,**keywords):
            if args[:2]==['gh','api']:
                queries.append(args);return json.dumps(dict(head=dict(sha=q.NEW,ref=plan['published_baseline']['branch']))).encode()
            return original_output(args,*positional,**keywords)
        with mock.patch.object(q.subprocess,'check_output',side_effect=advanced_publication):
            q.validate_scope(plan)
            q.verify_publication_provenance(plan,captured)
            captured['published_baseline']['head']=q.NEW
            with self.assertRaisesRegex(AssertionError,'Captured publication binding mismatch'):q.verify_publication_provenance(plan,captured)
        self.assertEqual(queries,[])
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
        branch=next(node for node in ast.walk(tree.body[0]) if isinstance(node,ast.If) and ast.unparse(node.test)=="plan.get('renko_constant_tr_private_qualification')")
        code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),pipeline.__file__,'exec')
        forbidden=mock.Mock(side_effect=AssertionError('generic variant fallback'))
        with mock.patch.object(q,'verify_harness_alias') as alias:exec(code,dict(__package__='hosted_timing',plan=self.plan(),folder=Path('unused'),proof={},verify_trx=forbidden));alias.assert_called_once()
        forbidden.assert_not_called()
    def test_workflow_preserves_one_runner_sdk_and_compatible_runtime(self):
        text=(q.p.REPO/'.github/workflows/renko-constant-tr-private-qualification.yml').read_text()
        self.assertNotIn('strategy:',text);self.assertIn('--phase targeted',text)
        self.assertIn('q.verify_current_publication(plan)',text)
        self.assertEqual(text.count('GH_TOKEN: ${{ github.token }}'),2)
        self.assertIn("$actualSdk -ne '10.0.403'",text);self.assertIn("$compatibleRuntimes[-1].ToString() -ne '10.0.15'",text)
if __name__=='__main__':unittest.main()
