"""Bounded orchestration checks; no production build, workload, capture or timing."""
import copy,json,unittest
import os
import shutil
from pathlib import Path
from unittest import mock
from hosted_timing import kama_fused_prefix_roi as q
from hosted_timing import pipeline,roi_scope_dispatch,verify

SAVED_PLAN=Path(__file__).resolve().parents[1]/'benchmarks/plans/hosted-kama-fused-prefix-roi-v2-20261119.json'
PLAN=Path(os.environ.get('HOSTED_TIMING_PLAN',str(SAVED_PLAN)))
if not PLAN.is_absolute():PLAN=SAVED_PLAN.parents[2]/PLAN

class KamaFusedPrefixRoiTests(unittest.TestCase):
    def plan(self):return json.loads(PLAN.read_bytes())
    def held_plan(self):
        plan=copy.deepcopy(self.plan());plan['inputs_ready']=False;plan['parent_review_approved']=False
        return plan
    def rows(self):
        sample=lambda mean:dict(mean_ns=mean,lower_ns=mean-1,upper_ns=mean+1)
        rows=[];decisions=[]
        for pair in q.PAIRS:
            for bars in q.COUNTS:
                target=pair==q.TARGETS[0] and bars==10000
                rows.append(dict(fixture=q.FIXTURE,pair=pair,bars=bars,method='Ooples',
                    old_rounds={k:sample(110 if target else 70) for k in ('0','3')},
                    candidate_rounds={k:sample(95 if target else 70) for k in ('1','2')},
                    native_controls={'Competitor':sample(100)}))
                decisions.append(dict(fixture=q.FIXTURE,pair=pair,bars=bars,method='Ooples',verdict='faster' if target else 'inconclusive'))
        return rows,decisions
    def bridge(self):
        plan=self.plan();bundle=json.loads(q.evidence(plan['worker_bridge_expected']))
        return dict(status='common-harness-four-adapter-preflight-passed',source=q.NEW,actual_clr='10.0.15',
            core_sha256=plan['candidate']['production_sha256'],harness_sha256=plan['harness_sha256'],
            cpu_engine='AiDotNet.Tensors.Engines.CpuEngine',default_backend='Auto',default_history='Full',default_workers=2,
            setup_calls=4,explicit_ooples_calls=4,builds=0,profiles=0,warmups=0,timings=0,qualification=False,performance_claim=False,
            contexts=[dict(pair=pair,bars=10000,period=20,factory=bundle[pair]['ready']['actual_factory'],
                parameters=bundle[pair]['ready']['actual_indicator_parameters'],actual_data_bits=bundle[pair]['ready']['actual_data_bits'],
                full_returned_output_hash=json.loads(bundle[pair]['ready']['full_returned_output_hash']),
                presence_sha256=json.loads(bundle[pair]['ready']['presence_sha256'])) for pair in q.KAMA])
    def test_exact_held_scope(self):
        p=self.held_plan();q.validate_scope(p,require_ready=False)
        self.assertIs(p['inputs_ready'],False);self.assertIs(p['parent_review_approved'],False)
        self.assertEqual((p['cases'],p['decisions']),(60,12));self.assertEqual(p['targeted_shards'],q.shards())
        self.assertEqual([sum(r['round']==i for r in q.case_inventory()) for i in range(4)],[12,12,24,12])
        self.assertNotIn(b'\r',PLAN.read_bytes())
    def test_held_before_sdk_or_inputs(self):
        with mock.patch.object(q.p,'read',return_value=self.held_plan()),mock.patch.object(q.subprocess,'check_output',side_effect=RuntimeError('forbidden')) as io,mock.patch.object(q,'checked_inputs') as inputs:
            with self.assertRaisesRegex(AssertionError,'HELD'):q.prepare('old','new','out')
            io.assert_not_called();inputs.assert_not_called()
    def test_frozen_matrix_and_policy(self):
        for mutate in [lambda p:p.update(cases=50),lambda p:p.update(decisions=10),lambda p:p['matrix'].update(max_parallel=2),
                       lambda p:p['native_progress_gate'].update(minimum_margin=.1),lambda p:p.update(report_import_permitted=True),
                       lambda p:p['targeted_shards'][0]['tasks'][0]['operations'].pop(),lambda p:p['measurement_policy'].update(iterations=9),
                       lambda p:p['bridge_policy'].update(warmups=1000),lambda p:p['harness_alias_policy'].pop('excluded_sibling_test')]:
            p=self.plan();mutate(p)
            with self.subTest(mutate=mutate),self.assertRaises(AssertionError):q.validate_scope(p,require_ready=False)
    def test_source_boundary_and_all_six_registry_paths(self):q.verify_source_boundary();q.verify_registry()
    def test_root_cpu_gpu_and_four_actual_worker_bindings(self):q.verify_root_evidence(self.plan())
    def test_evidence_mutation_fails(self):
        for field in ('worker_proof_root_review','worker_proof_index','worker_proof_helper','worker_bridge_expected','model_input'):
            p=self.plan();p[field]['sha256']='0'*64
            with self.subTest(field=field),self.assertRaises(AssertionError):q.verify_root_evidence(p)
    def test_actual_common_harness_bridge_contract(self):q.verify_bridge_record(self.plan(),self.bridge())
    def test_bridge_wrong_harness_source_outputs_masks_or_calls_fails(self):
        mutations=[lambda r:r.update(harness_sha256='25a4b8834ea5a0334ff4cdc61d10e87048b57e05c28c32c9dfe22f97d8cf9497'),
            lambda r:r.update(source=q.OLD),lambda r:r.update(warmups=1),lambda r:r.update(explicit_ooples_calls=5),
            lambda r:r['contexts'].pop(),lambda r:r['contexts'][0]['actual_data_bits'].update(Closes='0'*64),
            lambda r:r['contexts'][0]['full_returned_output_hash'].update(Kama='0'*64),
            lambda r:r['contexts'][1]['presence_sha256'].update(ER='0'*64),lambda r:r.update(default_history='LatestOnly')]
        for mutate in mutations:
            r=self.bridge();mutate(r)
            with self.subTest(mutate=mutate),self.assertRaises(AssertionError):q.verify_bridge_record(self.plan(),r)
    def test_one_eligible_separated_target_crossover_suffices(self):
        rows,ds=self.rows();result=q.adjudicate_feasibility(rows,ds)
        self.assertTrue(result['passed']);self.assertEqual(len(result['new_10000_target_wins']),1)
        self.assertEqual(result['closure_credit'],0);self.assertFalse(result['qualification'])
    def test_target_own_source_faster_required(self):
        rows,ds=self.rows();next(d for d in ds if d['pair']==q.TARGETS[0] and d['bars']==10000)['verdict']='inconclusive'
        self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
    def test_any_control_regression_fails(self):
        for pair in q.PAIRS[2:]:
            rows,ds=self.rows();next(d for d in ds if d['pair']==pair and d['bars']==1000)['verdict']='slower'
            self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
    def test_both_candidate_rounds_must_separate(self):
        rows,ds=self.rows();next(r for r in rows if r['pair']==q.TARGETS[0] and r['bars']==10000)['candidate_rounds']['2']['upper_ns']=100
        self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
    def test_prior_native_winner_reopening_fails(self):
        rows,ds=self.rows();next(r for r in rows if r['pair']==q.KAMA[3] and r['bars']==1000)['candidate_rounds']['1']['mean_ns']=101
        self.assertFalse(q.adjudicate_feasibility(rows,ds)['passed'])
    def test_missing_context_rejected(self):
        rows,ds=self.rows()
        with self.assertRaises(AssertionError):q.adjudicate_feasibility(rows[:-1],ds[:-1])
    def test_dispatch_hooks_and_narrow_single_push_workflow(self):
        for module in (pipeline,roi_scope_dispatch,verify):
            self.assertIn('kama_fused_prefix_roi',Path(module.__file__).read_text())
        workflow=(SAVED_PLAN.parents[2]/'.github/workflows/kama-fused-prefix-roi-v2.yml').read_text()
        q.verify_workflow_entrypoint(workflow)
        self.assertNotIn('workflow_dispatch:',workflow)
    def test_entrypoint_checks_are_flag_independent(self):
        workflow=(SAVED_PLAN.parents[2]/'.github/workflows/kama-fused-prefix-roi-v2.yml').read_text()
        for mutate in [lambda t:t.replace('  push:','  workflow_dispatch:'),lambda t:t.replace(q.WORKFLOW_BRANCH,'perf/other'),
            lambda t:t.replace("github.event_name == 'push'","github.event_name == 'workflow_dispatch'"),
            lambda t:t.replace('!$plan.inputs_ready','false'),lambda t:t.replace('!$plan.parent_review_approved','false')]:
            with self.subTest(mutate=mutate),self.assertRaises(AssertionError):q.verify_workflow_entrypoint(mutate(workflow))
        for event,ref in [('workflow_dispatch','refs/heads/'+q.WORKFLOW_BRANCH),('push','refs/heads/other')]:
            with self.subTest(event=event,ref=ref),self.assertRaises(AssertionError):q.verify_entrypoint_environment(dict(GITHUB_EVENT_NAME=event,GITHUB_REF=ref))
        q.verify_entrypoint_environment(dict(GITHUB_EVENT_NAME='push',GITHUB_REF='refs/heads/'+q.WORKFLOW_BRANCH))
    def deps(self):
        pack='runtimepack.Microsoft.NETCore.App.Runtime.win-x64/10.0.14'
        return dict(runtimeTarget=dict(name='.NETCoreApp,Version=v10.0/win-x64'),libraries={pack:dict(type='runtimepack'),
            'PowerShell/7.6.0':dict(type='project'),'runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.14':dict(type='runtimepack')},
            targets={'.NETCoreApp,Version=v10.0/win-x64':{pack:dict(runtime={'System.Private.CoreLib.dll':{}},native={'coreclr.dll':{},'hostpolicy.dll':{}}),
                'PowerShell/7.6.0':dict(runtime={'pwsh.dll':{}},dependencies={pack.rsplit('/',1)[0]:'10.0.14','Other':'1.0'}),
                'runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.14':dict(runtime={'System.Windows.Forms.dll':{}})}})
    def test_v2_normalizes_only_core_runtimepack(self):
        d=self.deps();raw=json.dumps(d).encode();normalized,proof=q.framework_deps(raw);n=json.loads(normalized)
        self.assertEqual(proof['target_major'],10);self.assertTrue(proof['other_components_preserved'])
        self.assertFalse(proof['forbidden_runtime_assets_remaining']);self.assertEqual(len(proof['removed_keypaths']),3)
        self.assertEqual(n['runtimeTarget'],d['runtimeTarget'])
        self.assertIn('runtimepack.Microsoft.WindowsDesktop.App.Runtime.win-x64/10.0.14',n['libraries'])
        self.assertEqual(n['targets'][d['runtimeTarget']['name']]['PowerShell/7.6.0']['runtime'],{'pwsh.dll':{}})
        self.assertEqual(n['targets'][d['runtimeTarget']['name']]['PowerShell/7.6.0']['dependencies'],{'Other':'1.0'})
        d['targets']['.NETCoreApp,Version=v10.0']={}
        normalized,proof=q.framework_deps(json.dumps(d).encode())
        self.assertEqual(json.loads(normalized)['targets']['.NETCoreApp,Version=v10.0'],{})
    def test_v2_rejects_wrong_major_or_residual_core_assets(self):
        for major in (8,9,11):
            d=self.deps();d['runtimeTarget']['name']=d['runtimeTarget']['name'].replace('v10.0','v'+str(major)+'.0')
            with self.subTest(major=major),self.assertRaisesRegex(AssertionError,'target major'):q.framework_deps(json.dumps(d).encode())
        for asset in ('System.Private.CoreLib.dll','coreclr.dll','hostpolicy.dll'):
            d=self.deps();d['targets'][d['runtimeTarget']['name']]['PowerShell/7.6.0']['runtime'][asset]={}
            with self.subTest(asset=asset),self.assertRaisesRegex(AssertionError,'runtime asset'):q.framework_deps(json.dumps(d).encode())
    def test_v2_real_local_deps_structure_only(self):
        raw=Path(shutil.which('pwsh')).resolve().with_name('pwsh.deps.json').read_bytes()
        normalized,proof=q.framework_deps(raw)
        self.assertEqual(q.sha(normalized),proof['normalized_sha256'])
        self.assertEqual(q.sha(raw),proof['original_sha256'])

if __name__=='__main__':unittest.main()
