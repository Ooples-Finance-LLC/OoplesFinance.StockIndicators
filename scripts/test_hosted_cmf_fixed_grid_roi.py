"""CMF scope routing and adversarial native-win screening; no production builds."""
import copy,json,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import cmf_fixed_grid_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-cmf-fixed-grid-roi-20261118.json'
class CmfFixedGridRoiTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for bars in (1000,10000):
            rows.append(dict(pair=q.PAIRS[0],bars=bars,old_rounds={k:sample(120) for k in ('0','3')},candidate_rounds={k:sample(80 if bars==10000 else 120) for k in ('1','2')},native_control=sample(100)))
            decisions.append(dict(fixture='LibraryPairBenchmarks',pair=q.PAIRS[0],bars=bars,method='Ooples',verdict='faster' if bars==10000 else 'inconclusive'))
        return rows,decisions
    def test_actual_frozen_scope_policy_and_source_boundary(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual((plan['cases'],plan['decisions']),(10,2));self.assertEqual(q.PAIRS,['Skender.GetCmf'])
        self.assertNotIn(b'\r',PLAN.read_bytes())
        self.assertEqual(pipeline.warmups_for(plan,'LibraryPairBenchmarks',q.shards()[0]['tasks'][0]),1000)
        self.assertEqual(plan['candidate']['unit_passed'],30);self.assertEqual(plan['candidate']['parity_passed'],9)
        self.assertNotEqual(plan['old_candidate']['actual_harness_sha256'],plan['candidate']['actual_harness_sha256'])
    def test_scope_cannot_change_policy_or_current_source_claim(self):
        for field,value in [('frozen_baseline_feasibility_only',False),('current_combined_source_qualified',True),('report_import_permitted',True),('followup_requires_current_published_baseline',False),('require_net_10000_point_progress',1),('cases',20)]:
            plan=self.plan();plan[field]=value
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def test_unreviewed_inputs_stop_before_sdk_or_artifact_io(self):
        plan=self.plan();plan.update(inputs_ready=False,parent_review_approved=False)
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('I/O forbidden')) as io,mock.patch.object(q,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('old','new','out')
            io.assert_not_called();inputs.assert_not_called()
    def test_new_native_win_requires_both_rounds_margin_intervals_and_source_faster(self):
        rows,decisions=self.rows();gate=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(gate['passed']);self.assertEqual(gate['old_10000_point_win_counts'],{'0':0,'3':0});self.assertEqual(gate['candidate_10000_point_win_counts'],{'1':1,'2':1})
        for mutate in [lambda r,d:r[1]['candidate_rounds']['1'].update(upper_ns=99),lambda r,d:r[1]['candidate_rounds']['2'].update(mean_ns=91,lower_ns=90,upper_ns=92),lambda r,d:d[1].update(verdict='inconclusive'),lambda r,d:d[0].update(verdict='slower')]:
            rows,decisions=self.rows();mutate(rows,decisions);self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_prior_point_winner_is_ineligible_and_prior_confident_wins_are_protected(self):
        for old_round in ('0','3'):
            rows,decisions=self.rows();rows[1]['old_rounds'][old_round].update(mean_ns=95)
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
        rows,decisions=self.rows();rows[0]['old_rounds']={k:dict(mean_ns=80,lower_ns=79,upper_ns=81) for k in ('0','3')}
        gate=q.adjudicate_feasibility(rows,decisions);self.assertFalse(gate['passed']);self.assertTrue(gate['prior_native_win_reopenings'])
    def test_duplicate_or_missing_evidence_fails_closed(self):
        rows,decisions=self.rows()
        for broken in (rows[:-1],rows+[copy.deepcopy(rows[0])]):
            with self.assertRaises(AssertionError):q.adjudicate_feasibility(broken,decisions)
    def test_registry_and_collector_route_diagnostic_only(self):
        with mock.patch.object(q,'validate_scope') as check:roi_scope_dispatch.validate_scope(self.plan());check.assert_called_once()
        with mock.patch.object(verify,'read',return_value=self.plan()),mock.patch.object(q,'collect',return_value={'accepted':False}) as collect:
            self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
    def test_favorable_screen_cannot_qualify_or_import_frozen_source(self):
        rows,decisions=self.rows();result=dict(accepted=False,case_count=10,decision_count=2,decisions=decisions)
        with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q,'native_rows',return_value=rows):
            got=q.collect(Path('unused'))
        self.assertTrue(got['feasibility_screen']['passed']);self.assertTrue(got['frozen_baseline_feasibility_only'])
        self.assertFalse(got['accepted']);self.assertFalse(got['qualification']);self.assertFalse(got['current_combined_source_qualified'])
        self.assertFalse(got['report_imported']);self.assertFalse(got['automatic_qualification']);self.assertEqual(got['closure_ledger_new_rows'],0)
if __name__=='__main__':unittest.main()
