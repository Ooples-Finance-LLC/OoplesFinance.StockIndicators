"""Complete affected-consumer scope and authenticated source boundaries."""
import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.pipeline import REPO, read, write
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect


PLAN = REPO/'benchmarks/plans/hosted-hilbert-fir-20261102.json'
CHANGED = '\n'.join(('src/Helpers/HilbertRoundedKernel.cs',
    'src/Indicators/HilbertPhaseState.cs', 'tests/ModelsTests/HilbertRoundedKernelTests.cs'))


class TargetedReferenceTests(unittest.TestCase):
    def setUp(self):
        self.plan = read(PLAN)
        self.decision = dict(accepted=True, phase='successor', case_count=1860,
            decision_count=372, verdicts={'faster':13},
            run_id=self.plan['targeted_reference']['run_id'],
            sources={'new':self.plan['targeted_reference']['qualified_source']})

    def verify(self, diffs=('', CHANGED)):
        raw = json.dumps(self.decision).encode()
        self.plan['targeted_reference'].setdefault('sha256', hashlib.sha256(raw).hexdigest())
        with patch('hosted_timing.targeted.subprocess.check_output', return_value=raw), \
                patch('hosted_timing.targeted.git', side_effect=diffs):
            verify_reference(self.plan)

    def test_complete_scope_and_source_boundary(self):
        self.plan['targeted_reference'].pop('sha256')
        self.verify()

    def test_missing_consumer_or_wrong_fixture_rejected(self):
        for mode in ('missing', 'fixture', 'method', 'implicit'):
            with self.subTest(mode=mode):
                original = copy.deepcopy(self.plan)
                task = self.plan['targeted_shards'][1]['tasks'][0]
                if mode == 'missing': task['operations'].pop()
                if mode == 'fixture': task['fixture'] = 'SharedHilbertBenchmarks'
                if mode == 'method': task['methods'].pop()
                if mode == 'implicit': task['implicit_pair'] = True
                self.plan['targeted_reference'].pop('sha256')
                with self.assertRaises(AssertionError): self.verify()
                self.plan = original

    def test_changed_source_or_receipt_rejected(self):
        self.plan['targeted_reference'].pop('sha256')
        for diffs in [('src/Other.cs', CHANGED), ('', CHANGED+'\nsrc/Other.cs'), ('', '')]:
            with self.assertRaises(AssertionError): self.verify(diffs)
        self.plan['targeted_reference']['sha256'] = 'wrong'
        with self.assertRaises(AssertionError): self.verify()

    def test_rejected_or_incomplete_baseline_rejected(self):
        for change in [dict(accepted=False), dict(case_count=48), dict(decision_count=12),
                       dict(verdicts={'slower':1}), dict(sources={'new':'wrong'}), dict(run_id='wrong')]:
            with self.subTest(change=change):
                original = self.decision.copy()
                self.decision.update(change)
                self.plan['targeted_reference'].pop('sha256', None)
                with self.assertRaises(AssertionError): self.verify()
                self.decision = original


class TargetedEvidenceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.path = self.root/'plan.json'
        write(self.path, read(PLAN))
        for module in ('hosted_timing.pipeline', 'hosted_timing.verify', 'test_hosted_timing'):
            self.enterContext(patch(module+'.PLAN', self.path))
        self.enterContext(patch('hosted_timing.targeted.verify_reference'))
        self.evidence = self.root/'evidence'
        self.evidence.mkdir()
        test_hosted_timing.make_evidence(self.evidence, 'targeted')

    def test_complete_300_cases_60_decisions(self):
        result = collect(self.evidence, 'targeted')
        self.assertEqual((300,60), (result['case_count'],result['decision_count']))
        self.assertTrue(result['accepted'])

    def test_missing_shard_rejected(self):
        (self.evidence/'shard1/provenance.json').unlink()
        with self.assertRaises(AssertionError): collect(self.evidence,'targeted')

    def test_incomplete_round_rejected(self):
        path = self.evidence/'shard1/status.json'
        state = read(path)
        state['rounds'].pop()
        write(path,state)
        with self.assertRaises(AssertionError): collect(self.evidence,'targeted')
