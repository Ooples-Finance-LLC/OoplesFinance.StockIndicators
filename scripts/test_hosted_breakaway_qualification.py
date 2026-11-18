import copy,json,tempfile,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import breakaway_qualification as q
from hosted_timing import targeted,verify

PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-breakaway-isolated-qualification-20261118.json'

class BreakawayQualificationTests(unittest.TestCase):
    def plan(self):return q.p.read(PLAN)
    def test_exact_draft_source_harness_matrix_and_original_policy(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual(targeted.expected_totals(plan),(108,24))
        self.assertEqual(targeted.SCOPES[q.SCOPE]['paths'],q.PATHS)
        tasks=q.p.phase_tasks(plan,'targeted',0)
        self.assertEqual([task['cases'] for task in tasks],[40,48,20])
        self.assertEqual([task['decisions'] for task in tasks],[8,12,4])
        self.assertEqual(tasks[1]['methods'],['BuilderFull','BuilderLatestOnly'])
        self.assertEqual(q.p.cpu_test_classes(plan,'old'),q.auth.UNIT)
        self.assertEqual(q.p.cpu_test_classes(plan,'successor'),q.UNIT)
    def test_real_targeted_matrix_boundary_with_explicit_mock_prior_decision(self):
        decision=dict(accepted=True,phase='targeted',case_count=120,decision_count=24,verdicts={'faster':1},run_id='44222776323',sources=dict(old=q.auth.OLD,new=q.OLD),production_integrated=False,report_imported=False)
        with mock.patch.object(q,'verify_reference',return_value=decision) as prior:
            targeted.verify_reference(self.plan())
        prior.assert_called_once()
    def test_unbound_actual_current_reference_is_rejected(self):
        plan=self.plan();plan['targeted_reference'].update(archive_source='UNBOUND',sha256='UNBOUND')
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.verify_reference(plan)
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):targeted.verify_reference(plan)
    def test_explicit_prior_decision_fixture_requires_current_source_and_all_gates(self):
        decision=dict(status='qualified-for-integration',accepted=True,phase='targeted',case_count=120,decision_count=24,run_id='44222776323',workflow_head='304d97f61654ad4159b36b4ca6082667ea74df78',sources=dict(old=q.auth.OLD,new=q.OLD),source_gate_accepted=True,native_progress_gate_passed=True,master_reconciliation=True,closure_ledger_new_rows=0,native_progress_gate=dict(required_two_crows_closure_preserved=True))
        def check(fixture):
            plan=self.plan();raw=json.dumps(fixture).encode();plan['targeted_reference'].update(archive_source='1'*40,sha256=q.auth.sha(raw))
            with mock.patch.object(q.p,'git',return_value=''),mock.patch.object(q.auth,'blob',return_value=raw):return q.verify_reference(plan)
        self.assertEqual(check(decision),decision)
        for field in ('accepted','source_gate_accepted','native_progress_gate_passed','master_reconciliation'):
            wrong=copy.deepcopy(decision);wrong[field]=False
            with self.assertRaises(AssertionError):check(wrong)
        wrong=copy.deepcopy(decision);wrong['sources']['new']=q.NEW
        with self.assertRaises(AssertionError):check(wrong)
        wrong=copy.deepcopy(decision);wrong['native_progress_gate']['required_two_crows_closure_preserved']=False
        with self.assertRaises(AssertionError):check(wrong)
    def test_unbound_prepare_stops_before_sdk_or_artifact_io(self):
        plan=self.plan();plan['inputs_ready']=False;actual_read=q.p.read
        def read(path):return plan if Path(path)==PLAN else actual_read(path)
        with mock.patch.object(q.p,'PLAN',PLAN),mock.patch.object(q.p,'read',side_effect=read),mock.patch.object(q,'verify_source'),mock.patch.object(q.subprocess,'check_output',side_effect=AssertionError('SDK or artifact I/O forbidden')) as calls:
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-output')
        calls.assert_not_called()
    def test_scope_policy_source_and_fixture_expansion_rejected(self):
        for mutate in [lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p['targeted_shards'][0]['tasks'][1]['methods'].append('NativeSingle'),lambda p:p['sources'].update(variants=q.NEW),lambda p:p.update(required_native_closure_pair=q.TWO),lambda p:p.update(existing_two_crows_recounted=True)]:
            plan=self.plan();mutate(plan)
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def rows(self,winner=q.BREAK,two_new=80):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                old=70 if pair==q.TWO else 110
                new=two_new if pair==q.TWO else 80 if pair==winner and bars==10000 else 110
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(old) for k in ('0','3')},candidate_rounds={k:sample(new) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if pair==winner and bars==10000 else 'inconclusive'))
        return rows,decisions
    def test_only_breakaway_can_earn_new_native_closure(self):
        rows,decisions=self.rows();result=q.adjudicate_native(rows,decisions)
        self.assertTrue(result['passed']);self.assertEqual(result['new_10000_closures'],[dict(pair=q.BREAK,bars=10000,own_source_verdict='faster')])
        for other in (q.EXTENDED[1],q.EXTENDED[2],q.TWO):
            rows,decisions=self.rows(other)
            self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_two_crows_margin_is_diagnostic_without_new_acceptance_gate(self):
        rows,decisions=self.rows(two_new=95);result=q.adjudicate_native(rows,decisions)
        self.assertTrue(result['passed']);self.assertFalse(result['two_crows_robust_10000_diagnostic'])
    def test_two_crows_prior_confident_win_at_either_size_cannot_reopen(self):
        for bars in (1000,10000):
            rows,decisions=self.rows()
            row=next(row for row in rows if row['pair']==q.TWO and row['bars']==bars)
            for value in row['candidate_rounds'].values():value.update(mean_ns=110,lower_ns=109,upper_ns=111)
            self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_original_native_margin_interval_and_own_source_faster_required(self):
        for kind in ('margin','interval','source'):
            rows,decisions=self.rows();row=next(row for row in rows if row['pair']==q.BREAK and row['bars']==10000)
            if kind=='margin':
                for value in row['candidate_rounds'].values():value.update(mean_ns=91,lower_ns=90,upper_ns=92)
            elif kind=='interval':row['candidate_rounds']['1']['upper_ns']=101
            else:
                next(decision for decision in decisions if decision['pair']==q.BREAK and decision['bars']==10000)['verdict']='inconclusive'
            self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_existing_breakaway_point_win_is_not_a_new_closure(self):
        rows,decisions=self.rows();row=next(row for row in rows if row['pair']==q.BREAK and row['bars']==10000)
        for value in row['old_rounds'].values():value.update(mean_ns=90,lower_ns=89,upper_ns=91)
        self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_source_gate_and_native_gate_both_required_and_raw_is_bound(self):
        rows,decisions=self.rows();result=dict(case_count=108,decision_count=24,accepted=False,decisions=decisions,input_provenance_sha256='actual')
        with mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q.native,'native_rows',return_value=rows):
            self.assertFalse(q.collect('fixture')['accepted'])
        with mock.patch.object(q.p,'verify_inputs'),mock.patch.object(q.p,'digest',return_value='different'),mock.patch.object(verify,'_collect',return_value=result):
            with self.assertRaises(AssertionError):q.collect('fixture','prepared')
    def test_alias_uses_baseline_without_additional_execution_or_changed_dependencies(self):
        alias=q.source_authentication(self.plan());self.assertEqual(alias['variants_alias_of'],'old');self.assertEqual(alias['additional_cpu_executions'],0);self.assertEqual(alias['additional_gpu_executions'],0)
        with tempfile.TemporaryDirectory(prefix='break-alias-') as temp:
            folder=Path(temp);(folder/'evidence').mkdir()
            for suffix in ('.trx','-parity.trx','-original-gpu-receipt.json','-original-gpu-gpu.trx.gz'):
                for label in ('old','variants'):(folder/'evidence'/(label+suffix)).write_bytes(b'explicit-fixture')
            proof=dict(source_authentication=alias,production_sha256=dict(old='same',variants='same'),checks=dict(old={'passed':73},variants={'passed':73}),tested_runtime_dependencies=dict(old={'version':'0.151.1'},variants={'version':'foreign'}))
            with self.assertRaises(AssertionError):q.verify_harness_alias(folder,self.plan(),proof)

if __name__=='__main__':unittest.main()
