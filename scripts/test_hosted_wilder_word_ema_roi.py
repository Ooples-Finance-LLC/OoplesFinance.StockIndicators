import copy
import hashlib
import importlib.util
import json
import runpy
import sys
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p, wilder_word_ema_roi as roi, verify

PLAN = p.REPO / 'benchmarks/plans/hosted-wilder-word-ema-roi-20261116.json'


class RoiDiagnosticTests(unittest.TestCase):
    def test_legacy_checks_pin_their_plan_without_changing_measurement_plan(self):
        workflow = (p.REPO / '.github/workflows/cpu-kernel-performance.yml').read_text()
        job = workflow.split('  bounded-wilder-roi-diagnostic:\n', 1)[1]
        check = job.split('      - name: Verify bounded orchestration\n', 1)[1].split('      - uses:', 1)[0]
        self.assertIn('        env:\n          HOSTED_TIMING_PLAN: benchmarks/plans/hosted-rollout-20261101.json\n', check)
        measurement = job.split('      - name: Measure thirty approved Wilder diagnostic cases\n', 1)[1].split('      - name:', 1)[0]
        self.assertNotIn('HOSTED_TIMING_PLAN:', measurement)

    def test_pinned_json_hashes_survive_windows_checkout_filters(self):
        for path, expected in ((PLAN, 'e67b3daa7646bc0ea427031fb047e1f0370f1cbc8083f744bc4cead2ec6ce8be'),
                               (p.REPO / roi.APPROVAL, roi.APPROVAL_SHA),
                               (p.REPO / roi.BASE_PLAN, roi.BASE_PLAN_SHA)):
            raw = subprocess.check_output(['git', '-c', 'core.autocrlf=true', '-c', 'core.eol=crlf',
                'cat-file', '--filters', 'HEAD:' + path.relative_to(p.REPO).as_posix()], cwd=p.REPO)
            self.assertEqual(hashlib.sha256(raw).hexdigest(), expected)
            self.assertNotIn(b'\r\n', raw)

    def test_relative_cli_measure_paths_are_absolute_before_runtime_changes_cwd(self):
        expected_inputs=(Path.cwd()/'relative-inputs').resolve()
        expected_out=(Path.cwd()/'relative-output/shard0').resolve()
        with patch.object(sys,'argv',['run-wilder-word-ema-roi.py','measure','--inputs','relative-inputs','--out','relative-output/shard0']), \
             patch.object(roi,'validate_scope'),patch.object(p,'verify_inputs') as inputs,patch.object(p,'measure') as measure:
            runpy.run_path(str(p.REPO/'scripts/run-wilder-word-ema-roi.py'),run_name='__main__')
        inputs.assert_called_once_with(expected_inputs)
        measure.assert_called_once_with(expected_inputs,expected_out,'diagnostic',0)

    def test_partial_clone_prefetches_exact_blobs_with_binary_lf_stdin(self):
        a,b='1'*40,'2'*40
        entries=f'100644 blob {b}\tsrc/b.cs\n100644 blob {a}\tsrc/a.cs\n100644 blob {b}\tsrc/c.cs'
        promisor=subprocess.CompletedProcess([],0,stdout='true\n')
        with patch.object(p,'git',return_value=entries),patch.object(roi.subprocess,'run',return_value=promisor) as run:
            roi.prefetch_source_blobs('source')
        self.assertEqual(run.call_count,2)
        self.assertEqual(run.call_args.kwargs['input'],(a+'\n'+b+'\n').encode())
        self.assertTrue(run.call_args.kwargs['check'])
        self.assertIn('--no-write-fetch-head',run.call_args.args[0])

    def test_complete_clone_does_not_need_network_prefetch(self):
        with patch.object(roi.subprocess,'run',return_value=subprocess.CompletedProcess([],1,stdout='')) as run:
            roi.prefetch_source_blobs('source')
        self.assertEqual(run.call_count,1)

    def evidence(self, root):
        with patch.object(test_hosted_timing, 'PLAN', PLAN):
            test_hosted_timing.make_evidence(root, 'diagnostic')

    def test_complete_thirty_cases_never_qualify_even_when_all_faster(self):
        with tempfile.TemporaryDirectory() as work, patch.object(p, 'PLAN', PLAN), patch.object(verify, 'PLAN', PLAN):
            root = Path(work)
            self.evidence(root)
            result = roi.collect(root)
            self.assertEqual((30, 6), (result['case_count'], result['decision_count']))
            self.assertEqual({'faster': 6}, result['verdicts'])
            self.assertFalse(result['accepted'])
            self.assertTrue(result['diagnostic_only'])
            self.assertFalse(result['qualification'])
            self.assertFalse(result['production_integrated'] or result['report_imported'] or result['policy_changed'])
            with self.assertRaisesRegex(AssertionError, 'cannot qualify'):
                verify.collect(root, 'diagnostic')
            with self.assertRaisesRegex(AssertionError, 'cannot qualify'):
                verify.collect(root, 'successor')

    def test_missing_or_duplicate_shard_is_not_a_diagnostic_result(self):
        import shutil
        with tempfile.TemporaryDirectory() as work, patch.object(p, 'PLAN', PLAN), patch.object(verify, 'PLAN', PLAN):
            root = Path(work)
            self.evidence(root)
            shutil.copytree(root / 'shard0', root / 'shard1')
            with self.assertRaises(AssertionError): roi.collect(root)

    def test_wrong_host_cannot_be_pooled(self):
        with tempfile.TemporaryDirectory() as work, patch.object(p, 'PLAN', PLAN), patch.object(verify, 'PLAN', PLAN):
            root = Path(work)
            self.evidence(root)
            proof = p.read(root / 'shard0/provenance.json')
            proof['host']['run_id'] = 'another-run'
            p.write(root / 'shard0/provenance.json', proof)
            with self.assertRaises(AssertionError): roi.collect(root)

    def test_cpu_gate_stays_strict_in_diagnostic_phase(self):
        with tempfile.TemporaryDirectory() as work, patch.object(p, 'PLAN', PLAN), patch.object(verify, 'PLAN', PLAN):
            root = Path(work)
            self.evidence(root)
            path = root / 'shard0/status.json'
            state = p.read(path)
            state['rounds'][0]['operation_checkpoints'][0]['quiet_cpu_samples'][0] = 15.01
            p.write(path, state)
            with self.assertRaises(AssertionError): roi.collect(root)

    def test_scope_cannot_expand_change_sources_or_relax_policy(self):
        for mutation in ('cases', 'runner', 'pair', 'method', 'source', 'warmup', 'cpu', 'iterations', 'runtime', 'inventory'):
            plan = copy.deepcopy(p.read(PLAN))
            if mutation == 'cases': plan['cases'] += 10
            elif mutation == 'runner': plan['matrix']['max_parallel'] = 2
            elif mutation == 'pair': plan['diagnostic_shards'][0]['tasks'][0]['operations'][0]['pair'] = 'unknown'
            elif mutation == 'method': plan['diagnostic_shards'][0]['tasks'][0]['methods'].append('BuilderFull')
            elif mutation == 'source': plan['sources']['successor'] = roi.OLD
            elif mutation == 'warmup': plan['measurement_policy']['fixed_invocation_warmups'] = 20000
            elif mutation == 'cpu': plan['measurement_policy']['max_cpu_percent'] = 16
            elif mutation == 'iterations': plan['measurement_policy']['iterations'] = 9
            elif mutation == 'runtime': plan['runtime_policy_overrides'] = {'DOTNET_TieredPGO': '0'}
            else: plan['require_fixture_inventory'] = False
            with self.subTest(mutation=mutation), self.assertRaises(AssertionError): roi.validate_scope(plan)

    def test_changed_approval_bytes_are_rejected(self):
        with patch.object(p, 'digest', return_value='changed'):
            with self.assertRaises(AssertionError): roi.validate_scope(p.read(PLAN))

    def test_changed_candidate_manifest_is_rejected_before_blob_use(self):
        with tempfile.TemporaryDirectory() as work, patch.object(roi, 'source_fingerprint', return_value='source-fingerprint'):
            root = Path(work)
            (root / 'manifest.json').write_bytes(b'{}')
            with self.assertRaises(AssertionError): roi.candidate_inputs(root, p.read(PLAN))

    def test_another_ci_run_cannot_supply_candidate(self):
        with tempfile.TemporaryDirectory() as work, patch.object(roi, 'source_fingerprint', return_value='source-fingerprint'):
            root = Path(work)
            raw = json.dumps(dict(run_id='another-run', workflow_head=roi.PRODUCER)).encode()
            (root / 'manifest.json').write_bytes(raw)
            plan = copy.deepcopy(p.read(PLAN))
            plan['candidate']['manifest_sha256'] = hashlib.sha256(raw).hexdigest()
            with self.assertRaises(AssertionError): roi.candidate_inputs(root, plan)

    def test_git_tree_fingerprint_matches_existing_gpu_algorithm(self):
        original_repo = p.REPO
        spec = importlib.util.spec_from_file_location('roi_gpu_fingerprint_test', original_repo / 'scripts/verify-competitor-local-gpu.py')
        gpu = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(gpu)
        with tempfile.TemporaryDirectory(prefix='si-roi-source-') as work:
            root = Path(work).resolve()
            assert root.parent == Path(tempfile.gettempdir()).resolve()
            (root / 'src').mkdir()
            (root / 'src/state.cs').write_bytes(b'class State {}\r\n')
            (root / 'src/binary.dat').write_bytes(b'\0binary\r\n')
            (root / 'global.json').write_bytes(b'{}\n')
            for args in (['init', '-q'], ['config', 'user.name', 'ROI test'], ['config', 'user.email', 'roi-test@example.invalid'],
                         ['add', '.'], ['commit', '-qm', 'fixture']):
                subprocess.run(['git', *args], cwd=root, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
            source = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
            def git(*args):
                return subprocess.check_output(['git', *args], cwd=root, text=True).strip()
            expected = gpu.source_fingerprint(root)
            with patch.object(p, 'REPO', root), patch.object(p, 'git', side_effect=git):
                self.assertEqual(expected, roi.source_fingerprint(source))


if __name__ == '__main__':
    unittest.main()
