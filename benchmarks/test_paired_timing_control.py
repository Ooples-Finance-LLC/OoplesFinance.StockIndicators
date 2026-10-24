import copy
import gzip
import hashlib
import json
from pathlib import Path
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import run_paired_batch_timing as runner
from run_paired_batch_timing import DLL, activity, contained, passed_trx, refuse_competing_runner, verify_snapshot
from test_paired_batch_timing import report


def observation(time, cpu=0):
    return dict(time=time, processes={'1': dict(created=10, cmd=['dotnet', 'service'], cpu=cpu)})


def trx(classes):
    names = [f'Tests.{name}.Case{i}' for name, count in classes.items() for i in range(count)]
    return ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'  # NOSONAR(S5332) XML namespace only.
            + ''.join(f'<UnitTestResult testName="{name}" outcome="Passed"/>' for name in names)
            + f'</Results><ResultSummary outcome="Completed"><Counters total="{len(names)}" executed="{len(names)}" passed="{len(names)}"/>'
            '</ResultSummary></TestRun>').encode()


class PairedTimingControlTests(unittest.TestCase):
    def test_quiet_threshold_is_cumulative_and_interval_based(self):
        self.assertFalse(activity(observation(0), observation(240, 2.5)))
        self.assertTrue(activity(observation(0), observation(240, 2.51)))
        self.assertTrue(activity(observation(230, 2), observation(240, 2.3)))

    def test_process_churn_identity_and_bad_clock_are_uncertain(self):
        first = observation(0)
        for current in [dict(time=10, processes={}), observation(0), observation(-1), observation(10, -1)]:
            self.assertTrue(activity(first, current))
        for field, value in [('created', 11), ('cmd', ['dotnet', 'different'])]:
            current = observation(10); current['processes']['1'][field] = value
            self.assertTrue(activity(first, current))
        current = observation(10); current['processes']['2'] = current['processes']['1']
        self.assertTrue(activity(first, current))

    def test_existing_competitor_retry_or_paired_runner_is_refused(self):
        for script in ['Z:/StockIndicators-work/si-competitor-refresh-20261021/retry-after-unheld-quiet.py',
                       'benchmarks/run_paired_batch_timing.py']:
            process = SimpleNamespace(pid=-1, info={'cmdline': ['python', script]})
            with self.assertRaises(RuntimeError):
                refuse_competing_runner(SimpleNamespace(process_iter=lambda _: [process]))
        process = SimpleNamespace(pid=-1, info={'cmdline': ['dotnet', 'unrelated-service.dll']})
        refuse_competing_runner(SimpleNamespace(process_iter=lambda _: [process]))

    def snapshot(self, root):
        names = [DLL, 'qualified-working-source.zip', 'evidence/unit.trx', 'evidence/parity.trx',
                 'evidence/gpu-receipt.json', 'evidence/gpu-gpu.trx.gz']
        hashes = {}
        gpu_raw = trx({'Gpu': 100})
        contents = {'evidence/unit.trx': trx({'RsmkBatchTests': 153, 'PairedNumericalTests': 1, 'PairedInputContractTests': 1}),
                    'evidence/parity.trx': trx({'RsmkBatchBenchmarkTests': 3}),
                    'evidence/gpu-gpu.trx.gz': gzip.compress(gpu_raw),
                    'evidence/gpu-receipt.json': json.dumps({'passed_cases': 100, 'trx_sha256': hashlib.sha256(gpu_raw).hexdigest()}).encode()}
        for name in names:
            path = root / name; path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(contents.get(name, name.encode())); hashes[name] = hashlib.sha256(path.read_bytes()).hexdigest()
        manifest = dict(status='correctness-qualified-performance-pending', qualified_families=['rsmk'], files_sha256=hashes)
        (root / 'manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
        pending = dict(status='correctness-qualified-dedicated-timing-pending', snapshot=str(root),
                       fixture='RsmkBatchBenchmarks', operations=['RSMKIndicator'],
                       sizes=[1000, 10000], methods=['Legacy', 'Shared'])
        return pending, manifest

    def test_failed_skipped_incomplete_or_wrong_class_receipts_are_rejected(self):
        raw = trx({'Needed': 2})
        for changed in [raw.replace(b'outcome="Passed"', b'outcome="Failed"', 1),
                        raw.replace(b'outcome="Passed"', b'outcome="NotExecuted"', 1),
                        raw.replace(b'executed="2"', b'executed="1"'),
                        raw.replace(b'outcome="Completed"', b'outcome="Aborted"'), trx({'Other': 2})]:
            with self.assertRaises(ValueError): passed_trx(changed, {'Needed': 2})

    def test_snapshot_source_binary_and_evidence_hashes_are_checked(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); pending, manifest = self.snapshot(root)
            self.assertEqual(root.resolve(), verify_snapshot(pending)[0])
            for name in manifest['files_sha256']:
                path = root / name; original = path.read_bytes(); path.write_bytes(b'changed')
                with self.assertRaisesRegex(ValueError, 'hash mismatch'): verify_snapshot(pending)
                path.write_bytes(original)

    def test_incomplete_unqualified_or_different_workload_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder); pending, manifest = self.snapshot(root)
            for field, value in [('status', 'prepared'), ('fixture', 'Other'), ('operations', ['Other']),
                                 ('sizes', [1000]), ('methods', ['Legacy'])]:
                changed = copy.deepcopy(pending); changed[field] = value
                with self.assertRaises(ValueError): verify_snapshot(changed)
            del manifest['files_sha256']['evidence/unit.trx']
            (root / 'manifest.json').write_text(json.dumps(manifest), encoding='utf-8')
            with self.assertRaisesRegex(ValueError, 'Missing snapshot'): verify_snapshot(pending)

    def test_snapshot_paths_cannot_escape(self):
        with tempfile.TemporaryDirectory() as folder:
            for name in ['../outside', '.', str(Path(folder).parent / 'outside')]:
                with self.assertRaises(ValueError): contained(Path(folder), name)

    def test_execution_only_publishes_complete_uncontaminated_unchanged_inputs(self):
        for outcome in ['valid', 'contaminated', 'changed-input', 'failed', 'incomplete']:
            with self.subTest(outcome=outcome), tempfile.TemporaryDirectory() as folder:
                root = Path(folder); snapshot = root / 'snapshot'; snapshot.mkdir()
                pending, _ = self.snapshot(snapshot)
                path = root / 'pending.json'; path.write_text(json.dumps(pending), encoding='utf-8')
                runtime = root / 'dotnet.exe'; runtime.write_bytes(b'fake runtime, never executed')
                output = root / 'run'

                def fake_process(command, **kwargs):
                    result = output / 'artifacts/results/test-report-full-compressed.json'
                    result.parent.mkdir(parents=True)
                    data = report('RsmkBatchBenchmarks')
                    if outcome == 'incomplete': data['Benchmarks'].pop()
                    result.write_text(json.dumps(data), encoding='utf-8')
                    if outcome == 'changed-input': (snapshot / DLL).write_bytes(b'changed')
                    code = 1 if outcome == 'failed' else 0
                    return SimpleNamespace(pid=999, poll=lambda: code, wait=lambda: code)

                samples = [observation(0), observation(240), observation(250),
                           observation(260, 1 if outcome == 'contaminated' else 0)]
                arguments = ['runner', '--pending', str(path), '--output', str(output), '--dotnet', str(runtime)]
                with patch('sys.argv', arguments), patch.object(runner, 'refuse_competing_runner'), \
                     patch.object(runner, 'sample', side_effect=samples), patch.object(runner.time, 'sleep'), \
                     patch.object(runner.subprocess, 'Popen', side_effect=fake_process) as start:
                    if outcome == 'valid':
                        runner.main()
                        receipt = json.loads((output / 'receipt.json').read_text(encoding='utf-8'))
                        self.assertEqual(2, len(receipt['rows']))
                        self.assertEqual('qualified-full-batch-comparison', receipt['status'])
                        self.assertEqual(hashlib.sha256((output / 'quiet.json').read_bytes()).hexdigest(), receipt['quiet_sha256'])
                        self.assertIn('no competitor comparison', receipt['comparison'])
                    else:
                        with self.assertRaises(ValueError): runner.main()
                        self.assertFalse((output / 'receipt.json').exists())
                        self.assertEqual('failed-needs-inspection', json.loads((output / 'status.json').read_text(encoding='utf-8'))['status'])
                    self.assertEqual(1, start.call_count)


if __name__ == '__main__':
    unittest.main()
