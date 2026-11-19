"""Focused held-screen and shared default-count regression checks; no builds/timing."""
import ast,copy,fnmatch,inspect,json,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import native_eligibility_eight_screen as q,pipeline,checkpoints,baseline_refresh_dispatch as dispatch,verify
PLAN=q.p.REPO/'benchmarks/plans/hosted-current-native-eight-screen-20261118.json'

class NativeEightScreenTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self):
        def sample(n):return dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[dict(pair=pair,bars=10000,owned_rounds={str(i):sample(110) for i in range(4)},native_control=sample(100)) for pair in q.PAIRS]
        decisions=[dict(pair=pair,bars=10000,fixture='LibraryPairBenchmarks',method='Ooples',verdict='inconclusive') for pair in q.PAIRS]
        return rows,decisions
    def case(self,count,method='Ooples'):
        return dict(FullName=f'LibraryPairBenchmarks.{method}(PairId: "{q.PAIRS[0]}", Count: {count})',Method=method,Statistics=dict(N=7))
    def test_held_scope_and_honest_reused_cohort(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual((plan['cases'],plan['decisions']),(40,8))
        self.assertEqual((plan['old_candidate']['unit_passed'],plan['old_candidate']['parity_passed']),(55,8))
        self.assertEqual(plan['old_candidate']['unit_classes'],q.baseline.BASE_UNIT)
        self.assertFalse(plan['reused_correctness_scope']['new_selected_pair_cohort'])
        held=copy.deepcopy(plan);held.update(inputs_ready=False,parent_review_approved=False)
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(held)
        self.assertNotIn(b'\r',PLAN.read_bytes())
    def test_scope_mutations_fail_closed(self):
        mutations=[lambda p:p.update(cases=80),lambda p:p['refresh_shards'][0]['tasks'][0].update(counts=[1000,10000]),
            lambda p:p['matrix'].update(max_parallel=2),lambda p:p['measurement_policy'].update(iterations=9),
            lambda p:p['old_candidate']['unit_classes'].append('DirectionalTests'),
            lambda p:p['reused_correctness_scope'].update(new_selected_pair_cohort=True),
            lambda p:p.update(profile_in_this_plan=True),lambda p:p['sources'].update(successor=q.baseline.NEW),
            lambda p:p['published_baseline'].update(head='0'*40)]
        for mutate in mutations:
            plan=self.plan();mutate(plan)
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def test_exact_registry_binding_rejects_alias_pending_and_wrong_indicator(self):
        rows=[dict(Id=pair,Status='paired',OoplesIndicator=indicator) for pair,indicator in zip(q.PAIRS,q.INDICATORS)]
        self.assertEqual(q.verify_registry_data(rows),rows)
        for field,value in (('Id','TaLib.Functions.Stddev'),('Status','pending'),('OoplesIndicator','Other')):
            changed=copy.deepcopy(rows);changed[6][field]=value
            with self.assertRaises(AssertionError):q.verify_registry_data(changed)
        with self.assertRaises(AssertionError):q.verify_registry_data(rows+[rows[0]])
    def test_ten_thousand_filters_match_both_orders_without_extra_sizes(self):
        patterns=checkpoints.filters('LibraryPairBenchmarks',q.PAIRS[0],['Ooples'],counts=[10000])
        for name in (f'LibraryPairBenchmarks.Ooples(PairId: "{q.PAIRS[0]}", Count: 10000)',f'LibraryPairBenchmarks.Ooples(Count: 10000, PairId: "{q.PAIRS[0]}")'):
            self.assertTrue(any(fnmatch.fnmatchcase(name,p) for p in patterns))
        for count in (1000,100000):
            self.assertFalse(any(fnmatch.fnmatchcase(self.case(count)['FullName'],p) for p in patterns))
        with self.assertRaises(AssertionError):checkpoints.filters('SharedPointwiseBenchmarks','X',['Batch'],counts=[10000])
    def test_default_two_size_merge_preserved_and_explicit_size_is_exact(self):
        pair=q.PAIRS[0];both=dict(Benchmarks=[self.case(1000),self.case(10000)])
        self.assertEqual(checkpoints.combine_reports([both],[pair],['Ooples']),both)
        one=dict(Benchmarks=[self.case(10000)])
        self.assertEqual(checkpoints.combine_reports([one],[pair],['Ooples'],counts=[10000]),one)
        with self.assertRaises(AssertionError):checkpoints.combine_reports([one],[pair],['Ooples'])
        with self.assertRaises(AssertionError):checkpoints.combine_reports([both],[pair],['Ooples'],counts=[10000])
        with self.assertRaises(AssertionError):checkpoints.combine_reports([dict(Benchmarks=[self.case(100000)])],[pair],['Ooples'],counts=[10000])
        bad=copy.deepcopy(one);bad['Benchmarks'][0]['Statistics']['N']=6
        with self.assertRaises(AssertionError):checkpoints.combine_reports([bad],[pair],['Ooples'],counts=[10000])
    def test_count_override_is_exclusive_and_bound_into_checkpoint_context(self):
        plan=self.plan();task=q.shards()[0]['tasks'][0]
        self.assertEqual(pipeline.benchmark_counts(plan,task),(10000,))
        self.assertEqual(pipeline.warmup_context(plan,'LibraryPairBenchmarks',task),dict(warmup_count=1000,benchmark_counts=[10000]))
        legacy=dict(measurement_policy=copy.deepcopy(plan['measurement_policy']))
        self.assertEqual(pipeline.benchmark_counts(legacy,dict(fixture='LibraryPairBenchmarks')),(1000,10000))
        self.assertEqual(pipeline.warmup_context(legacy,'LibraryPairBenchmarks',dict(fixture='LibraryPairBenchmarks')),dict(warmup_count=1000))
        with self.assertRaises(AssertionError):pipeline.benchmark_counts(legacy,task)
    def test_whole_runtime_identity_required(self):
        plan=self.plan();manifests=dict(old=dict(core='same',tensor='same'),new=dict(core='same',tensor='same'))
        with mock.patch.object(q,'validate_scope'):
            q.verify_runtime_identity(plan,manifests)
            manifests['new']['tensor']='different'
            with self.assertRaises(AssertionError):q.verify_runtime_identity(plan,manifests)
    def test_point_eligibility_and_aa_instability_are_separate(self):
        rows,decisions=self.rows();got=q.eligibility(rows,decisions)
        self.assertTrue(all(row['profile_candidate'] for row in got))
        rows[0]['owned_rounds']['0']=dict(mean_ns=99,lower_ns=98,upper_ns=100)
        got=q.eligibility(rows,decisions);self.assertFalse(got[0]['frozen_new_win_eligible']);self.assertFalse(got[0]['old_confident_native_win'])
        self.assertFalse(got[0]['profile_candidate'])
        rows[0]['owned_rounds']['0']=dict(mean_ns=100,lower_ns=99,upper_ns=101)
        self.assertTrue(q.eligibility(rows,decisions)[0]['frozen_new_win_eligible'])
        for verdict in ('faster','slower'):
            decisions[1]['verdict']=verdict;row=q.eligibility(rows,decisions)[1]
            self.assertTrue(row['frozen_new_win_eligible']);self.assertFalse(row['aa_admitted_for_triage']);self.assertFalse(row['profile_candidate'])
    def test_interval_contact_is_not_a_native_interval_win(self):
        rows,decisions=self.rows()
        for sample in rows[0]['owned_rounds'].values():sample.update(mean_ns=98,lower_ns=97,upper_ns=99)
        self.assertFalse(q.eligibility(rows,decisions)[0]['old_confident_native_win'])
        for sample in rows[0]['owned_rounds'].values():sample.update(upper_ns=98.5)
        self.assertTrue(q.eligibility(rows,decisions)[0]['old_confident_native_win'])
    def test_diagnostic_collect_never_qualifies_or_starts_profiles(self):
        rows,decisions=self.rows();result=dict(accepted=False,case_count=40,decision_count=8,decisions=decisions)
        with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q,'validate_scope'),mock.patch.object(q.baseline.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result) as collect,mock.patch.object(q,'native_rows',return_value=rows):
            got=q.collect(Path('unused'))
        collect.assert_called_once_with(Path('unused'),'baseline-refresh',preflight=False)
        self.assertFalse(got['accepted']);self.assertFalse(got['qualification']);self.assertFalse(got['report_imported'])
        self.assertEqual(got['closure_credit'],0);self.assertFalse(got['automatic_profile_started']);self.assertEqual(got['profile_candidates'],q.PAIRS)
    def test_diagnostic_guard_admits_only_the_new_baseline_route(self):
        plan=self.plan()
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(dispatch,'validate_scope'),mock.patch.object(verify,'phase_shards',side_effect=RuntimeError('reached shard contract')):
            with self.assertRaisesRegex(RuntimeError,'reached shard contract'):verify._collect(Path('unused'),'baseline-refresh',preflight=False)
            with self.assertRaises(AssertionError):verify._collect(Path('unused'),'targeted',preflight=False)
            plan.pop('native_eligibility_eight_screen')
            with self.assertRaisesRegex(AssertionError,'Diagnostic evidence cannot qualify production'):verify._collect(Path('unused'),'baseline-refresh',preflight=False)
    def test_registry_dispatch_is_exclusive(self):
        plan=self.plan();self.assertIs(dispatch.implementation(plan),q)
        plan['current_baseline_ten_refresh']=True
        with self.assertRaises(AssertionError):dispatch.implementation(plan)
    def test_alias_branch_cannot_reach_generic_variant_fallback(self):
        tree=ast.parse(inspect.getsource(pipeline._verify_input_contents))
        branch=next(node for node in ast.walk(tree) if isinstance(node,ast.If) and ast.unparse(node.test)=="plan.get('native_eligibility_eight_screen')")
        code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),pipeline.__file__,'exec')
        forbidden=mock.Mock(side_effect=AssertionError('generic fallback'))
        with mock.patch.object(q,'verify_harness_alias') as alias:exec(code,dict(__package__='hosted_timing',plan=self.plan(),folder=Path('unused'),proof={},verify_trx=forbidden))
        alias.assert_called_once();forbidden.assert_not_called()
    def test_held_prepare_stops_before_current_api_sdk_or_artifacts(self):
        plan=self.plan();plan.update(inputs_ready=False,parent_review_approved=False)
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.baseline,'verify_current_publication') as current,mock.patch.object(q.baseline,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('unused','unused-out')
        current.assert_not_called();inputs.assert_not_called()
    def test_workflow_is_narrow_single_runner_without_build_or_profile(self):
        text=(q.p.REPO/'.github/workflows/current-native-eight-screen.yml').read_text()
        self.assertIn('workflow_dispatch:',text);self.assertIn('push:',text);self.assertNotIn('strategy:',text)
        self.assertIn('branches: [perf/current-native-eight-screen-20261118]',text)
        self.assertIn('paths: [.github/workflows/current-native-eight-screen.yml]',text)
        self.assertNotIn('dotnet build',text);self.assertNotIn('dotnet run',text);self.assertNotIn('dotnet-trace',text)
        self.assertLess(text.index('Fail closed before SDK'),text.index('actions/setup-dotnet'))
        self.assertEqual(text.count('GH_TOKEN: ${{ github.token }}'),2)
if __name__=='__main__':unittest.main()
