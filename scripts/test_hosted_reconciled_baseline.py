"""Reject unverified source or dependency changes in the reconciled baseline path."""
import copy
import hashlib
import json
import unittest
from unittest.mock import patch

from hosted_timing import reconciled_baseline as check


class ReconciledBaselineTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        source = '376c0ef71e4dad1cef14330d3a6d989d19de729e'
        cls.project = check.blob(source, check.PROJECT).replace(b'\r\n', b'\n')
        cls.evidence = json.loads(check.blob(source,
            'benchmarks/results/remaining-indicators/combined-ratio-klinger-20261104/verification.json'))
        cls.cpu = {name: check.blob(source,
            'benchmarks/results/remaining-indicators/combined-ratio-klinger-20261104/' + name)
            for name in ('unit.trx.gz', 'parity.trx.gz')}
        cls.cpu['final-workflow-state.json'] = json.dumps(dict(status='completed', conclusion='success',
            headSha=cls.evidence['build_origin']['workflow_head'])).encode()
        cls.gpu = {name: check.blob(source, name) for name in (
            check.GPU + 'receipt.json', check.GPU + 'gpu.trx.gz', 'scripts/competitor-gpu-methods.json')}

    def setUp(self):
        self.record = copy.deepcopy(self.evidence)
        self.reference = dict(qualified_source='qualified', dependency_reconciliation=dict(
            kind='master-tensors-0.144.0-to-0.146.0', timings_reused=False,
            evidence='proof/evidence.json', sha256=''))
        self.after = self.project.replace(check.OLD, check.NEW)
        self.changes = [check.PROJECT]
        self.final = self.cpu['final-workflow-state.json']

    def run_check(self, refresh_hash=True):
        raw = json.dumps(self.record).encode()
        if refresh_hash:
            self.reference['dependency_reconciliation']['sha256'] = hashlib.sha256(raw).hexdigest()
        def blob(source, path):
            if path == check.PROJECT:
                return self.project if source == 'qualified' else self.after
            if path == 'proof/evidence.json':
                return raw
            if path == 'proof/final-workflow-state.json':
                return self.final
            if path.startswith('proof/'):
                return self.cpu[path.removeprefix('proof/')]
            return self.gpu[path]
        def diff(*args):
            return '\n'.join(self.changes) if args[2] == 'qualified' else ''
        with patch.object(check, 'blob', side_effect=blob), patch.object(check, 'git', side_effect=diff):
            check.verify_reconciliation(self.reference, 'baseline')

    def test_verified_dependency_only_path_preserves_real_gpu_checks(self):
        self.run_check()

    def test_extra_source_or_project_edit_rejected(self):
        self.after += b'<!-- unrelated edit -->'
        with self.assertRaises(AssertionError): self.run_check()
        self.after = self.project.replace(check.OLD, check.NEW)
        self.changes.append('src/Indicators/ClassicPriceOscillator.cs')
        with self.assertRaises(AssertionError): self.run_check()

    def test_incomplete_correctness_or_gpu_rejected(self):
        for key in ('unit', 'parity'):
            original = self.record[key]['passed']
            self.record[key]['passed'] = original - 1
            with self.assertRaises(AssertionError): self.run_check()
            self.record[key]['passed'] = original
        self.record['gpu_passed'] = 99
        with self.assertRaises(AssertionError): self.run_check()

    def test_tampered_evidence_or_reused_timings_rejected(self):
        with self.assertRaises(AssertionError): self.run_check(refresh_hash=False)
        self.reference['dependency_reconciliation']['timings_reused'] = True
        with self.assertRaises(AssertionError): self.run_check()

    def test_wrong_gpu_source_rejected(self):
        self.record['source_fingerprint'] = '0' * 64
        with self.assertRaises(AssertionError): self.run_check()

    def test_failed_producer_workflow_rejected(self):
        self.final = json.dumps(dict(status='completed', conclusion='failure',
            headSha=self.record['build_origin']['workflow_head'])).encode()
        with self.assertRaises(AssertionError): self.run_check()


if __name__ == '__main__':
    unittest.main()
