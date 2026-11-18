"""Scope and adversarial native-progress checks; no builds or measurements."""
import ast,copy,inspect,json,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import cmf_alma_combined_qualification as q
from hosted_timing import pipeline,targeted,verify
PLAN=q.p.REPO/'benchmarks/plans/hosted-cmf-alma-combined-qualification-20261118.json'
class CombinedQualificationTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                win=pair==q.TARGETS[0] and bars==10000
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sample(80 if win else 70) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if win else 'inconclusive'))
        return rows,decisions
    def test_scope_and_unbound_readiness(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual(sum(t['cases'] for t in q.shards()[0]['tasks']),100)
        self.assertEqual(sum(t['decisions'] for t in q.shards()[0]['tasks']),20)
        self.assertNotIn(b'\r',PLAN.read_bytes())
        unbound=copy.deepcopy(plan)
        unbound.update(inputs_ready=False,parent_review_approved=False)
        unbound['candidate']['actual_harness_sha256']='UNBOUND'
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(unbound)
        unbound.update(inputs_ready=True,parent_review_approved=True)
        with self.assertRaises(AssertionError):q.validate_scope(unbound)
    def test_scope_policy_and_receipt_classes_fail_closed(self):
        for mutate in [lambda p:p.update(cases=90),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p['candidate']['unit_classes'].pop(),lambda p:p['native_progress_gate'].update(minimum_margin=.01),lambda p:p['protected_ultimate_qualification'].update(pool_prior_samples=True)]:
            plan=self.plan();mutate(plan)
            with self.assertRaises((AssertionError,KeyError)):q.validate_scope(plan,require_ready=False)
    def test_real_target_progress_and_control_excluded(self):
        rows,decisions=self.rows();gate=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(gate['passed']);self.assertEqual(gate['new_10000_target_wins'],[dict(pair=q.TARGETS[0],bars=10000,source_verdict='faster')])
        self.assertEqual(gate['candidate_10000_point_win_counts'],{'1':3,'2':3})
        for d in decisions:d['verdict']='inconclusive'
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_prior_control_requires_fresh_preservation_at_both_sizes(self):
        for bars in (1000,10000):
            rows,decisions=self.rows();control=next(r for r in rows if r['pair']==q.CONTROL and r['bars']==bars)
            # Even if today's baseline is slow, previously qualified wins stay protected.
            for value in control['old_rounds'].values():value.update(mean_ns=110,lower_ns=109,upper_ns=111)
            control['candidate_rounds']['1'].update(mean_ns=101,lower_ns=100,upper_ns=102)
            gate=q.adjudicate_feasibility(rows,decisions);self.assertFalse(gate['passed']);self.assertFalse(gate['protected_ultimate_native_wins_preserved'])
    def test_margin_intervals_own_source_and_prior_eligibility(self):
        for mutate in [lambda r,d:r[1]['candidate_rounds']['1'].update(mean_ns=91),lambda r,d:r[1]['candidate_rounds']['2'].update(upper_ns=100),lambda r,d:d[1].update(verdict='inconclusive'),lambda r,d:r[1]['old_rounds']['0'].update(mean_ns=95),lambda r,d:d[0].update(verdict='slower')]:
            rows,decisions=self.rows();mutate(rows,decisions);self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_duplicate_or_foreign_rows_rejected(self):
        rows,decisions=self.rows()
        for broken in [rows[:-1],rows+[copy.deepcopy(rows[0])]]:
            with self.assertRaises(AssertionError):q.adjudicate_feasibility(broken,decisions)
    def test_registry_routes_to_dedicated_gate(self):
        plan=self.plan()
        with mock.patch.object(q,'validate_scope') as check:targeted.verify_reference(plan);check.assert_called_once()
        self.assertEqual(targeted.expected_totals(plan),(100,20))
        self.assertEqual(pipeline.phase_tasks(plan,'targeted',0),q.shards()[0]['tasks'])
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(q,'collect',return_value={'accepted':True}) as collect:
            self.assertTrue(verify.collect(Path('unused'),'targeted')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'diagnostic')
    def test_workflow_keeps_sdk_runtime_and_single_runner(self):
        text=(q.p.REPO/'.github/workflows/cmf-alma-combined-qualification.yml').read_text()
        self.assertIn('10.0.403',text);self.assertIn("$compatibleRuntimes[-1].ToString() -ne '10.0.15'",text)
        self.assertNotIn('strategy:',text)
        self.assertLess(text.index('Fail closed before SDK'),text.index('actions/setup-dotnet'))
    def test_combined_alias_cannot_fall_through_to_generic_variant_harness(self):
        tree=ast.parse(inspect.getsource(pipeline._verify_input_contents))
        branch=next(node for node in tree.body[0].body if isinstance(node,ast.If)
            and ast.unparse(node.test)=="plan.get('cmf_alma_combined_qualification')")
        code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),
            pipeline.__file__,'exec')
        forbidden=mock.Mock(side_effect=AssertionError('generic variant-harness fallback executed'))
        with mock.patch.object(q,'verify_harness_alias') as alias:
            exec(code,dict(__package__='hosted_timing',plan=self.plan(),folder=Path('unused'),proof={},verify_trx=forbidden))
            alias.assert_called_once()
        forbidden.assert_not_called()
if __name__=='__main__':unittest.main()
