"""Diagnostic boundaries, real routing, and adversarial promotion cases; no production builds."""
import copy,json,os,sys,tempfile,unittest
from pathlib import Path
from unittest import mock
from hosted_timing import small_candle_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-small-candle-roi-20261118.json'
class SmallCandleRoiTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_text())
    def test_plan_real_worktree_git_blob_and_checkout_filters_are_canonical_lf(self):
        import subprocess
        rel=PLAN.relative_to(q.p.REPO).as_posix();raw=PLAN.read_bytes();blob=q.blob('HEAD',rel)
        self.assertNotIn(b'\r',raw);self.assertEqual(raw,blob)
        self.assertEqual(q.p.git('check-attr','text','eol','--',rel).splitlines(),[rel+': text: set',rel+': eol: lf'])
        filtered=subprocess.check_output(['git','-c','core.autocrlf=true','-c','core.eol=crlf','cat-file','--filters','HEAD:'+rel],cwd=q.p.REPO)
        self.assertEqual(filtered,blob);self.assertNotIn(b'\r',filtered)
    def test_actual_source_boundary_and_diagnostic_tasks(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        real_validate=q.validate_scope
        with mock.patch.object(q,'validate_scope',side_effect=lambda p:real_validate(p,require_ready=False)):
            self.assertEqual(pipeline.phase_tasks(plan,'diagnostic',0),q.shards()[0]['tasks'])
        self.assertEqual(pipeline.phase_labels('diagnostic'),('old','successor'))
        self.assertEqual(pipeline.warmups_for(plan,'LibraryPairBenchmarks',q.shards()[0]['tasks'][0]),1000)
        self.assertEqual(pipeline.cpu_test_classes(plan,'successor'),q.UNIT)
    def test_unbound_preparation_stops_before_sdk_and_artifact_io(self):
        plan=self.plan();plan['inputs_ready']=False
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('I/O forbidden')) as calls,mock.patch.object(q,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-out')
            calls.assert_not_called();inputs.assert_not_called()
    def test_parent_review_cannot_be_replaced_by_readiness(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=False)
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.validate_scope(plan)
    def test_receipt_pins_fail_closed_even_with_booleans_true(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True);plan['candidate']['manifest_sha256']='UNBOUND'
        with self.assertRaises(AssertionError):q.validate_scope(plan)
    def test_no_silent_policy_scope_or_role_expansion(self):
        for mutate in [lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['sources'].update(variants=q.NEW),lambda p:p.update(diagnostic_only=False),lambda p:p.update(cases=40),lambda p:p.update(prospective_1000_acceptance_policy='approved'),lambda p:p.update(automatic_qualification=True),lambda p:p['candidate']['unit_classes'].pop(),lambda p:p['diagnostic_shards'][0]['tasks'][0]['operations'].pop()]:
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
    def rows(self,winner=None):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in (1000,10000):
                new=80 if pair==winner and bars==1000 else 110
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110) for k in ('0','3')},candidate_rounds={k:sample(new) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if new==80 else 'inconclusive'))
        return rows,decisions
    def test_one_genuinely_new_target_is_sufficient_not_all_four(self):
        for winner in q.TARGETS:
            rows,decisions=self.rows(winner);gate=q.adjudicate_feasibility(rows,decisions)
            self.assertTrue(gate['passed']);self.assertEqual(gate['new_1000_target_wins'],[dict(pair=winner,bars=1000,source_verdict='faster')])
            self.assertFalse(gate['automatic_qualification']);self.assertFalse(gate['qualification']);self.assertEqual(gate['closure_credit'],0);self.assertEqual(gate['prospective_1000_acceptance_policy'],'UNBOUND')
    def test_zero_new_1000_wins_stops_source_only_improvement(self):
        rows,decisions=self.rows();decisions[1]['verdict']='faster'
        gate=q.adjudicate_feasibility(rows,decisions)
        self.assertFalse(gate['passed']);self.assertTrue(gate['all_ten_source_comparisons_no_regression']);self.assertFalse(gate['at_least_one_new_1000_target_win'])
    def test_already_baseline_robust_winner_is_reported_but_never_a_new_win(self):
        winner=q.TARGETS[1];rows,decisions=self.rows(winner);row=next(r for r in rows if r['pair']==winner and r['bars']==1000)
        row['old_rounds']={k:dict(mean_ns=70,lower_ns=69,upper_ns=71) for k in ('0','3')}
        row['candidate_rounds']={k:dict(mean_ns=50,lower_ns=49,upper_ns=51) for k in ('1','2')}
        gate=q.adjudicate_feasibility(rows,decisions);detail=next(r for r in gate['per_target_1000'] if r['pair']==winner)
        self.assertTrue(detail['baseline_robust_10percent_native_winner']);self.assertTrue(detail['candidate_robust_10percent_native_win']);self.assertFalse(detail['baseline_new_win_eligible']);self.assertFalse(detail['new_1000_target_win']);self.assertFalse(gate['passed'])
    def test_point_winner_in_either_old_round_is_ineligible_but_mean_tie_is_eligible(self):
        winner=q.TARGETS[0]
        for index in ('0','3'):
            rows,decisions=self.rows(winner);row=next(r for r in rows if r['pair']==winner and r['bars']==1000);row['old_rounds'][index]=dict(mean_ns=95,lower_ns=94,upper_ns=96)
            gate=q.adjudicate_feasibility(rows,decisions);self.assertFalse(gate['passed']);self.assertFalse(gate['per_target_1000'][0]['baseline_new_win_eligible'])
        rows,decisions=self.rows(winner);row=next(r for r in rows if r['pair']==winner and r['bars']==1000);row['old_rounds']={k:dict(mean_ns=100,lower_ns=99,upper_ns=101) for k in ('0','3')}
        self.assertTrue(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_native_margin_interval_separation_and_own_source_faster_all_required(self):
        for mutate in [lambda r,d:r[0]['candidate_rounds']['1'].update(upper_ns=99),lambda r,d:r[0]['candidate_rounds']['2'].update(mean_ns=91,lower_ns=90,upper_ns=92),lambda r,d:d[0].update(verdict='inconclusive')]:
            rows,decisions=self.rows(q.TARGETS[0]);mutate(rows,decisions);self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
    def test_every_one_of_ten_source_regressions_blocks_feasibility(self):
        rows,decisions=self.rows(q.TARGETS[0])
        for i in range(10):
            broken=copy.deepcopy(decisions);broken[i]['verdict']='slower';gate=q.adjudicate_feasibility(rows,broken);self.assertFalse(gate['passed']);self.assertFalse(gate['all_ten_source_comparisons_no_regression'])
    def test_prior_interval_native_wins_are_protected_for_every_pair_both_sizes(self):
        for pair in q.PAIRS:
            for bars in (1000,10000):
                winner=q.TARGETS[1] if pair==q.TARGETS[0] else q.TARGETS[0];rows,decisions=self.rows(winner);row=next(r for r in rows if r['pair']==pair and r['bars']==bars)
                row['old_rounds']={'0':dict(mean_ns=70,lower_ns=40,upper_ns=98),'3':dict(mean_ns=90,lower_ns=80,upper_ns=98)}
                row['candidate_rounds']={k:dict(mean_ns=110,lower_ns=60,upper_ns=160) for k in ('1','2')}
                gate=q.adjudicate_feasibility(rows,decisions);self.assertFalse(gate['passed']);self.assertIn(dict(pair=pair,bars=bars,preserved=False),gate['prior_native_win_reopenings'])
    def test_genuine_new_win_offset_by_lost_mixed_old_point_win_is_net_flat(self):
        rows,decisions=self.rows(q.TARGETS[0])
        control=next(r for r in rows if r['pair']==q.PAIRS[-1] and r['bars']==1000)
        control['old_rounds']['0']=dict(mean_ns=95,lower_ns=94,upper_ns=96)
        gate=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(gate['at_least_one_new_1000_target_win']);self.assertTrue(gate['all_ten_source_comparisons_no_regression']);self.assertFalse(gate['prior_native_win_reopenings'])
        self.assertEqual(gate['old_1000_point_win_counts'],{'0':1,'3':0});self.assertEqual(gate['candidate_1000_point_win_counts'],{'1':1,'2':1});self.assertFalse(gate['net_1000_point_progress']);self.assertFalse(gate['passed'])
    def test_duplicate_missing_or_unknown_verdicts_fail_closed(self):
        rows,decisions=self.rows(q.TARGETS[0])
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows[:-1],decisions)
        broken=copy.deepcopy(decisions);broken[-1]=copy.deepcopy(broken[0])
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows,broken)
        decisions[0]['verdict']='unknown'
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows,decisions)
    def test_distinct_actual_old_native_harness_and_new_hash_are_both_preserved(self):
        plan=self.plan();old=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['old_candidate']['actual_harness_sha256']}));new=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['candidate']['actual_harness_sha256']}))
        alias=q.harness_role_alias(plan,old,new)
        self.assertEqual(alias['common_harness_sha256'],'a794b0bce71ea59bcbe08242122ad3721d61bfd8689e4bb3e2cc14e40740cb64');self.assertEqual(alias['actual_artifact_harness_sha256']['successor'],'75dbbb84c92b0f2cb44d54d4f016d6a5c67068f07792de622be51117d0e7eafa');self.assertFalse(alias['actual_artifact_binary_identical'])
        new['manifest']['runtime_files'][q.HARNESS_PATH]='f'*64
        with self.assertRaises(AssertionError):q.harness_role_alias(plan,old,new)
    def test_collect_routing_cannot_qualify_source(self):
        with mock.patch.object(verify,'read',return_value=self.plan()),mock.patch.object(q,'collect',return_value={'accepted':False}) as collect:
            self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
    def test_diagnostic_collector_retains_no_credit_even_if_feasible(self):
        rows,decisions=self.rows(q.TARGETS[0]);result=dict(accepted=False,case_count=50,decision_count=10,decisions=decisions)
        with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q,'native_rows',return_value=rows):
            got=q.collect(Path('unused'));self.assertTrue(got['feasibility_screen']['passed']);self.assertFalse(got['accepted']);self.assertFalse(got['qualification']);self.assertFalse(got['automatic_qualification']);self.assertEqual(got['closure_ledger_new_rows'],0)
    def test_actual_cli_accepts_diagnostic_measure_without_argparse_rejection(self):
        args=['run-hosted-timing.py','measure','--inputs','relative-inputs','--out','relative-output/shard0','--phase','diagnostic','--shard','0']
        with mock.patch.object(sys,'argv',args),mock.patch.object(pipeline,'measure') as measure:
            pipeline.main()
        measure.assert_called_once_with(Path('relative-inputs').resolve(),Path('relative-output/shard0').resolve(),'diagnostic',0,None)
    def test_actual_cli_collect_verifies_prepared_then_binds_provenance(self):
        args=['run-hosted-timing.py','collect','--inputs','relative-raw','--prepared','relative-prepared','--out','relative-summary.json','--phase','diagnostic']
        result=dict(input_provenance_sha256='actual-prepared-hash',accepted=False,case_count=50,decision_count=10,verdicts={})
        calls=[]
        with mock.patch.object(sys,'argv',args),mock.patch.dict(os.environ,{},clear=True),mock.patch.object(pipeline,'verify_inputs',side_effect=lambda path:calls.append(('inputs',path))),mock.patch.object(verify,'collect',side_effect=lambda path,phase:(calls.append(('collect',path,phase)) or result)),mock.patch.object(pipeline,'digest',return_value='actual-prepared-hash') as digest,mock.patch.object(pipeline,'write') as write:
            pipeline.main()
            self.assertEqual(calls,[('inputs',Path('relative-prepared').resolve()),('collect',Path('relative-raw').resolve(),'diagnostic')])
            digest.assert_called_once_with(Path('relative-prepared')/'provenance.json')
            write.assert_called_once_with(Path('relative-summary.json').resolve(),result)
            result['input_provenance_sha256']='foreign-prepared-hash';write.reset_mock()
            with self.assertRaises(AssertionError):pipeline.main()
            write.assert_not_called()
    def test_unregistered_diagnostic_scope_remains_rejected(self):
        plan=self.plan();plan['diagnostic_kind']='unregistered-diagnostic'
        with self.assertRaises(AssertionError):pipeline.phase_tasks(plan,'diagnostic',0)
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
