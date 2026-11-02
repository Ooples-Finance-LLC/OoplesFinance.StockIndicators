"""A targeted run may reuse acceptance only for an unchanged qualified baseline."""
import copy
import hashlib
import json
import unittest
from unittest.mock import patch

from hosted_timing.variants import verify_baseline


class VariantBaselineTests(unittest.TestCase):
    def setUp(self):
        self.decision = dict(accepted=True, phase='successor', case_count=1860,
                             decision_count=372, verdicts={'faster': 13},
                             run_id='accepted-run', sources={'new': 'qualified-source'})
        self.plan = dict(qualify_variants=True, trix_preflight=False,
                         sources=dict(published='baseline', successor='baseline', variants='candidate'),
                         standalone_variants=dict(decision='receipt.json', run_id='accepted-run',
                                                  qualified_source='qualified-source'))

    def verify(self, baseline_diff='', candidate_diff='src/Indicators/WideClassicalWindowState.cs'):
        raw = json.dumps(self.decision).encode()
        self.plan['standalone_variants'].setdefault('sha256', hashlib.sha256(raw).hexdigest())
        with patch('hosted_timing.variants.subprocess.check_output', return_value=raw), \
                patch('hosted_timing.variants.git', side_effect=[baseline_diff, candidate_diff]):
            verify_baseline(self.plan)

    def test_accepts_complete_qualified_baseline_and_only_normalization_change(self):
        self.verify()

    def test_legacy_plans_do_not_require_a_prior_acceptance(self):
        verify_baseline({})

    def test_rejects_changed_receipt_bytes(self):
        self.plan['standalone_variants']['sha256'] = 'wrong'
        with self.assertRaises(AssertionError):
            self.verify()

    def test_rejects_failed_incomplete_or_different_qualification(self):
        for changes in [dict(accepted=False), dict(case_count=48), dict(decision_count=12),
                        dict(verdicts={'slower': 1}), dict(run_id='other'),
                        dict(sources={'new': 'other'}), dict(phase='variants')]:
            with self.subTest(changes=changes):
                original = copy.deepcopy(self.decision)
                self.decision.update(changes)
                with self.assertRaises(AssertionError):
                    self.verify()
                self.decision = original
                self.plan['standalone_variants'].pop('sha256', None)

    def test_rejects_baseline_with_unqualified_source_changes(self):
        with self.assertRaises(AssertionError):
            self.verify(baseline_diff='src/Other.cs')

    def test_rejects_unrelated_candidate_changes(self):
        for change in ['', 'src/Other.cs', 'src/Indicators/WideClassicalWindowState.cs\ntests/Other.cs']:
            with self.subTest(change=change), self.assertRaises(AssertionError):
                self.verify(candidate_diff=change)

    def test_rejects_different_published_baseline(self):
        self.plan['sources']['published'] = 'other'
        with self.assertRaises(AssertionError):
            self.verify()
