"""Bounded Ultimate diagnostic orchestration checks; no production builds."""
import copy,json,subprocess,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import ultimate_wider_long_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-ultimate-wider-long-roi-20261118.json'

class UltimateWiderLongRoiTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self,winner=0):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for index,pair in enumerate(q.PAIRS):
            for bars in (1000,10000):
                win=index==winner and bars==10000
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sample(80 if win else 70) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if win else 'inconclusive'))
        return rows,decisions
    def test_actual_scope_source_boundary_and_lf_policy(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertNotIn(b'\r',PLAN.read_bytes())
        rel=PLAN.relative_to(q.p.REPO).as_posix()
        self.assertEqual(q.p.git('check-attr','text','eol','--',rel).splitlines(),[rel+': text: set',rel+': eol: lf'])
        self.assertEqual(q.PAIRS,['TaLib.Functions.UltOsc','Skender.GetUltimate'])
    def test_unbound_or_unreviewed_stops_before_sdk_and_artifact_io(self):
        for change in ({},{'inputs_ready':True}):
            plan=self.plan();plan.update(inputs_ready=False,parent_review_approved=False);plan.update(change)
            with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('I/O forbidden')) as calls,mock.patch.object(q,'checked_inputs') as inputs:
                with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-out')
                calls.assert_not_called();inputs.assert_not_called()
    def test_readiness_cannot_replace_actual_pins(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True)
        plan['candidate']['manifest_sha256']='UNBOUND'
        with self.assertRaises(AssertionError):q.validate_scope(plan)
    def test_policy_scope_and_classes_cannot_expand(self):
        mutations=[lambda p:p.update(cases=60),lambda p:p.update(decisions=12),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p.update(automatic_qualification=True),lambda p:p['sources'].update(variants=q.NEW),lambda p:p['candidate']['unit_classes'].pop(),lambda p:p['diagnostic_shards'][0]['tasks'][0]['operations'].pop(),lambda p:p.update(future_builder_fixture='Wrong'),lambda p:p.update(require_net_10000_point_progress=False)]
        for mutate in mutations:
            plan=self.plan();mutate(plan)
            with self.assertRaises((AssertionError,KeyError)):q.validate_scope(plan,require_ready=False)
    def test_shared_phase_and_registry_use_dedicated_scope(self):
        plan=self.plan()
        with mock.patch.object(q,'validate_scope') as check:
            roi_scope_dispatch.validate_scope(plan);check.assert_called_once()
            self.assertEqual(pipeline.phase_tasks(plan,'diagnostic',0),q.shards()[0]['tasks'])
        self.assertEqual(pipeline.phase_labels('diagnostic'),('old','successor'))
        self.assertEqual(pipeline.warmups_for(plan,'LibraryPairBenchmarks',q.shards()[0]['tasks'][0]),1000)
    def test_each_pair_can_fund_only_real_new_ten_k_win(self):
        for winner in range(2):
            rows,decisions=self.rows(winner);gate=q.adjudicate_feasibility(rows,decisions)
            self.assertTrue(gate['passed']);self.assertEqual(len(gate['new_10000_target_wins']),1)
            self.assertEqual(len(gate['all_pair_size_rows']),4)
            self.assertFalse(gate['qualification']);self.assertFalse(gate['automatic_qualification']);self.assertEqual(gate['closure_credit'],0)
    def test_both_new_rounds_require_margin_interval_and_source_faster(self):
        mutations=[lambda r,d:d[1].update(verdict='inconclusive')]
        for index in ('1','2'):
            mutations.extend([lambda r,d,i=index:r[1]['candidate_rounds'][i].update(upper_ns=99),lambda r,d,i=index:r[1]['candidate_rounds'][i].update(mean_ns=91,lower_ns=90,upper_ns=92)])
        for mutate in mutations:
            rows,decisions=self.rows();mutate(rows,decisions);self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_old_point_winner_cannot_be_claimed_new(self):
        for index in ('0','3'):
            rows,decisions=self.rows();rows[1]['old_rounds'][index].update(mean_ns=95,lower_ns=94,upper_ns=96)
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_any_source_regression_blocks(self):
        for index in range(4):
            rows,decisions=self.rows();decisions[index]['verdict']='slower'
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_prior_confident_wins_and_net_count_protected(self):
        rows,decisions=self.rows();rows[3]['candidate_rounds']['1'].update(mean_ns=105,lower_ns=104,upper_ns=106)
        gate=q.adjudicate_feasibility(rows,decisions)
        self.assertFalse(gate['passed']);self.assertTrue(gate['prior_native_win_reopenings']);self.assertFalse(gate['net_10000_point_progress'])
    def test_duplicate_missing_and_foreign_pairs_fail_closed(self):
        rows,decisions=self.rows()
        for broken in (rows[:-1],rows+[copy.deepcopy(rows[0])]):
            with self.assertRaises(AssertionError):q.adjudicate_feasibility(broken,decisions)
        rows[0]['pair']='foreign'
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows,decisions)
    def test_harness_roles_keep_actual_binary_identity_separate(self):
        plan=self.plan();plan['candidate']['actual_harness_sha256']='a'*64
        old=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['harness_sha256']}));new=dict(manifest=dict(runtime_files={q.HARNESS_PATH:'a'*64}))
        alias=q.harness_role_alias(plan,old,new)
        self.assertEqual(alias['common_harness_sha256'],plan['old_candidate']['actual_harness_sha256'])
        self.assertFalse(alias['actual_artifact_binary_identical']);self.assertTrue(alias['scoped_source_identical'])
    def test_collect_is_diagnostic_only_and_never_credits(self):
        plan=self.plan()
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(q,'collect',return_value={'accepted':False}) as collect:
            self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
        rows,decisions=self.rows();result=dict(accepted=False,case_count=20,decision_count=4,decisions=decisions)
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q,'native_rows',return_value=rows):
            got=q.collect(Path('unused'));self.assertTrue(got['feasibility_screen']['passed']);self.assertFalse(got['accepted']);self.assertEqual(got['closure_ledger_new_rows'],0)

if __name__=='__main__':unittest.main()
