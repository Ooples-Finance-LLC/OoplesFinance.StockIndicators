"""Actual consumer, evidence and native-gate regressions; no build or timing."""
import ast
import copy
import hashlib
import inspect
import json
import tempfile
import unittest
from pathlib import Path
from unittest import mock

from hosted_timing import sinc_original_startup_roi as q
from hosted_timing import analytic_grid_roi as prior
from hosted_timing import pipeline,roi_scope_dispatch,verify

REPO=Path(__file__).resolve().parents[1]
PLAN=REPO/'benchmarks/plans/hosted-sinc-original-startup-roi-20261119.json'

class SincOriginalStartupRoiTests(unittest.TestCase):
    def plan(self): return json.loads(PLAN.read_bytes())

    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows=[];decisions=[]
        for task in q.shards()[0]['tasks']:
            fixture=task['fixture'];pair=task['operations'][0]['raw']
            for bars in q.COUNTS:
                for method in (m for m in task['methods'] if m in pipeline.OWNED):
                    target=fixture==q.SINC and bars==10000
                    old=200 if fixture==q.SINC else 70
                    new=95 if target else old
                    controls={'NativeSingle':sample(120),'NativeParallel':sample(100)} if fixture==q.SINC else {'Competitor':sample(100)}
                    rows.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,
                        old_rounds={i:sample(old) for i in ('0','3')},candidate_rounds={i:sample(new) for i in ('1','2')},native_controls=controls))
                    decisions.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,verdict='faster' if target else 'inconclusive'))
        return rows,decisions

    def test_held_exact_scope_evidence_and_five_source_consumers(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertIsInstance(plan['inputs_ready'],bool);self.assertIsInstance(plan['parent_review_approved'],bool)
        self.assertEqual((plan['cases'],plan['decisions']),(30,6))
        self.assertEqual([sum(r['round']==i for r in q.case_inventory()) for i in range(4)],[6,6,12,6])
        self.assertEqual(len(plan['full_consumer_registry']),4)
        self.assertNotIn(b'\r',PLAN.read_bytes())

    def test_held_blocks_before_sdk_artifact_or_workload_io(self):
        plan=self.plan();plan['inputs_ready']=plan['parent_review_approved']=False
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q.subprocess,'check_output') as process,mock.patch.object(q,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'HELD'):q.prepare('old','new','out')
            process.assert_not_called();inputs.assert_not_called()

    def test_approval_flags_do_not_require_changing_tests_or_frozen_scope(self):
        plan=self.plan();plan['inputs_ready']=plan['parent_review_approved']=True
        with mock.patch.object(q,'verify_source_boundary'),mock.patch.object(q,'verify_registry'),mock.patch.object(q,'verify_publication'),mock.patch.object(q,'verify_root_evidence'):
            q.validate_scope(plan)

    def test_source_and_input_boundary_not_repointed_in_imported_modules(self):
        self.assertEqual(prior.OLD,'48d238f56c37986b8437e5c31164552cc1362802')
        self.assertEqual(prior.NEW,q.OLD)
        self.assertEqual(prior.shards()[0]['cases'],40)
        self.assertEqual(q.shards()[0]['cases'],30)

    def test_common_harness_is_explicit_old_role_without_binary_identity_claim(self):
        plan=self.plan()
        old=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['old_candidate']['actual_harness_sha256']}))
        new=dict(manifest=dict(runtime_files={q.HARNESS_PATH:plan['candidate']['actual_harness_sha256']}))
        alias=q.harness_role_alias(plan,old,new)
        self.assertEqual(alias['common_artifact_role'],'old')
        self.assertEqual(alias['common_harness_sha256'],plan['old_candidate']['actual_harness_sha256'])
        self.assertFalse(alias['actual_artifact_binary_identical']);self.assertTrue(alias['scoped_source_identical'])
        wrong=copy.deepcopy(plan);wrong['candidate']['actual_harness_sha256']='0'*64
        with self.assertRaises(AssertionError):q.harness_role_alias(wrong,old,new)
        with mock.patch.object(q,'verify_source_boundary'),mock.patch.object(q.p,'git',side_effect=['old-tree','new-tree']):
            with self.assertRaises(AssertionError):q.harness_role_alias(plan,old,new)

    def test_mutated_scope_control_or_alias_fails_closed(self):
        changes=[lambda p:p.update(cases=29),lambda p:p.update(decisions=5),
            lambda p:p['matrix'].update(max_parallel=2),lambda p:p['sources'].update(successor=q.OLD),
            lambda p:p['harness_alias_policy'].update(claim_artifact_binary_identity=True),
            lambda p:p['native_progress_gate'].update(minimum_margin=.1),lambda p:p.update(report_import_permitted=True),
            lambda p:p['targeted_shards'][0]['tasks'][1]['methods'].remove('BuilderLatestOnly'),
            lambda p:p['targeted_shards'][0]['tasks'][1]['methods'].remove('NativeParallel'),
            lambda p:p['requested_case_inventory'][0]['parameters'].update(Count=1000),
            lambda p:p['full_consumer_registry'].pop(),lambda p:p['candidate'].update(unit_passed=94)]
        for change in changes:
            plan=self.plan();change(plan)
            with self.subTest(change=change),self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)

    def test_missing_or_wrong_root_evidence_cannot_become_ready(self):
        for mutate in [lambda p:p.update(builder_proof_root_review=None),
                       lambda p:p['builder_proof'].update(sha256='0'*64),
                       lambda p:p['candidate']['gpu_receipt'].update(sha256='0'*64),
                       lambda p:p['candidate'].update(producer='0'*40)]:
            plan=self.plan();mutate(plan)
            with self.subTest(mutate=mutate),self.assertRaises(AssertionError):q.verify_root_evidence(plan)

    def test_exact_filters_keep_actual_pair_size_metadata(self):
        inventory=q.case_inventory()
        self.assertEqual(len({(x['round'],x['fixture'],x['raw'],x['bars'],x['method']) for x in inventory}),30)
        for row in inventory:
            self.assertNotIn('BuilderReference',row['method'])
            parameter='Bars' if row['fixture']=='LibraryPairBenchmarks' else 'Count'
            self.assertEqual(row['parameters'][parameter],row['bars'])
            if row['method'] not in pipeline.OWNED:self.assertEqual(row['round'],2)
            self.assertIn('"'+row['raw']+'"',row['filters'][0])

    def test_five_percent_mean_gain_with_separated_intervals_passes(self):
        rows,decisions=self.rows();result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['passed']);self.assertIsNone(result['minimum_native_margin'])
        self.assertEqual({x['method'] for x in result['new_10000_builder_wins']},{'BuilderFull','BuilderLatestOnly'})
        self.assertTrue(all(x['fixture']==q.SINC for x in result['new_10000_builder_wins']))

    def test_new_native_win_still_requires_own_source_faster(self):
        rows,decisions=self.rows()
        for decision in decisions:
            if decision['fixture']==q.SINC:decision['verdict']='inconclusive'
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['net_10000_point_progress']);self.assertFalse(result['new_10000_builder_wins']);self.assertFalse(result['passed'])

    def test_one_thousand_builder_wins_are_not_new_target_credit(self):
        rows,decisions=self.rows()
        for row in rows:
            if row['fixture']!=q.SINC:continue
            for value in row['candidate_rounds'].values():
                value.update(mean_ns=95 if row['bars']==1000 else 200,
                             lower_ns=94 if row['bars']==1000 else 199,
                             upper_ns=96 if row['bars']==1000 else 201)
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertFalse(result['new_10000_builder_wins']);self.assertFalse(result['passed'])

    def test_faster_parallel_control_cannot_be_replaced_by_slower_single(self):
        rows,decisions=self.rows()
        for row in rows:
            if row['fixture']==q.SINC:
                row['native_controls']['NativeParallel']=dict(mean_ns=90,lower_ns=89,upper_ns=91)
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_both_new_intervals_must_be_below_best_native_lower(self):
        for index in ('1','2'):
            rows,decisions=self.rows()
            for row in rows:
                if row['fixture']==q.SINC and row['bars']==10000:row['candidate_rounds'][index]['upper_ns']=99
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_best_native_lower_not_native_mean_or_slowest_control(self):
        rows,decisions=self.rows()
        for row in rows:
            if row['fixture']==q.SINC:row['native_controls']['NativeSingle']['lower_ns']=94
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_missing_or_different_shared_controls_rejected(self):
        rows,decisions=self.rows();rows[-1]['native_controls'].pop('NativeParallel')
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows,decisions)
        rows,decisions=self.rows();rows[-1]['native_controls']['NativeParallel']['mean_ns']=101
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows,decisions)

    def test_prior_point_winner_is_ineligible_even_without_prior_interval_win(self):
        rows,decisions=self.rows()
        for row in rows:
            if row['fixture']==q.SINC and row['bars']==10000:
                row['old_rounds']['0']=dict(mean_ns=99,lower_ns=80,upper_ns=120)
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_prior_gma_both_sizes_and_every_source_decision_are_protected(self):
        for bars in q.COUNTS:
            rows,decisions=self.rows()
            for row in rows:
                if row['pair']==q.PRIOR and row['bars']==bars:row['candidate_rounds']['1']['mean_ns']=101
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
        for index in range(6):
            rows,decisions=self.rows();decisions[index]['verdict']='slower'
            self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_control_only_win_and_loss_of_net_point_progress_cannot_pass(self):
        rows,decisions=self.rows()
        for row in rows:
            if row['fixture']==q.SINC:row['candidate_rounds']=copy.deepcopy(row['old_rounds']);row['candidate_rounds']={'1':row['candidate_rounds']['0'],'2':row['candidate_rounds']['3']}
        self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])
        rows,decisions=self.rows()
        for row in rows:
            if row['bars']!=10000:continue
            if row['pair']==q.PRIOR:
                for value in row['old_rounds'].values():value['upper_ns']=120
                for value in row['candidate_rounds'].values():value['mean_ns']=101
            if row['method']=='BuilderLatestOnly':
                for value in row['candidate_rounds'].values():value.update(mean_ns=200,lower_ns=199,upper_ns=201)
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['new_10000_builder_wins']);self.assertFalse(result['net_10000_point_progress']);self.assertFalse(result['passed'])

    def reports(self,folder,mutate=None):
        root=folder/'shard0';root.mkdir();raw_root=r'D:\agent\evidence\shard0'
        (root/'provenance.json').write_text(json.dumps(dict(raw_root=raw_root)))
        state=dict(rounds=[])
        for index in range(4):
            for task in q.shards()[0]['tasks']:
                cases=[]
                for operation in task['operations']:
                    for bars in q.COUNTS:
                        for method in pipeline.methods_for(task,index):
                            lp=task['fixture']=='LibraryPairBenchmarks'
                            parameters=f'{"PairId" if lp else "Operation"}: "{operation["raw"]}", {"Bars" if lp else "Count"}: {bars}'
                            cases.append(dict(Type=task['fixture'],Method=method,
                                FullName='OoplesFinance.StockIndicators.CompetitorBenchmarks.'+task['fixture']+'.'+method+'('+parameters+')',
                                Statistics=dict(N=7,OriginalValues=[100.0]*7,Mean=100.0,ConfidenceInterval=dict(Lower=99.0,Upper=101.0)),
                                Memory=dict(BytesAllocatedPerOperation=0)))
                if mutate:mutate(index,task['fixture'],cases)
                name=str(index)+'-'+task['fixture']+'.json';path=root/name
                raw=json.dumps(dict(Benchmarks=cases)).encode();path.write_bytes(raw)
                state['rounds'].append(dict(index=index,fixture=task['fixture'],report=raw_root+'\\'+name,sha256=hashlib.sha256(raw).hexdigest()))
        (root/'status.json').write_text(json.dumps(state))
        return root

    def test_actual_reports_keep_six_contexts_and_unique_two_controls(self):
        with tempfile.TemporaryDirectory() as temp:
            folder=Path(temp);self.reports(folder)
            rows=q.native_rows(folder)
            self.assertEqual(len(rows),6)
            self.assertTrue(all(set(r['native_controls'])=={'NativeSingle','NativeParallel'} for r in rows if r['fixture']==q.SINC))

    def test_raw_missing_duplicate_wrong_fixture_operation_or_size_rejected(self):
        def mutation(kind):
            def change(index,fixture,cases):
                if index!=2 or fixture!=q.SINC:return
                if kind=='missing':cases[:]=[c for c in cases if c['Method']!='NativeParallel']
                elif kind=='duplicate':cases.append(copy.deepcopy(cases[-1]))
                elif kind=='fixture':cases[0]['Type']='LibraryPairBenchmarks'
                elif kind=='operation':cases[0]['FullName']=cases[0]['FullName'].replace(q.PAIR,q.PRIOR)
                elif kind=='size':cases[0]['FullName']=cases[0]['FullName'].replace('Count: 1000','Count: 100000')
                elif kind=='parameter':cases[0]['FullName']=cases[0]['FullName'].replace('Count:','Bars:')
            return change
        for kind in ('missing','duplicate','fixture','operation','size','parameter'):
            with self.subTest(kind=kind),tempfile.TemporaryDirectory() as temp:
                folder=Path(temp);self.reports(folder,mutation(kind))
                with self.assertRaises(AssertionError):q.native_rows(folder)

    def test_dispatch_hooks_select_new_scope_without_old_scope_fallback(self):
        plan=self.plan()
        with mock.patch.object(q,'validate_scope') as check:
            roi_scope_dispatch.validate_scope(plan);check.assert_called_once_with(plan)
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(q,'collect',return_value={'accepted':False}) as collect:
            self.assertFalse(verify.collect(Path('unused'),'diagnostic')['accepted']);collect.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'targeted')
        for function,name in [(pipeline.verify_inputs,'verify_authentication'),(pipeline._verify_input_contents,'verify_harness_alias')]:
            tree=ast.parse(inspect.getsource(function))
            branch=next(n for n in ast.walk(tree) if isinstance(n,ast.If) and ast.unparse(n.test)=="plan.get('diagnostic_kind') == 'sinc-original-startup-native-pilot'")
            code=compile(ast.fix_missing_locations(ast.Module(body=[branch],type_ignores=[])),pipeline.__file__,'exec')
            with mock.patch.object(q,name) as check:
                exec(code,dict(__package__='hosted_timing',plan=plan,folder=Path('unused'),proof={}))
                check.assert_called_once()

    def test_workflow_is_manual_held_before_sdk_download_and_has_no_build(self):
        text=(REPO/'.github/workflows/sinc-original-startup-roi.yml').read_text()
        self.assertNotIn('\n  push:',text)
        self.assertLess(text.index('UNBOUND diagnostic'),text.index('actions/setup-dotnet'))
        self.assertLess(text.index('parent_review_approved'),text.index('actions/download-artifact'))
        self.assertIn('Measure 30 actual-builder',text)
        self.assertNotIn('dotnet build',text);self.assertNotIn('dotnet restore',text)
        self.assertIn(q.NEW,text);self.assertIn(q.OLD,text)

    def test_evidence_bytes_and_plan_have_explicit_checkout_policy(self):
        attributes=(REPO/'.gitattributes').read_text()
        self.assertIn('benchmarks/plans/hosted-sinc-original-startup-roi-20261119.json text eol=lf',attributes)
        self.assertIn('benchmarks/plans/evidence/sinc-* -text whitespace=cr-at-eol',attributes)

if __name__=='__main__':unittest.main()
