import json
from pathlib import Path
import unittest
from unittest.mock import patch

from hosted_timing import candle_topology_replay_roi as q

PLAN = q.p.REPO/'benchmarks/plans/hosted-candle-topology-replay-roi-20261119.json'


class CandleTopologyReplayScreenTests(unittest.TestCase):
    def plan(self): return json.loads(PLAN.read_bytes())

    def test_held_plan_and_exact_twenty_case_inventory(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        plan['inputs_ready']=False;plan['parent_review_approved']=False
        with self.assertRaisesRegex(AssertionError,'HELD'):q.validate_scope(plan)
        task=q.shards()[0]['tasks'][0]
        actual=[(index,arm,pair['raw'],bars,method)
                for index,arm in enumerate(plan['measurement_policy']['order'])
                for pair in task['operations'] for bars in (1000,10000)
                for method in q.p.methods_for(task,index)]
        self.assertEqual(20,len(actual));self.assertEqual(20,len(set(actual)))
        self.assertEqual(4,sum(row[-1]=='Competitor' for row in actual))
        self.assertEqual(set(q.PAIRS),{row[2] for row in actual})

    def test_mutated_scope_cost_policy_or_receipt_is_rejected(self):
        for field,value in [('cases',10),('decisions',2),('runtime_policy_overrides',{'jit':False}),
                            ('matrix',dict(shards=1,max_parallel=2,timeout_minutes_per_shard=120)),
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
        self.assertTrue(result['passed']);self.assertEqual(2,len(result['primary_native_crossovers']))
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

    def test_separated_stalled_1000_crossover_without_net_10000_progress_is_rejected(self):
        rows,decisions=self.samples()
        identical=next(row for row in rows if row['pair']==q.PAIRS[0] and row['bars']==10000)
        for sample in identical['candidate_rounds'].values():sample.update(mean_ns=10,lower_ns=9,upper_ns=11)
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertEqual([dict(pair=q.PAIRS[1],bars=1000)],result['primary_native_crossovers'])
        self.assertEqual({'0':0,'3':0},result['old_10000_point_win_counts'])
        self.assertEqual({'1':0,'2':0},result['candidate_10000_point_win_counts'])
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
        text=(q.p.REPO/'.github/workflows/candle-topology-replay-roi.yml').read_text()
        self.assertLess(text.index('HELD before SDK'),text.index('actions/setup-dotnet'))
        self.assertIn('max-parallel: 1',text);self.assertIn('shard: [0]',text)
        self.assertIn('Published baseline changed; no SDK/input I/O permitted',text)
        self.assertNotIn('--phase targeted',text)
        self.assertNotIn('workflow_dispatch:',text)


if __name__=='__main__':unittest.main()
