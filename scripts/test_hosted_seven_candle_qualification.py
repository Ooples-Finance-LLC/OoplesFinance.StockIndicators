import copy,json,os,sys,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import seven_candle_qualification as q
from hosted_timing import pipeline,targeted,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-seven-candle-word-qualification-20261118.json'

class SevenCandleQualificationTests(unittest.TestCase):
    def plan(self):return q.p.read(PLAN)
    def test_retained_roi_plan_is_canonical_lf_and_git_blob_identical(self):
        raw=(q.p.REPO/q.ROI_PLAN).read_bytes();blob=q.roi.blob('HEAD',q.ROI_PLAN)
        self.assertNotIn(b'\r',raw)
        self.assertEqual(raw,blob)
        self.assertEqual(q.roi.sha(blob),q.ROI_PLAN_SHA)
        self.assertEqual(q.p.git('check-attr','text','eol','--',q.ROI_PLAN).splitlines(),[
            q.ROI_PLAN+': text: set',q.ROI_PLAN+': eol: lf'])
    def test_real_source_reference_and_exact198_42_registry_boundary(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False);targeted.verify_reference(plan)
        self.assertEqual(targeted.expected_totals(plan),(198,42))
        self.assertEqual([task['cases'] for task in plan['targeted_shards'][0]['tasks']],[70,80,48])
        self.assertEqual([task['decisions'] for task in plan['targeted_shards'][0]['tasks']],[14,16,12])
        self.assertEqual(plan['targeted_shards'][0]['tasks'][2]['methods'],['BuilderFull','BuilderLatestOnly'])
        self.assertEqual(pipeline.phase_tasks(plan,'targeted',0),q.shards()[0]['tasks'])
        self.assertEqual(pipeline.cpu_test_classes(plan,'successor'),q.roi.UNIT)
        self.assertEqual(plan['candidate']['unit_passed'],117);self.assertEqual(plan['candidate']['parity_passed'],329)
    def test_unbound_prepare_stops_before_sdk_or_artifact_io(self):
        plan=self.plan();plan.update(inputs_ready=False,parent_review_approved=False)
        real_read=q.p.read
        def read(path):return plan if Path(path)==PLAN else real_read(path)
        with mock.patch.object(q.p,'PLAN',PLAN),mock.patch.object(q.p,'read',side_effect=read),mock.patch.object(q.roi,'verify_source_boundary'),mock.patch.object(q,'verify_reference'),mock.patch.object(q.roi.subprocess,'check_output',side_effect=AssertionError('SDK or artifact I/O forbidden')) as calls,mock.patch.object(q.roi,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-out')
        calls.assert_not_called();inputs.assert_not_called()
    def test_parent_and_roi_approval_cannot_be_replaced_by_readiness(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True)
        parent,evidence=q.approval_documents(plan);parent['approved']=False
        with mock.patch.object(q,'approval_documents',return_value=(parent,evidence)):
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(plan)
        for approved,root in [(False,False),(True,False),(False,True)]:
            bad=copy.deepcopy(evidence);bad.update(approved=approved,root_authenticated=root)
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.verify_roi_approval(bad)
    def test_scope_policy_and_method_expansion_rejected(self):
        for mutate in [lambda p:p.update(cases=200),lambda p:p['targeted_shards'][0]['tasks'][2]['methods'].append('NativeSingle'),lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p['required_native_closure_pairs'].pop(),lambda p:p['sources'].update(variants=q.NEW),lambda p:p['candidate'].update(actual_harness_sha256='f'*64)]:
            plan=self.plan();mutate(plan)
            with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def roi_fixture(self):
        evidence=dict(approved=True,root_authenticated=True,run_id='123',workflow_head='2'*40,summary=dict(path='unit-fixture.json',sha256='1'*64))
        decisions=[dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if pair in q.REQUIRED and bars==10000 else 'inconclusive',provenance=dict(host=dict(run_id='123'),workflow_head='2'*40)) for pair in q.roi.PAIRS for bars in (1000,10000)]
        result=dict(diagnostic_only=True,qualification=False,accepted=False,phase='diagnostic',case_count=30,decision_count=6,sources=dict(old=q.OLD,new=q.NEW),closure_ledger_new_rows=0,decisions=decisions,promotion_gate=dict(passed=True,all_six_source_comparisons_no_regression=True,ladder_bottom_no_source_regression=True,qualification_authorized=False,closure_credit=0,minimum_native_margin=.1,target_10000_robust_native_wins=[dict(pair=pair,bars=10000,passed=True) for pair in q.REQUIRED]))
        return evidence,result
    def test_roi_gate_requires_both_actual_target_wins_all_source_safety_and_zero_credit(self):
        evidence,result=self.roi_fixture()
        def check(data):
            with mock.patch.object(q.p,'digest',return_value='1'*64),mock.patch.object(q.p,'read',return_value=data):q.verify_roi_approval(evidence)
        check(result)
        for mutate in [lambda r:r.update(qualification=True),lambda r:r.update(accepted=True),lambda r:r['promotion_gate'].update(passed=False),lambda r:r['promotion_gate'].update(closure_credit=1),lambda r:r['promotion_gate']['target_10000_robust_native_wins'][1].update(passed=False),lambda r:r['decisions'][1].update(verdict='inconclusive'),lambda r:r['decisions'][-1].update(verdict='slower'),lambda r:r['decisions'][0]['provenance']['host'].update(run_id='999')]:
            bad=copy.deepcopy(result);mutate(bad)
            with self.assertRaises(AssertionError):check(bad)
    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                old=70 if pair=='TaLib.Candles.TwoCrows' else 110
                new=80 if pair=='TaLib.Candles.TwoCrows' or pair in q.REQUIRED and bars==10000 else 110
                rows.append(dict(pair=pair,bars=bars,old_rounds={key:sample(old) for key in ('0','3')},candidate_rounds={key:sample(new) for key in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if pair in q.REQUIRED and bars==10000 else 'inconclusive'))
        return rows,decisions
    def test_both_required_closures_and_original_native_policy(self):
        rows,decisions=self.rows();gate=q.adjudicate_native(rows,decisions)
        self.assertTrue(gate['passed']);self.assertEqual(gate['policy'],q.native.NATIVE_POLICY)
        self.assertEqual({row['pair'] for row in gate['new_10000_closures']},set(q.REQUIRED))
        for target in q.REQUIRED:
            bad=copy.deepcopy(rows);next(row for row in bad if row['pair']==target and row['bars']==10000)['candidate_rounds']['1']['upper_ns']=101
            self.assertFalse(q.adjudicate_native(bad,decisions)['passed'])
    def test_required_closure_must_be_new_and_own_source_faster(self):
        for target in q.REQUIRED:
            rows,decisions=self.rows()
            next(row for row in rows if row['pair']==target and row['bars']==10000)['old_rounds']['0'].update(mean_ns=90,lower_ns=89,upper_ns=91)
            self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
            rows,decisions=self.rows();next(row for row in decisions if row['pair']==target and row['bars']==10000)['verdict']='inconclusive'
            self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_two_crows_prior_win_protection_both_sizes_and_no_recredit(self):
        for bars in (1000,10000):
            rows,decisions=self.rows();row=next(row for row in rows if row['pair']=='TaLib.Candles.TwoCrows' and row['bars']==bars)
            row['candidate_rounds']['1'].update(mean_ns=110,lower_ns=109,upper_ns=111)
            self.assertFalse(q.adjudicate_native(rows,decisions)['passed'])
    def test_source_gate_cannot_be_replaced_by_native_success_and_raw_is_bound(self):
        rows,decisions=self.rows()
        for accepted in (False,True):
            data=dict(case_count=198,decision_count=42,accepted=accepted,decisions=decisions,input_provenance_sha256='actual')
            with mock.patch.object(verify,'_collect',return_value=data),mock.patch.object(q.native,'native_rows',return_value=rows):
                result=q.collect('unit-fixture');self.assertEqual(result['accepted'],accepted);self.assertEqual(result['new_closure_credit_pairs'],q.REQUIRED if accepted else [])
        with mock.patch.object(q.p,'verify_inputs'),mock.patch.object(q.p,'digest',return_value='different'),mock.patch.object(verify,'_collect',return_value=data):
            with self.assertRaises(AssertionError):q.collect('fixture','prepared')
    def test_direct_generic_collector_cannot_skip_native_gate(self):
        with mock.patch.object(verify,'read',return_value=self.plan()):
            with self.assertRaises(AssertionError):verify._collect(Path('unused'),'targeted',preflight=False)
    def test_actual_targeted_cli_boundary_accepts_measure_and_preserves_input_binding(self):
        args=['run-hosted-timing.py','measure','--inputs','inputs','--out','output','--phase','targeted','--shard','0']
        with mock.patch.object(sys,'argv',args),mock.patch.object(pipeline,'measure') as measure:pipeline.main()
        measure.assert_called_once_with(Path('inputs').resolve(),Path('output').resolve(),'targeted',0,None)
        args=['run-hosted-timing.py','collect','--inputs','raw','--prepared','prepared','--out','summary.json','--phase','targeted']
        result=dict(input_provenance_sha256='bound',accepted=False,case_count=198,decision_count=42,verdicts={})
        with mock.patch.object(sys,'argv',args),mock.patch.dict(os.environ,{},clear=True),mock.patch.object(pipeline,'verify_inputs') as inputs,mock.patch.object(verify,'collect',return_value=result),mock.patch.object(pipeline,'digest',return_value='bound'),mock.patch.object(pipeline,'write') as write:
            pipeline.main();inputs.assert_called_once_with(Path('prepared').resolve());write.assert_called_once()
            result['input_provenance_sha256']='foreign';write.reset_mock()
            with self.assertRaises(AssertionError):pipeline.main()
            write.assert_not_called()
    def test_reuses_authenticated_harness_alias_with_explicit_qualification_mode(self):
        with mock.patch.object(q.roi,'verify_authentication') as auth:
            q.verify_authentication('folder','plan','proof')
        auth.assert_called_once_with('folder','plan','proof',validator=q.validate_scope,preparation_mode=q.MODE)

if __name__=='__main__':unittest.main()
