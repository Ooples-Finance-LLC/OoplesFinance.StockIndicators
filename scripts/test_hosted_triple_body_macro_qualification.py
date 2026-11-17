import copy
import unittest
from pathlib import Path
from unittest import mock
from hosted_timing import triple_body_macro_qualification as q

PLAN = Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-triple-body-macro-20261117.json'

class MacroQualificationTests(unittest.TestCase):
    def plan(self): return q.p.read(PLAN)
    def test_unbound_scope_is_structurally_valid_and_not_executable(self):
        plan=self.plan();plan["inputs_ready"]=False;q.validate_scope(plan,require_ready=False)
        with self.assertRaises(AssertionError):q.validate_scope(plan)
        self.assertEqual((plan['cases'],plan['decisions']),(120,24))
        self.assertEqual(len(q.PAIRS),4)
    def test_prepare_stops_before_sdk_or_copy_for_unbound_plan(self):
        unbound=self.plan();unbound['inputs_ready']=False
        read_original=q.p.read
        def read_fixture(path):
            return unbound if Path(path).resolve()==PLAN.resolve() else read_original(path)
        with mock.patch.object(q.p,'PLAN',PLAN),mock.patch.object(q.p,'read',side_effect=read_fixture),mock.patch.object(q.subprocess,'check_output',side_effect=AssertionError('SDK must not run')) as sdk:
            with self.assertRaises(AssertionError):q.prepare('baseline','old','new','out')
            sdk.assert_not_called()
    def test_foreign_pair_or_policy_cannot_expand_scope(self):
        for key in ('pair','warmup'):
            plan=self.plan()
            if key=='pair':plan['targeted_shards'][0]['tasks'][0]['operations'][0]['pair']='TaLib.Candles.DarkCloudCover'
            else:plan['measurement_policy']['fixed_invocation_warmups']=20000
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def test_macro_cannot_enter_standard_collect_without_native_gate(self):
        from hosted_timing import verify
        with mock.patch.object(verify,'read',return_value=self.plan()):
            with self.assertRaises(AssertionError):verify._collect(Path('missing'),'targeted',preflight=False)
    def test_source_and_native_gates_are_both_required(self):
        from hosted_timing import verify
        for source_ok,winner,expected in [(True,q.PAIRS[0],False),(False,q.PAIRS[1],False),(True,q.PAIRS[1],True)]:
            rows,decisions=self.rows(winner)
            summary=dict(case_count=120,decision_count=24,accepted=source_ok,decisions=decisions)
            with mock.patch.object(verify,'_collect',return_value=summary) as standard,mock.patch.object(q,'native_rows',return_value=rows):
                self.assertEqual(q.collect('missing')['accepted'],expected)
                self.assertTrue(standard.call_args.kwargs['candle_predicate_gate'])
    def rows(self,winner):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                new=80 if pair==winner and bars==10000 else 110
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110) for k in ('0','3')},candidate_rounds={k:sample(new) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if new==80 else 'inconclusive'))
        return rows,decisions
    def test_three_inside_control_cannot_be_new_closure(self):
        rows,decisions=self.rows(q.PAIRS[0]);result=q.adjudicate_native(rows,decisions)
        self.assertFalse(result['passed']);self.assertEqual(result['new_10000_closures'],[])
    def test_real_target_closure_requires_original_margin_and_source_gate(self):
        rows,decisions=self.rows(q.PAIRS[1]);self.assertTrue(q.adjudicate_native(rows,decisions)['passed'])
        for row in rows:
            if row['pair']==q.PAIRS[1] and row['bars']==10000:
                for value in row['candidate_rounds'].values():value.update(mean_ns=95,lower_ns=94,upper_ns=96)
        self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_protective_control_prior_confident_win_cannot_reopen(self):
        rows,decisions=self.rows(q.PAIRS[1])
        for row in rows:
            if row['pair']==q.PAIRS[0] and row['bars']==10000:
                for value in row['old_rounds'].values():value.update(mean_ns=70,lower_ns=69,upper_ns=71)
        self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])

if __name__=='__main__':unittest.main()
