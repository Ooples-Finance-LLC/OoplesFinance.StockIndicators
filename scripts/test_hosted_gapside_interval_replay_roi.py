import json
from pathlib import Path
import unittest
from unittest.mock import patch

from hosted_timing import gapside_interval_replay_roi as q

PLAN = q.p.REPO/'benchmarks/plans/hosted-gapside-interval-replay-roi-20261119.json'


class GapSideIntervalReplayScreenTests(unittest.TestCase):
    def plan(self): return json.loads(PLAN.read_bytes())

    def test_held_plan_and_exact_ten_case_inventory(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        plan['inputs_ready']=False;plan['parent_review_approved']=False
        with self.assertRaisesRegex(AssertionError,'HELD'):q.validate_scope(plan)
        task=q.shards()[0]['tasks'][0]
        actual=[(index,arm,pair['raw'],bars,method)
                for index,arm in enumerate(plan['measurement_policy']['order'])
                for pair in task['operations'] for bars in (1000,10000)
                for method in q.p.methods_for(task,index)]
        self.assertEqual(10,len(actual));self.assertEqual(10,len(set(actual)))
        self.assertEqual(2,sum(row[-1]=='Competitor' for row in actual))
        self.assertEqual(set(q.PAIRS),{row[2] for row in actual})

    def test_mutated_scope_cost_policy_or_receipt_is_rejected(self):
        for field,value in [('cases',20),('decisions',4),('runtime_policy_overrides',{'jit':False}),
                            ('matrix',dict(shards=1,max_parallel=2,timeout_minutes_per_shard=60)),
                            ('closure_ledger_new_rows',1),('report_import_permitted',True)]:
            with self.subTest(field=field):
                plan=self.plan();plan[field]=value
                with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
        plan=self.plan();plan['candidate']['production_sha256']='0'*64
        with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)

    def test_changed_published_baseline_holds_before_preparation(self):
        with patch.object(q.subprocess,'check_output',return_value='0'*40+'\trefs/heads/perf/remaining-indicator-rollout\n'):
            with self.assertRaisesRegex(AssertionError,'Published baseline changed'):q.verify_current_publication(self.plan())

    @staticmethod
    def samples(primary=True):
        def sample(mean,lo,hi):return dict(mean_ns=mean,lower_ns=lo,upper_ns=hi,samples=10,allocated_bytes=10)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                target=(pair,bars) in q.PRIMARY
                rows.append(dict(pair=pair,bars=bars,old_rounds={str(i):sample(12,11,13) for i in (0,3)},
                    candidate_rounds={str(i):sample(7 if (target==primary) else 10,6 if (target==primary) else 9,8 if (target==primary) else 11) for i in (1,2)},
                    native_control=sample(10,9,11)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',pair=pair,bars=bars,method='Ooples',verdict='faster'))
        return rows,decisions

    def test_primary_crossover_requires_both_separated_rounds(self):
        rows,decisions=self.samples();result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['passed']);self.assertEqual(1,len(result['primary_native_crossovers']))
        for row in rows:
            if (row['pair'],row['bars']) in q.PRIMARY:row['candidate_rounds']['2']['upper_ns']=10
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_context_wins_or_ineligible_primary_cannot_fund_qualification(self):
        rows,decisions=self.samples(primary=False)
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
        rows,decisions=self.samples()
        for row in rows:
            for sample in row['old_rounds'].values():sample.update(mean_ns=8,lower_ns=7,upper_ns=9)
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertFalse(result['passed']);self.assertFalse(result['qualification']);self.assertEqual(0,result['closure_credit'])

    def test_no_ten_thousand_crossover_cannot_pass_on_a_context_win(self):
        rows,decisions=self.samples(primary=False);result=q.adjudicate_feasibility(rows,decisions)
        self.assertFalse(result['primary_native_crossovers'])
        self.assertFalse(result['net_10000_point_progress']);self.assertFalse(result['passed'])

    def test_prior_confident_win_reopening_is_rejected(self):
        rows,decisions=self.samples()
        context=next(row for row in rows if row['pair']==q.PAIRS[0] and row['bars']==1000)
        for sample in context['old_rounds'].values():sample.update(mean_ns=7,lower_ns=6,upper_ns=8)
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['net_10000_point_progress'])
        self.assertEqual([dict(pair=q.PAIRS[0],bars=1000)],result['protected_reopenings'])
        self.assertFalse(result['passed'])

    def test_source_regression_is_rejected_despite_primary_crossovers_and_net_progress(self):
        rows,decisions=self.samples();decisions[0]['verdict']='slower'
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['net_10000_point_progress']);self.assertTrue(result['primary_native_crossovers'])
        self.assertFalse(result['passed'])

    def test_pre_sdk_hold_and_single_measurement_vm(self):
        text=(q.p.REPO/'.github/workflows/gapside-interval-replay-roi.yml').read_text()
        self.assertLess(text.index('HELD before SDK'),text.index('actions/setup-dotnet'))
        self.assertIn('max-parallel: 1',text);self.assertIn('shard: [0]',text)
        self.assertIn('Published baseline changed; no SDK/input I/O permitted',text)
        self.assertNotIn('--phase targeted',text)
        self.assertNotIn('workflow_dispatch:',text)


    def test_ready_scope_and_wrong_kind_shard_totals_fail_closed(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True);q.validate_scope(plan)
        for field,value in [('diagnostic_kind','wrong-kind'),('diagnostic_shards',[]),('input_pin_status','pending'),('preparation_revision','wrong')]:
            bad=self.plan();bad[field]=value
            with self.subTest(field=field),self.assertRaises(AssertionError):q.validate_scope(bad,require_ready=False)

    def test_held_prepare_stops_before_sdk_or_inputs(self):
        held=self.plan();held.update(inputs_ready=False,parent_review_approved=False)
        with patch.object(q.p,'read',return_value=held),patch.object(q.subprocess,'check_output') as launch:
            with self.assertRaisesRegex(AssertionError,'HELD'):q.prepare('missing-old','missing-candidate','unused')
            launch.assert_not_called()

    def test_actual_internal_alias_and_auth_routes_avoid_legacy_variant_harness(self):
        import test_current_frontier_preparation as preparation
        from hosted_timing import current_frontier_refresh as frontier
        class ReachedRoute(Exception): pass
        fixture=preparation.CurrentFrontierPreparationTests()
        with fixture.fixture() as (root,candidate,plan):
            out=root/'prepared';proof=frontier.prepare(candidate,out)
            plan['current_frontier_refresh']=False;plan['diagnostic_kind']=q.KIND
            q.p.write(q.p.PLAN,plan);proof['plan_sha256']=q.p.digest(q.p.PLAN);q.p.write(out/'provenance.json',proof)
            with patch.object(q,'verify_harness_alias',side_effect=ReachedRoute) as alias:
                with self.assertRaises(ReachedRoute):q.p.verify_inputs(out)
                alias.assert_called_once()
            with patch.object(q,'verify_harness_alias'),patch.object(q,'verify_authentication',side_effect=ReachedRoute) as auth:
                with self.assertRaises(ReachedRoute):q.p.verify_inputs(out)
                auth.assert_called_once()

    def test_actual_scope_and_collect_dispatch_use_new_module(self):
        from hosted_timing import roi_scope_dispatch,verify
        plan=self.plan()
        with patch.object(q,'validate_scope') as check:roi_scope_dispatch.validate_scope(plan)
        check.assert_called_once_with(plan)
        with patch.object(verify,'PLAN',PLAN),patch.object(q,'collect',return_value='route-ok') as collect:
            self.assertEqual('route-ok',verify.collect(Path('unused'),'diagnostic'));collect.assert_called_once()

    def test_actual_cli_parser_prepare_hold_and_measure_collect_dispatch(self):
        import os,sys,subprocess,tempfile
        from hosted_timing import verify
        with tempfile.TemporaryDirectory() as temporary:
            root=Path(temporary);prepared=root/'prepared';prepared.mkdir();(prepared/'provenance.json').write_bytes(b'UNIT CLI proof')
            held=self.plan();held.update(inputs_ready=False,parent_review_approved=False)
            held_path=root/'held.json';held_path.write_text(json.dumps(held))
            env=dict(os.environ,HOSTED_TIMING_PLAN=str(held_path))
            for mode in ('measure','collect'):
                result=subprocess.run([sys.executable,str(q.p.REPO/'scripts/run-hosted-timing.py'),mode,'--phase','diagnostic','--out',str(root/'out'),'--help'],env=env,text=True,capture_output=True)
                self.assertEqual(0,result.returncode,result.stderr)
            result=subprocess.run([sys.executable,str(q.p.REPO/'scripts/prepare-gapside-interval-replay-roi.py'),'--old-candidate','missing','--candidate','missing','--out',str(root/'never')],env=env,text=True,capture_output=True)
            self.assertNotEqual(0,result.returncode);self.assertIn('HELD',result.stderr);self.assertFalse((root/'never').exists())
            argv=['run-hosted-timing.py','measure','--inputs',str(prepared),'--out',str(root/'raw'),'--phase','diagnostic','--shard','0']
            with patch.object(sys,'argv',argv),patch.object(q.p,'measure') as measure:q.p.main()
            measure.assert_called_once_with(prepared.resolve(),(root/'raw').resolve(),'diagnostic',0,None)
            summary=dict(case_count=10,decision_count=2,accepted=False,verdicts={},input_provenance_sha256=q.p.digest(prepared/'provenance.json'))
            argv=['run-hosted-timing.py','collect','--inputs',str(root/'raw'),'--prepared',str(prepared),'--out',str(root/'summary.json'),'--phase','diagnostic']
            with patch.object(sys,'argv',argv),patch.object(q.p,'verify_inputs'),patch.object(verify,'PLAN',PLAN),patch.object(q,'collect',return_value=summary),patch.dict(os.environ,GITHUB_OUTPUT=''):q.p.main()
            self.assertEqual(summary,q.p.read(root/'summary.json'))


if __name__=='__main__':unittest.main()
