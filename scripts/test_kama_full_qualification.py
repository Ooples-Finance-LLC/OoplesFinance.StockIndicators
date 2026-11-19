"""Inventory tests only; no indicator builds, GPU or timing."""
import copy
import json
import unittest
import tempfile
import gzip
from pathlib import Path
from unittest.mock import patch
from contextlib import ExitStack
from hosted_timing import kama_full_qualification as q

class FrozenScopeTests(unittest.TestCase):
    def setUp(self):self.plan=json.loads(q.PLAN.read_bytes())
    def test_exact_370_inventory(self):
        self.assertEqual(q.validate_scope(self.plan,require_ready=False)['decisions'],74)
    def reject(self,mutator):
        plan=copy.deepcopy(self.plan);mutator(plan)
        with self.assertRaises(AssertionError):q.validate_scope(plan,require_ready=False)
    def test_no_missing_case(self):
        self.reject(lambda p:p['targeted_shards'][0]['tasks'][0]['operations'].pop())
    def test_no_extra_runner(self):self.reject(lambda p:p['matrix'].update(max_parallel=2))
    def test_no_false_source(self):self.reject(lambda p:p['sources'].update(successor=q.OLD))
    def test_no_changed_warmups(self):self.reject(lambda p:p['measurement_policy'].update(fixed_invocation_warmups=20000))
    def test_no_missing_protection(self):self.reject(lambda p:p['native_progress_gate'].update(protection_edge_count=38))
    def test_no_weaker_target_gate(self):self.reject(lambda p:p['native_progress_gate'].update(require_net_10000_point_progress=False))
    def test_no_wrong_parameter(self):self.reject(lambda p:p['full_consumer_registry'][0].update(pair_parameter='Operation'))
    def test_no_dropped_size(self):self.reject(lambda p:p['full_consumer_registry'][0].update(sizes=[10000]))
    def test_no_qualification_credit(self):self.reject(lambda p:p.update(production_qualification=True))
    def test_workflow_missing_early_coverage_gate_rejected(self):
        text=(q.REPO/'.github/workflows/kama-full-qualification.yml').read_text()
        with self.assertRaises(AssertionError):q.verify_workflow_entrypoint(text.replace('$plan.supplemental_cpu_coverage.sha256','unbound'))
    def test_three_flags_are_insufficient_without_full_integration(self):
        self.plan.update(inputs_ready=True,parent_review_approved=True,peer_review_approved=True)
        self.plan['input_authentication_complete']=False
        with self.assertRaisesRegex(AssertionError,'HELD'):q.validate_scope(self.plan)
    def test_bound_cpu_sessions_replay_exact_names_and_original_counts(self):
        review=q.verify_cpu_coverage(self.plan)
        self.assertEqual(review['combined_coverage_totals']['baseline']['unit'],179)
        self.assertTrue(review['original_filter_19_expands_to_15_validation_ema_and_4_models_tsi'])

class CompletePreparationPathTests(unittest.TestCase):
    def test_real_prepare_reaches_explicit_alias_without_legacy_variant_harness(self):
        """Stub external artifact/bridge discovery; run actual disk/provenance validators."""
        from hosted_timing import pipeline as p, kama_fused_prefix_roi as prior, fixture_inventory
        plan=json.loads(q.PLAN.read_bytes())
        plan['cpu_test_classes']={label:['A'] for label in ('old','successor','variants')}
        unit=b'<TestRun><UnitTestResult outcome="Passed" testName="A.Check" /></TestRun>'
        gpu=b'<TestRun>'+b''.join(b'<UnitTestResult outcome="Passed" />' for _ in range(100))+b'</TestRun>'
        gpu_receipt=json.dumps(dict(trx_sha256=q.sha(gpu),passed_cases=100)).encode()
        harness=b'authenticated-old-harness';prefix=p.BENCH+'/bin/Release/net10.0/'
        harness_file=prefix+'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
        auth={}
        for key in ('old_candidate','candidate'):
            core=key.encode();bench=harness if key=='old_candidate' else b'candidate-harness'
            checks=dict(passed=1,classes={'A':1},sha256=q.sha(unit))
            blobs={'evidence/unit.trx':unit,'evidence/parity.trx':unit,'blobs/'+q.sha(core):core,'blobs/'+q.sha(bench):bench}
            auth[key]=dict(manifest=dict(runtime_files={harness_file:q.sha(bench)},checks={'unit':checks}),
                production=q.sha(core),closure={},gpu_receipt=gpu_receipt,gpu_trx=gzip.compress(gpu),read=blobs.__getitem__)
            plan[key].update(production_sha256=q.sha(core),actual_harness_sha256=q.sha(bench))
        plan['harness_sha256']=q.sha(harness)
        def checked(folder,description):return auth['old_candidate' if description['source']==q.OLD else 'candidate']
        bridge_raw=b'{}'
        def bridge(folder,scope):
            (folder/'evidence/common-harness-bridge.json').write_bytes(bridge_raw)
            return dict(receipt_sha256=q.sha(bridge_raw),runner_source_pins=scope['runner_source_pins'],
                expected_sha256=scope['worker_bridge_expected']['sha256'],common_runtime_unchanged=True,temporary_core_only_replacement=True)
        def methods(runtime,scope):
            return {fixture:dict(requested=names,available=names) for fixture,names in fixture_inventory.requested_methods(scope).items()}
        with tempfile.TemporaryDirectory(prefix='kama-prepare-path-') as temp,ExitStack() as stack:
            root=Path(temp);plan_path=root/'plan.json';p.write(plan_path,plan)
            old=root/'old';new=root/'new';old.mkdir();new.mkdir();out=root/'out'
            stack.enter_context(patch.object(p,'PLAN',plan_path))
            stack.enter_context(patch.object(q,'validate_scope',return_value={}))
            stack.enter_context(patch.object(q,'verify_cpu_coverage',return_value={'actual_sessions':'separately-tested'}))
            stack.enter_context(patch.object(prior,'verify_publication',return_value={'accepted_source':q.OLD}))
            stack.enter_context(patch.object(prior,'checked_inputs',side_effect=checked))
            stack.enter_context(patch.object(prior,'verify_source_boundary'))
            stack.enter_context(patch.object(prior,'common_harness_bridge',side_effect=bridge))
            stack.enter_context(patch.object(prior,'verify_bootstrap_evidence'))
            stack.enter_context(patch.object(prior,'verify_bridge_record'))
            stack.enter_context(patch.object(p,'git',return_value='a'*40))
            stack.enter_context(patch.object(q.subprocess,'check_output',side_effect=lambda args,**kw:'10.0.403\n' if '--version' in args else 'pinned SDK info'))
            from hosted_timing import runtime_dependencies
            stack.enter_context(patch.object(runtime_dependencies,'verify_input_alignment'))
            stack.enter_context(patch.object(fixture_inventory,'inspect_methods',side_effect=methods))
            stack.enter_context(patch.dict(q.os.environ,dict(GITHUB_EVENT_NAME='push',GITHUB_REF='refs/heads/perf/kama-full-qualification-20261119',GITHUB_RUN_ID='1',GITHUB_RUN_ATTEMPT='1')))
            alias=stack.enter_context(patch.object(prior,'verify_harness_alias',wraps=prior.verify_harness_alias))
            proof=q.prepare(old,new,out)
            self.assertGreaterEqual(alias.call_count,2)
            self.assertFalse((out/'evidence/variant-harness.trx').exists())
            self.assertEqual(proof['preparation_mode'],'authenticated-kama-full-qualification')
            self.assertEqual(len(json.loads((out/'case-selection-inventory.json').read_bytes())),370)

