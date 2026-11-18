"""Diagnostic boundaries, real routing, and adversarial promotion cases; no production builds."""
import copy,json,tempfile,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import candle_isolated_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-candle-isolated-roi-20261118.json'
class CandleIsolatedRoiTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_text())
    def test_actual_source_boundary_and_diagnostic_tasks(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        real_validate=q.validate_scope
        with mock.patch.object(q,'validate_scope',side_effect=lambda p:real_validate(p,require_ready=False)):
            self.assertEqual(pipeline.phase_tasks(plan,'diagnostic',0),q.shards()[0]['tasks'])
        self.assertEqual(pipeline.phase_labels('diagnostic'),('old','successor'))
        self.assertEqual(pipeline.warmups_for(plan,'LibraryPairBenchmarks',q.shards()[0]['tasks'][0]),1000)
        self.assertEqual(pipeline.cpu_test_classes(plan,'successor'),q.UNIT)
    def test_unbound_preparation_stops_before_sdk_and_artifact_io(self):
        with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('I/O forbidden')) as calls,mock.patch.object(q,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-out')
            calls.assert_not_called();inputs.assert_not_called()
    def test_parent_review_cannot_be_replaced_by_readiness(self):
        plan=self.plan();plan['inputs_ready']=True
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(plan)
    def test_receipt_pins_fail_closed_even_with_booleans_true(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True)
        with self.assertRaises(AssertionError):q.validate_scope(plan)
    def test_no_silent_policy_scope_or_role_expansion(self):
        for mutate in [lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['sources'].update(variants=q.NEW),lambda p:p.update(diagnostic_only=False),lambda p:p.update(cases=40),lambda p:p['candidate']['unit_classes'].pop(),lambda p:p['diagnostic_shards'][0]['tasks'][0]['operations'].pop()]:
            plan=self.plan();mutate(plan)
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def test_wrong_source_delta_or_benchmark_change_rejected(self):
        with mock.patch.object(q.p,'git',return_value='src/extra.cs'):
            with self.assertRaises(AssertionError):q.verify_source_boundary()
        with mock.patch.object(q.p,'git',side_effect=['\n'.join(q.PATHS),'changed-harness.cs']):
            with self.assertRaises(AssertionError):q.verify_source_boundary()
    def test_registry_dispatches_own_validator(self):
        with mock.patch.object(q,'validate_scope') as check:
            roi_scope_dispatch.validate_scope(self.plan());check.assert_called_once()
    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                new=80 if pair in q.TARGETS and bars==10000 else 110
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110) for k in ('0','3')},candidate_rounds={k:sample(new) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if new==80 else 'inconclusive'))
        return rows,decisions
    def test_both_native_targets_and_ladder_protection_required(self):
        rows,decisions=self.rows();gate=q.adjudicate_promotion(rows,decisions)
        self.assertTrue(gate['passed']);self.assertEqual(gate['closure_credit'],0);self.assertFalse(gate['qualification_authorized'])
        for pair in q.TARGETS:
            broken=copy.deepcopy(rows)
            next(r for r in broken if r['pair']==pair and r['bars']==10000)['candidate_rounds']['1']['upper_ns']=101
            self.assertFalse(q.adjudicate_promotion(broken,decisions)['passed'])
        for bars in (1000,10000):
            broken=copy.deepcopy(decisions);next(d for d in broken if d['pair']==q.PAIRS[2] and d['bars']==bars)['verdict']='slower'
            self.assertFalse(q.adjudicate_promotion(rows,broken)['passed'])
    def test_any_of_six_source_regressions_blocks_full_qualification(self):
        rows,decisions=self.rows()
        for i in range(6):
            broken=copy.deepcopy(decisions);broken[i]['verdict']='slower'
            gate=q.adjudicate_promotion(rows,broken)
            self.assertFalse(gate['passed']);self.assertFalse(gate['all_six_source_comparisons_no_regression'])
    def test_distinct_actual_harnesses_are_preserved_under_explicit_baseline_alias(self):
        plan=self.plan();plan['harness_sha256']=plan['old_candidate']['actual_harness_sha256']
        actual_new='a794b0bce71ea59bcbe08242122ad3721d61bfd8689e4bb3e2cc14e40740cb64'
        plan['candidate']['actual_harness_sha256']=actual_new
        old=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['harness_sha256']}))
        new=dict(manifest=dict(runtime_files={q.HARNESS_PATH:actual_new}))
        alias=q.harness_role_alias(plan,old,new)
        self.assertEqual(alias['common_artifact_role'],'old')
        self.assertEqual(alias['common_harness_sha256'],plan['harness_sha256'])
        self.assertEqual(alias['actual_artifact_harness_sha256'],dict(old=plan['harness_sha256'],successor=actual_new))
        self.assertEqual(alias['actual_artifact_manifest_sha256'],dict(old=plan['old_candidate']['manifest_sha256'],successor=plan['candidate']['manifest_sha256']))
        self.assertFalse(alias['actual_artifact_binary_identical']);self.assertTrue(alias['scoped_source_identical'])
        # A different actual candidate artifact must not be relabeled to its plan pin.
        new['manifest']['runtime_files'][q.HARNESS_PATH]='f'*64
        with self.assertRaises(AssertionError):q.harness_role_alias(plan,old,new)
    def test_candidate_harness_cannot_become_common_by_changing_only_selected_pin(self):
        plan=self.plan();plan['harness_sha256']='a'*64;plan['candidate']['actual_harness_sha256']='a'*64
        old=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['old_candidate']['actual_harness_sha256']}))
        new=dict(manifest=dict(runtime_files={q.HARNESS_PATH:'a'*64}))
        with self.assertRaises(AssertionError):q.harness_role_alias(plan,old,new)
    def test_mean_margin_and_own_source_improvement_are_required(self):
        rows,decisions=self.rows();rows[1]['candidate_rounds']['1'].update(mean_ns=91,upper_ns=92,lower_ns=90)
        self.assertFalse(q.adjudicate_promotion(rows,decisions)['passed'])
        rows,decisions=self.rows();decisions[1]['verdict']='inconclusive'
        self.assertFalse(q.adjudicate_promotion(rows,decisions)['passed'])
    def test_duplicate_or_missing_rows_rejected(self):
        rows,decisions=self.rows()
        with self.assertRaises(AssertionError):q.adjudicate_promotion(rows[:-1],decisions)
        decisions[-1]=copy.deepcopy(decisions[0])
        with self.assertRaises(AssertionError):q.adjudicate_promotion(rows,decisions)
    def test_collect_routing_cannot_qualify_source(self):
        with mock.patch.object(verify,'read',return_value=self.plan()),mock.patch.object(q,'collect',return_value={'accepted':False}) as collect:
            self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
    def test_diagnostic_collector_retains_no_credit_flags(self):
        rows,decisions=self.rows();result=dict(accepted=False,case_count=30,decision_count=6,decisions=decisions)
        with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q,'native_rows',return_value=rows):
            got=q.collect(Path('unused'));self.assertFalse(got['accepted']);self.assertFalse(got['qualification']);self.assertEqual(got['closure_ledger_new_rows'],0);self.assertTrue(got['promotion_gate']['passed'])
    def test_alias_cannot_substitute_candidate_receipts(self):
        plan=self.plan()
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp);(folder/'evidence').mkdir()
            for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
                for role in ('old','variants'):(folder/'evidence'/(role+suffix)).write_bytes(b'baseline')
            proof=dict(source_authentication=q.source_authentication(plan),production_sha256=dict(old='a',variants='a'),checks=dict(old={'passed':1},variants={'passed':1}),tested_runtime_dependencies=dict(old={'v':1},variants={'v':1}))
            q.verify_harness_alias(folder,plan,proof)
            (folder/'evidence/variants.trx').write_bytes(b'candidate')
            with self.assertRaises(AssertionError):q.verify_harness_alias(folder,plan,proof)
if __name__=='__main__':unittest.main()
