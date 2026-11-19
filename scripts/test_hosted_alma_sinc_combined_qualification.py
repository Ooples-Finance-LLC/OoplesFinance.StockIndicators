"""Focused combined scope/gate/provenance checks; no production build or timing."""
import ast
import copy
import inspect
import json
import re
import unittest
from pathlib import Path
from unittest import mock
from hosted_timing import alma_sinc_combined_qualification as q
from hosted_timing import pipeline,targeted,verify

PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-alma-sinc-combined-qualification-20261119.json'
class CombinedQualificationTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def rows(self):
        sample=lambda n:dict(mean_ns=n,lower_ns=n-1,upper_ns=n+1)
        rows,decisions=[],[]
        for fixture,pair,bars,method in sorted(q.expected_contexts()):
            target=bars==10000 and (fixture==q.SINC or fixture=='LibraryPairBenchmarks' and pair in q.ALMA)
            controls={'Competitor':sample(100)} if fixture=='LibraryPairBenchmarks' else dict(NativeSingle=sample(100),NativeParallel=sample(105))
            rows.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,old_rounds={i:sample(110 if target else 70) for i in ('0','3')},candidate_rounds={i:sample(95 if target else 70) for i in ('1','2')},native_controls=controls))
            decisions.append(dict(fixture=fixture,pair=pair,bars=bars,method=method,verdict='faster' if target else 'inconclusive'))
        return rows,decisions

    def test_full_scope_source_and_recorded_evidence(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual([(s['cases'],s['decisions']) for s in q.shards()],[(90,18),(100,20)])
        fixtures=[t['fixture'] for t in q.all_tasks()]
        self.assertEqual(len(fixtures),len(set(fixtures)))
        self.assertEqual(len(q.expected_contexts()),38)
        self.assertEqual(q.prior.p.git('diff','--name-only',q.OLD,q.NEW,'--',*q.prior.HARNESS_SCOPES),'')
        self.assertNotIn(b'\r',PLAN.read_bytes())

    def test_scope_native_policy_and_credit_cannot_drift(self):
        for change in [lambda p:p.update(cases=130),lambda p:p['matrix'].update(max_parallel=3),lambda p:p.update(lp_afirma_report_row_credit=1),lambda p:p.update(report_import_permitted=True),lambda p:p['native_progress_gate'].update(require_both_family_gates=False),lambda p:p['targeted_shards'][0]['tasks'].pop()]:
            plan=self.plan();change(plan)
            with self.subTest(change=change),self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)

    def test_abba_filters_and_atomic_shards(self):
        rows=q.case_inventory()
        self.assertEqual([sum(r['round']==i for r in rows) for i in range(4)],[38,38,76,38])
        self.assertEqual([sum(r['shard']==s for r in rows) for s in (0,1)],[90,100])
        for row in rows:
            self.assertIn(row['method'],row['filters'][0])
            self.assertEqual(row['parameters'].get('Bars',row['parameters'].get('Count')),row['bars'])
        lp=[t for t in q.all_tasks() if t['fixture']=='LibraryPairBenchmarks'][0]
        self.assertEqual(len(lp['operations']),5)
        self.assertNotIn(q.PAIR,[o['pair'] for o in lp['operations']])

    def test_held_stops_before_sdk_or_inputs(self):
        held=self.plan();held.update(inputs_ready=False,parent_review_approved=False,peer_review_approved=False)
        with mock.patch.object(q.p,'read',return_value=held),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('forbidden')) as io,mock.patch.object(q.prior,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'HELD'):q.prepare('old','new','out')
            io.assert_not_called();inputs.assert_not_called()
        plan=self.plan();plan.update(inputs_ready=True,parent_review_approved=True,peer_review_approved=False)
        with self.assertRaisesRegex(AssertionError,'HELD'):q.validate_scope(plan)

    def test_missing_combined_roots_cannot_be_ready(self):
        q.verify_evidence(self.plan(),require_ready=True)
        broken=self.plan();broken['combined_route_root_review']['sha256']='UNBOUND_ROUTE'
        with self.assertRaisesRegex(AssertionError,'UNBOUND'):q.verify_evidence(broken,require_ready=True)

    def test_source_scoped_reuse_preserves_actual_binary_roles(self):
        descriptor=q.verify_source_scoped_reuse(self.plan())
        self.assertEqual(descriptor['aligned_actual_checks'],dict(unit=35,parity=53))
        self.assertEqual(descriptor['original_actual_checks'],dict(unit=53,parity=48))
        self.assertNotEqual(descriptor['aligned_core_sha256'],descriptor['original_core_sha256'])
        self.assertFalse(descriptor['claim_original_tests_executed_on_aligned_binaries'])
        self.assertEqual(descriptor['timed_out_supplement_credit'],0)
        plan=self.plan();plan['source_scoped_baseline_reuse']['sha256']='0'*64
        with self.assertRaises(AssertionError):q.verify_source_scoped_reuse(plan)

    def test_both_native_family_gates_pass_without_mean_margin(self):
        rows,decisions=self.rows();result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['passed'])
        self.assertEqual(len(result['new_10000_alma_primary_wins']),2)
        self.assertEqual(len(result['new_10000_sinc_builder_wins']),2)
        self.assertTrue(all(g['net_10000_point_progress'] for g in result['family_gates'].values()))

    def test_one_family_cannot_mask_other_family_failure(self):
        for family in ('alma','sinc'):
            rows,decisions=self.rows()
            for row,decision in zip(rows,decisions):
                target=row['bars']==10000 and (row['fixture']==q.SINC if family=='sinc' else row['fixture']=='LibraryPairBenchmarks' and row['pair'] in q.ALMA)
                if target:
                    row['candidate_rounds']={i:dict(row['old_rounds']['0']) for i in ('1','2')};decision['verdict']='inconclusive'
            with self.subTest(family=family):self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_each_family_needs_both_rounds_eligibility_and_faster_source(self):
        for family in ('alma','sinc'):
            for mode in ('round1','round2','eligible','source','bestnative'):
                rows,decisions=self.rows()
                for row,decision in zip(rows,decisions):
                    target=row['bars']==10000 and (row['fixture']==q.SINC if family=='sinc' else row['fixture']=='LibraryPairBenchmarks' and row['pair'] in q.ALMA)
                    if not target:continue
                    if mode in ('round1','round2'):row['candidate_rounds']['1' if mode=='round1' else '2']['upper_ns']=99
                    if mode=='eligible':row['old_rounds']['0']['mean_ns']=90
                    if mode=='source':decision['verdict']='inconclusive'
                    if mode=='bestnative':
                        for control in row['native_controls'].values():control.update(lower_ns=94)
                with self.subTest(family=family,mode=mode):self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_alma_equal_net_is_not_masked_by_sinc_progress(self):
        rows,decisions=self.rows()
        gma=next(r for r in rows if r['fixture']=='LibraryPairBenchmarks' and r['pair']=='QuanTAlib.Gma' and r['bars']==10000)
        gma['old_rounds']={i:dict(mean_ns=99,lower_ns=90,upper_ns=110) for i in ('0','3')}
        gma['candidate_rounds']={i:dict(mean_ns=110,lower_ns=109,upper_ns=111) for i in ('1','2')}
        alma=next(r for r in rows if r['fixture']=='LibraryPairBenchmarks' and r['pair']==q.ALMA[0] and r['bars']==10000)
        alma['old_rounds']={i:dict(mean_ns=95,lower_ns=90,upper_ns=110) for i in ('0','3')}
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertTrue(result['family_gates']['sinc']['net_10000_point_progress'])
        self.assertFalse(result['family_gates']['alma']['net_10000_point_progress'])
        self.assertFalse(result['passed'])

    def test_all_38_source_comparisons_are_protected(self):
        for i in range(38):
            rows,decisions=self.rows();decisions[i]['verdict']='slower'
            with self.subTest(index=i):self.assertFalse(q.adjudicate_feasibility(rows,decisions)['passed'])

    def test_fwma_crossovers_cannot_rescue_original_sinc_roi_net_gate(self):
        rows,decisions=self.rows()
        latest=next(r for r in rows if r['fixture']==q.SINC and r['bars']==10000 and r['method']=='BuilderLatestOnly')
        latest['old_rounds']={i:dict(mean_ns=95,lower_ns=90,upper_ns=110) for i in ('0','3')}
        latest['candidate_rounds']={i:dict(mean_ns=110,lower_ns=109,upper_ns=111) for i in ('1','2')}
        for row in rows:
            if row['fixture']=='SharedFibonacciWeightedBenchmarks' and row['bars']==10000:
                row['old_rounds']={i:dict(mean_ns=110,lower_ns=109,upper_ns=111) for i in ('0','3')}
        result=q.adjudicate_feasibility(rows,decisions)
        self.assertEqual(len(result['new_10000_sinc_builder_wins']),1)
        self.assertTrue(result['family_gates']['all']['net_10000_point_progress'])
        self.assertTrue(result['family_gates']['alma']['net_10000_point_progress'])
        self.assertFalse(result['family_gates']['sinc']['net_10000_point_progress'])
        self.assertFalse(result['prior_native_win_reopenings'])
        self.assertFalse(result['passed'])

    def test_extra_alma_builder_prior_wins_are_protected_both_sizes(self):
        for bars in (1000,10000):
            rows,decisions=self.rows()
            row=next(r for r in rows if r['fixture']=='SharedAlmaWindowBenchmarks' and r['bars']==bars)
            row['candidate_rounds']['1'].update(mean_ns=110,lower_ns=109,upper_ns=111)
            result=q.adjudicate_feasibility(rows,decisions)
            self.assertFalse(result['passed']);self.assertTrue(result['prior_native_win_reopenings'])

    def test_targeted_hooks_use_combined_authentication(self):
        plan=self.plan()
        with mock.patch.object(q,'validate_scope') as check:targeted.verify_reference(plan);check.assert_called_once_with(plan)
        self.assertEqual(targeted.expected_totals(plan),(190,38))
        with mock.patch.object(verify,'read',return_value=plan),mock.patch.object(q,'collect',return_value={'accepted':False}) as check:
            self.assertFalse(verify.collect(Path('unused'),'targeted')['accepted']);check.assert_called_once()
            with self.assertRaises(AssertionError):verify.collect(Path('unused'),'diagnostic')
        tree=ast.parse(inspect.getsource(pipeline._verify_input_contents))
        branch=next(n for n in ast.walk(tree) if isinstance(n,ast.If) and ast.unparse(n.test)=="plan.get('alma_sinc_combined_qualification')")
        code=compile(ast.fix_missing_locations(ast.Module(body=[ast.If(test=branch.test,body=branch.body,orelse=[])],type_ignores=[])),pipeline.__file__,'exec')
        with mock.patch.object(q,'verify_harness_alias') as alias:exec(code,dict(__package__='hosted_timing',plan=plan,folder=Path('unused'),proof={}));alias.assert_called_once()

    def test_result_keeps_primary_and_builder_wins_separate(self):
        rows,decisions=self.rows();base=dict(accepted=True,case_count=190,decision_count=38,decisions=decisions)
        with mock.patch.object(q.prior.original,'verify_runtime'),mock.patch.object(q.p,'read',return_value=self.plan()),mock.patch.object(verify,'_collect',return_value=base),mock.patch.object(q,'native_rows',return_value=rows):
            result=q.collect(Path('unused'))
        self.assertTrue(result['accepted']);self.assertTrue(result['qualification'])
        self.assertEqual(len(result['qualified_primary_native_wins']),2);self.assertEqual(len(result['qualified_builder_native_wins']),2)
        self.assertEqual(result['lp_afirma_report_row_credit'],0);self.assertEqual(result['closure_credit'],0);self.assertFalse(result['report_imported'])

    def test_workflow_scope_and_two_cold_measurement_limit(self):
        text=(q.p.REPO/'.github/workflows/alma-sinc-combined-qualification.yml').read_text()
        self.assertIn('branches: [perf/alma-sinc-combined-qualification-20261119]',text)
        self.assertIn('paths: [.github/workflows/alma-sinc-combined-qualification.yml]',text)
        self.assertIn("if: github.event_name == 'push'",text)
        self.assertIn('max-parallel: 2',text);self.assertIn('shard: [0, 1]',text)
        self.assertLess(text.index('Fail closed before SDK'),text.index('actions/setup-dotnet'))
        self.assertIn('peer_review_approved',text);self.assertIn('Verify all38',text)

    def test_actual_workflow_python_entrypoint_uses_available_module_interface(self):
        text=(q.p.REPO/'.github/workflows/alma-sinc-combined-qualification.yml').read_text()
        code=re.search(r'python -c "(from hosted_timing import alma_sinc_combined_qualification as q;[^"\r\n]+)"',text)[1]
        plan=self.plan()
        with mock.patch.object(q.p,'read',return_value=plan),mock.patch.object(q,'validate_scope') as validate,mock.patch.object(q,'verify_publication') as publication:
            exec(code,{})
        validate.assert_called_once_with(plan)
        publication.assert_called_once_with(plan,current=True)

if __name__=='__main__':unittest.main()
