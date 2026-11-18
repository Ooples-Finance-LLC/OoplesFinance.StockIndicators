"""Bounded Ultimate diagnostic orchestration checks; no production builds."""
import copy,json,subprocess,unittest,tempfile
from pathlib import Path
from unittest import mock
from hosted_timing import ultimate_direct_long_qualification as q
from hosted_timing import pipeline,targeted,verify
PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-ultimate-direct-long-qualification-20261118.json'

class UltimateDirectLongQualificationTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self,winner=0):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for index,pair in enumerate(q.PAIRS):
            for bars in (1000,10000):
                win=index==winner and bars==10000
                rows.append(dict(pair=pair,bars=bars,old_rounds={k:sample(110 if win else 70) for k in ('0','3')},candidate_rounds={k:sample(80 if win else 70) for k in ('1','2')},native_control=sample(100)))
                decisions.append(dict(fixture='LibraryPairBenchmarks',method='Ooples',pair=pair,bars=bars,verdict='faster' if win else 'inconclusive'))
        return rows,decisions
    def test_actual_scope_source_boundary_and_lf_policy(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertNotIn(b'\r',PLAN.read_bytes())
        rel=PLAN.relative_to(q.p.REPO).as_posix()
        self.assertEqual(q.p.git('check-attr','text','eol','--',rel).splitlines(),[rel+': text: set',rel+': eol: lf'])
        self.assertEqual(q.PAIRS,['TaLib.Functions.UltOsc','Skender.GetUltimate'])
    def test_workflow_selects_and_checks_exact_sdk_before_artifact_downloads(self):
        workflow=(q.p.REPO/'.github/workflows/ultimate-direct-long-qualification.yml').read_text()
        selector=workflow.index('Select the installed qualification SDK before input downloads')
        self.assertLess(workflow.index('uses: actions/setup-dotnet@v6'),selector)
        self.assertLess(selector,workflow.index('uses: actions/download-artifact@v8'))
        self.assertIn('{"sdk":{"version":"10.0.403","rollForward":"disable","allowPrerelease":false}}',workflow)
        self.assertIn("$actualSdk -ne '10.0.403'",workflow)
        self.assertIn('$actualInfo=(& dotnet --info | Out-String)',workflow)
        self.assertIn('Wrong actual qualification host runtime:',workflow)
        self.assertIn(r'Version:\s+10\.0\.15',workflow)
    def test_actual_ready_preparation_checks_sdk_before_artifact_authentication(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True)
        real_check_output=q.subprocess.check_output
        for sdk in ('10.0.404','10.0.403'):
            query=lambda args,**kwargs:sdk if args==['dotnet','--version'] else real_check_output(args,**kwargs)
            with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output',side_effect=query),mock.patch.object(q,'checked_inputs',side_effect=RuntimeError('artifact boundary reached')) as inputs:
                if sdk=='10.0.404':
                    with self.assertRaises(AssertionError):q.prepare('missing-old','missing-new','missing-out')
                    inputs.assert_not_called()
                else:
                    with self.assertRaisesRegex(RuntimeError,'artifact boundary reached'):q.prepare('missing-old','missing-new','missing-out')
                    inputs.assert_called_once()
    def test_unbound_or_unreviewed_stops_before_sdk_and_artifact_io(self):
        for change in ({},{'inputs_ready':True}):
            plan=self.plan();plan.update(inputs_ready=False,parent_review_approved=False);plan.update(change)
            with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('I/O forbidden')) as calls,mock.patch.object(q,'checked_inputs') as inputs:
                with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.prepare('missing-old','missing-new','missing-out')
                calls.assert_not_called();inputs.assert_not_called()
    def test_readiness_cannot_replace_actual_pins(self):
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True)
        plan['candidate']['manifest_sha256']='UNBOUND'
        with self.assertRaises(AssertionError):q.validate_scope(plan)
    def test_policy_scope_and_classes_cannot_expand(self):
        mutations=[lambda p:p.update(cases=20),lambda p:p.update(decisions=4),lambda p:p['matrix'].update(max_parallel=2),lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000),lambda p:p.update(automatic_qualification=True),lambda p:p['sources'].update(variants=q.NEW),lambda p:p['candidate']['unit_classes'].pop(),lambda p:p['targeted_shards'][0]['tasks'][0]['operations'].pop(),lambda p:p['native_progress_gate'].update(minimum_margin=.01),lambda p:p.update(require_net_10000_point_progress=False)]
        for mutate in mutations:
            plan=self.plan();mutate(plan)
            with self.assertRaises((AssertionError,KeyError)):q.validate_scope(plan,require_ready=False)
    def test_shared_phase_and_registry_use_dedicated_scope(self):
        plan=self.plan()
        with mock.patch.object(q,'validate_scope') as check:
            targeted.verify_reference(plan);check.assert_called_once()
            self.assertEqual(pipeline.phase_tasks(plan,'targeted',0),q.shards()[0]['tasks'])
        self.assertEqual(pipeline.phase_labels('diagnostic'),('old','successor'))
        self.assertEqual(pipeline.warmups_for(plan,'LibraryPairBenchmarks',q.shards()[0]['tasks'][0]),1000)
    def test_each_pair_can_fund_only_real_new_ten_k_win(self):
        for winner in range(2):
            rows,decisions=self.rows(winner);gate=q.roi.adjudicate_feasibility(rows,decisions)
            self.assertTrue(gate['passed']);self.assertEqual(len(gate['new_10000_target_wins']),1)
            self.assertEqual(len(gate['all_pair_size_rows']),4)
            self.assertFalse(gate['qualification']);self.assertFalse(gate['automatic_qualification']);self.assertEqual(gate['closure_credit'],0)
    def test_both_new_rounds_require_margin_interval_and_source_faster(self):
        mutations=[lambda r,d:d[1].update(verdict='inconclusive')]
        for index in ('1','2'):
            mutations.extend([lambda r,d,i=index:r[1]['candidate_rounds'][i].update(upper_ns=99),lambda r,d,i=index:r[1]['candidate_rounds'][i].update(mean_ns=91,lower_ns=90,upper_ns=92)])
        for mutate in mutations:
            rows,decisions=self.rows();mutate(rows,decisions);self.assertFalse(q.roi.adjudicate_feasibility(rows,decisions)['passed'])
    def test_old_point_winner_cannot_be_claimed_new(self):
        for index in ('0','3'):
            rows,decisions=self.rows();rows[1]['old_rounds'][index].update(mean_ns=95,lower_ns=94,upper_ns=96)
            self.assertFalse(q.roi.adjudicate_feasibility(rows,decisions)['passed'])
    def test_any_source_regression_blocks(self):
        for index in range(4):
            rows,decisions=self.rows();decisions[index]['verdict']='slower'
            self.assertFalse(q.roi.adjudicate_feasibility(rows,decisions)['passed'])
    def test_prior_confident_wins_and_net_count_protected(self):
        rows,decisions=self.rows();rows[3]['candidate_rounds']['1'].update(mean_ns=105,lower_ns=104,upper_ns=106)
        gate=q.roi.adjudicate_feasibility(rows,decisions)
        self.assertFalse(gate['passed']);self.assertTrue(gate['prior_native_win_reopenings']);self.assertFalse(gate['net_10000_point_progress'])
    def test_duplicate_missing_and_foreign_pairs_fail_closed(self):
        rows,decisions=self.rows()
        for broken in (rows[:-1],rows+[copy.deepcopy(rows[0])]):
            with self.assertRaises(AssertionError):q.roi.adjudicate_feasibility(broken,decisions)
        rows[0]['pair']='foreign'
        with self.assertRaises(AssertionError):q.roi.adjudicate_feasibility(rows,decisions)
    def test_harness_roles_keep_actual_binary_identity_separate(self):
        plan=self.plan();plan['candidate']['actual_harness_sha256']='a'*64
        old=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['harness_sha256']}));new=dict(manifest=dict(runtime_files={q.HARNESS_PATH:'a'*64}))
        alias=q.harness_role_alias(plan,old,new)
        self.assertEqual(alias['common_harness_sha256'],plan['old_candidate']['actual_harness_sha256'])
        self.assertFalse(alias['actual_artifact_binary_identical']);self.assertTrue(alias['scoped_source_identical'])
    def test_collect_requires_full_scope_and_native_gate(self):
        plan=self.plan()
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(q,'collect',return_value={'accepted':True}) as collect:
            self.assertTrue(verify.collect(Path('unused'),'targeted')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'diagnostic')
        rows,lp=self.rows(1)
        decisions=lp+[dict(fixture='SharedUltimateBenchmarks',pair=p,bars=n,method=m,verdict='inconclusive') for p in q.PAIRS for n in (1000,10000) for m in ('BuilderFull','BuilderLatestOnly')]
        for source_pass,native_pass in ((True,True),(False,True),(True,False)):
            chosen=copy.deepcopy(rows)
            if not native_pass:chosen[3]['candidate_rounds']['1'].update(mean_ns=95,lower_ns=94,upper_ns=96)
            result=dict(accepted=source_pass,case_count=60,decision_count=12,decisions=decisions)
            with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q.roi,'native_rows',return_value=chosen):
                got=q.collect(Path('unused'));self.assertEqual(got['accepted'],source_pass and native_pass);self.assertTrue(got['qualification']);self.assertFalse(got['production_integrated'])
    def test_generic_collection_cannot_bypass_qualification_gate(self):
        with mock.patch.object(verify,'read',return_value=self.plan()):
            with self.assertRaises(AssertionError):verify._collect(Path('unused'),'targeted',preflight=False)
    def test_exact_sixty_case_inventory_and_warmups(self):
        plan=self.plan();self.assertEqual([(t['fixture'],t['cases'],t['decisions']) for t in q.shards()[0]['tasks']],[('LibraryPairBenchmarks',20,4),('SharedUltimateBenchmarks',40,8)])
        self.assertEqual(targeted.expected_totals(plan),(60,12))
        self.assertEqual(pipeline.warmups_for(plan,'SharedUltimateBenchmarks',q.shards()[0]['tasks'][1]),10)
        self.assertEqual(plan['focused_fixture_checks']['old']['cases'],4)
    def test_focused_check_requires_both_exact_source_bound_four_case_receipts(self):
        plan=self.plan()
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            for label in ('old','successor'):
                receipt=plan['focused_fixture_checks'][label]
                names=[f'SharedAllocationProbeTests.UltimateFixtureHandlesSingleOutputAndWorkersAndRestoresSettings(count: {count}, operation: &quot;{pair}&quot;)' for count in (29,513) for pair in q.PAIRS]
                raw=('<TestRun><Results>'+''.join(f'<UnitTestResult outcome="Passed" testName="{name}" />' for name in names)+'</Results></TestRun>').encode()
                target=root/receipt['path'];target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(raw);receipt['sha256']=q.sha(raw)
                candidate=plan['old_candidate' if label=='old' else 'candidate']
                auxiliary=dict(status='actual-ultimate-shared-fixture-auxiliary-passed',arm='old' if label=='old' else 'new',
                    **{key:candidate[key] for key in ('source','source_fingerprint','ci_run','manifest_sha256')},
                    trx_sha256=receipt['sha256'],passed_cases=4,test_method='OoplesFinance.StockIndicators.CompetitorTests.SharedAllocationProbeTests.UltimateFixtureHandlesSingleOutputAndWorkersAndRestoresSettings',
                    runtime_pins={'OoplesFinance.StockIndicators.dll':candidate['production_sha256'],'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll':candidate['actual_harness_sha256']},
                    runtime_closure_files_verified=185,host_runtime='10.0.15',auxiliary_bootstrap={'AIDOTNET_DISABLE_GPU':'1'},new_local_builds=0,new_measurements=0,gpu_reexecuted=False,
                    original_cpu_gpu_receipts_changed=False,auxiliary_only=True,qualification=False,vstest_timeout_seconds=120,
                    cases=[dict(count=count,operation=pair) for count in (29,513) for pair in q.PAIRS])
                auxiliary_raw=json.dumps(auxiliary).encode();(root/receipt['receipt_path']).write_bytes(auxiliary_raw);receipt['receipt_sha256']=q.sha(auxiliary_raw)
            with mock.patch.object(q.p,'REPO',root):
                self.assertEqual(set(q.verify_focused_checks(plan)),{'old','successor'})
                for change in ('source','sha256','cases'):
                    bad=copy.deepcopy(plan);bad['focused_fixture_checks']['successor'][change]='bad'
                    with self.assertRaises(AssertionError):q.verify_focused_checks(bad)
                path=root/plan['focused_fixture_checks']['old']['path'];raw=path.read_bytes().replace(b'count: 29',b'count: 129');path.write_bytes(raw);plan['focused_fixture_checks']['old']['sha256']=q.sha(raw)
                with self.assertRaises(AssertionError):q.verify_focused_checks(plan)
    def test_actual_focused_auxiliary_receipts_and_cases_are_bound(self):
        plan=self.plan();self.assertEqual(set(q.verify_focused_checks(plan)),{'old','successor'})
        for label in ('old','successor'):
            entry=plan['focused_fixture_checks'][label]
            with self.subTest(label=label):
                self.assertEqual(q.sha(q.focused_receipt_bytes(plan,label)),entry['receipt_sha256'])
    def test_any_builder_regression_blocks_full_qualification_despite_native_win(self):
        rows,lp=self.rows(1)
        decisions=lp+[dict(fixture='SharedUltimateBenchmarks',pair=p,bars=n,method=m,verdict='inconclusive') for p in q.PAIRS for n in (1000,10000) for m in ('BuilderFull','BuilderLatestOnly')]
        for index in range(4,12):
            changed=copy.deepcopy(decisions);changed[index]['verdict']='slower'
            result=dict(accepted=True,case_count=60,decision_count=12,decisions=changed)
            with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q.roi,'native_rows',return_value=rows):
                self.assertFalse(q.collect(Path('unused'))['accepted'])
    def test_ta_only_win_does_not_replace_required_skender_closure(self):
        rows,lp=self.rows(0)
        decisions=lp+[dict(fixture='SharedUltimateBenchmarks',pair=p,bars=n,method=m,verdict='inconclusive') for p in q.PAIRS for n in (1000,10000) for m in ('BuilderFull','BuilderLatestOnly')]
        result=dict(accepted=True,case_count=60,decision_count=12,decisions=decisions)
        with mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(q.original,'verify_runtime'),mock.patch.object(verify,'_collect',return_value=result),mock.patch.object(q.roi,'native_rows',return_value=rows):
            got=q.collect(Path('unused'));self.assertFalse(got['accepted']);self.assertFalse(got['native_progress_gate']['required_pair_passed'])

if __name__=='__main__':unittest.main()