class FullGateTests(unittest.TestCase):
    def setUp(self):
        self.plan=json.loads(q.PLAN.read_bytes());self.rows=[];self.decisions=[]
        def sample(mean):return dict(mean_ns=mean,lower_ns=mean-1,upper_ns=mean+1)
        for fixture,pair,bars,method in sorted(q.expected_contexts(self.plan)):
            native=['Competitor'] if fixture=='LibraryPairBenchmarks' else ['NativeSingle','NativeParallel']
            identity=dict(fixture=fixture,pair=pair,bars=bars,method=method)
            self.rows.append(dict(**identity,old_rounds={str(i):sample(120) for i in (0,3)},
                candidate_rounds={str(i):sample(80) for i in (1,2)},native_controls={m:sample(100) for m in native}))
            self.decisions.append(dict(**identity,verdict='faster'))
    def test_all_scope_and_original_kama_gate_pass(self):
        result=q.adjudicate_feasibility(self.rows,self.decisions,self.plan)
        self.assertTrue(result['passed']);self.assertEqual(result['historical_protection_edges_covered'],41)
    def test_builder_source_regression_rejects(self):
        next(d for d in self.decisions if d['method']=='BuilderLatestOnly')['verdict']='slower'
        self.assertFalse(q.adjudicate_feasibility(self.rows,self.decisions,self.plan)['passed'])
    def test_one_native_control_reopening_rejects(self):
        row=next(r for r in self.rows if r['fixture']=='SharedWeightedWindowBenchmarks' and r['method']=='BuilderFull')
        for sample in row['old_rounds'].values():sample.update(mean_ns=70,lower_ns=69,upper_ns=71)
        for sample in row['candidate_rounds'].values():sample.update(mean_ns=110,lower_ns=109,upper_ns=111)
        self.assertFalse(q.adjudicate_feasibility(self.rows,self.decisions,self.plan)['passed'])
    def test_historical_edge_reopening_rejects_when_fresh_old_cis_overlap(self):
        row=next(r for r in self.rows if r['fixture']=='LibraryPairBenchmarks' and r['pair']=='QuanTAlib.Alma' and r['bars']==1000)
        for sample in row['old_rounds'].values():sample.update(mean_ns=99,lower_ns=90,upper_ns=110)
        for sample in row['candidate_rounds'].values():sample.update(mean_ns=101,lower_ns=95,upper_ns=107)
        next(d for d in self.decisions if d['fixture']==row['fixture'] and d['pair']==row['pair'] and d['bars']==1000)['verdict']='inconclusive'
        result=q.adjudicate_feasibility(self.rows,self.decisions,self.plan)
        self.assertFalse(result['passed'])
        reopened=next(e for e in result['prior_native_win_reopenings'] if e['fixture']==row['fixture'] and e['pair']==row['pair'] and e['bars']==1000)
        self.assertTrue(reopened['historical_edge']);self.assertFalse(reopened['fresh_old_ci_edge'])
    def test_historical_and_fresh_edge_reported_once(self):
        row=next(r for r in self.rows if r['fixture']=='LibraryPairBenchmarks' and r['pair']=='QuanTAlib.Alma' and r['bars']==1000)
        for sample in row['old_rounds'].values():sample.update(mean_ns=70,lower_ns=69,upper_ns=71)
        result=q.adjudicate_feasibility(self.rows,self.decisions,self.plan)
        edges=[e for e in result['protected_prior_native_wins'] if e['fixture']==row['fixture'] and e['pair']==row['pair'] and e['bars']==1000]
        self.assertEqual(len(edges),1);self.assertTrue(edges[0]['historical_edge']);self.assertTrue(edges[0]['fresh_old_ci_edge'])

if __name__=='__main__':unittest.main()
