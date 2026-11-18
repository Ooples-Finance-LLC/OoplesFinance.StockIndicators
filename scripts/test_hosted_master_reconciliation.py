import copy,json,tempfile,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import master_reconciliation as q
from hosted_timing import targeted
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-triple-body-master-reconciliation-20261117.json'
class MasterReconciliationTests(unittest.TestCase):
    def plan(self):return q.p.read(PLAN)
    def test_actual_targeted_registry_and_preparation_reference_boundary(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False);targeted.verify_reference(plan)
        self.assertEqual(targeted.expected_totals(plan),(120,24));self.assertEqual(targeted.SCOPES[q.SCOPE]['paths'],q.PATHS)
        self.assertEqual(q.p.phase_tasks(plan,'targeted',0),q.original.shards()[0]['tasks'])
        for label in ('old','successor','variants'):self.assertEqual(q.p.cpu_test_classes(plan,label),q.UNIT)
    def test_explicit_unbound_fixture_stops_before_sdk_or_input_io(self):
        plan=self.plan();plan['inputs_ready']=False
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output',side_effect=AssertionError('SDK/input I/O forbidden')) as calls,mock.patch.object(q,'verify_reconciliation'):
            # Structural proof reads are allowed; no artifact/SDK paths are reached.
            approval=q.p.read;real_read=json.loads((PLAN.parent/'hosted-triple-body-master-reconciliation-20261117-approval.json').read_text())
            def read(path):return real_read if str(path).endswith('-approval.json') else plan
            with mock.patch.object(q.p,'read',side_effect=read):
                with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-output')
            calls.assert_not_called()
    def test_actual_receipt_pins_are_required_when_readiness_is_true(self):
        plan=self.plan();plan['inputs_ready']=True;approved=q.p.read(q.p.REPO/plan['scope_approval']['path']);approved['approved']=True
        actual_read=q.p.read
        def read(path):return approved if str(path).endswith('-approval.json') else actual_read(path)
        # Deliberately unbind a fixture, independent of future checked-in pins.
        plan['candidate']['production_sha256']='UNBOUND';plan['harness_sha256']='UNBOUND'
        with mock.patch.object(q.p,'read',side_effect=read):
            with self.assertRaises(AssertionError):q.validate_scope(plan)
    def test_old_dependency_only_verifier_is_not_extended(self):
        from hosted_timing import reconciled_baseline
        self.assertNotIn(q.SCOPE,Path(reconciled_baseline.__file__).read_text())
    def test_source_or_policy_expansion_rejected(self):
        for mutate in [lambda p:p['sources'].update(variants=q.OLD),lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p.update(required_native_closure_pair='TaLib.Candles.ThreeInside')]:
            plan=self.plan();mutate(plan)
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def test_foreign_prior_reference_or_false_gates_are_rejected(self):
        reference=self.plan()['targeted_reference'];decision=json.loads(q.blob(q.NEW,q.REFERENCE))
        for key,value in [('run_id','1'),('decision','foreign/decision.json'),('sha256','0'*64),('qualified_source',q.OLD)]:
            foreign=copy.deepcopy(reference);foreign[key]=value
            with self.assertRaises(AssertionError):q.verify_prior_qualification(foreign,decision)
        for key in ('accepted','source_gate_accepted','native_progress_gate_passed'):
            false=copy.deepcopy(decision);false[key]=False
            with self.assertRaises(AssertionError):q.verify_prior_qualification(reference,false)
    def test_alias_does_not_imply_another_execution(self):
        alias=q.source_authentication(self.plan());self.assertEqual(alias['variants_alias_of'],'successor');self.assertEqual(alias['additional_cpu_executions'],0);self.assertEqual(alias['additional_gpu_executions'],0)
    def rows(self,winner):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                new=80 if pair==winner and bars==10000 else 110
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110) for k in ('0','3')},candidate_rounds={k:sample(new) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if new==80 else 'inconclusive'))
        return rows,decisions
    def test_two_crows_closure_required_in_addition_to_original_gates(self):
        rows,decisions=self.rows('TaLib.Candles.TwoCrows');self.assertTrue(q.adjudicate_native(rows,decisions)['passed'])
        rows,decisions=self.rows('TaLib.Candles.UniqueThreeRiver');self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_collector_rejects_different_prepared_provenance(self):
        from hosted_timing import verify
        result=dict(case_count=120,decision_count=24,input_provenance_sha256='different-proof',accepted=True)
        with mock.patch.object(q.p,'verify_inputs'),mock.patch.object(q.p,'digest',return_value='expected-proof'),mock.patch.object(verify,'_collect',return_value=result):
            with self.assertRaises(AssertionError):q.collect('raw','prepared')
    def test_harness_alias_rejects_different_tested_dependencies(self):
        with tempfile.TemporaryDirectory(prefix='master-alias-fixture-') as temp:
            folder=Path(temp);(folder/'evidence').mkdir()
            for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
                for label in ('successor','variants'):(folder/'evidence'/(label+suffix)).write_bytes(b'explicit-unit-test-fixture')
            proof=dict(source_authentication=q.source_authentication(self.plan()),production_sha256=dict(successor='same',variants='same'),checks=dict(successor={'passed':1},variants={'passed':1}),tested_runtime_dependencies=dict(successor={'version':'0.151.1'},variants={'version':'foreign'}))
            with self.assertRaises(AssertionError):q.verify_harness_alias(folder,self.plan(),proof)
    def test_prior_native_win_still_cannot_reopen(self):
        rows,decisions=self.rows('TaLib.Candles.TwoCrows')
        for row in rows:
            if row['pair']=='TaLib.Candles.ThreeInside' and row['bars']==10000:
                for value in row['old_rounds'].values():value.update(mean_ns=70,lower_ns=69,upper_ns=71)
        self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
if __name__=='__main__':unittest.main()
