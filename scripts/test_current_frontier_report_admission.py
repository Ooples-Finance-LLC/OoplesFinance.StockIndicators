"""Exercise new refresh dispatch against full802-primary/1544-builder report shapes."""
from unittest.mock import patch
import unittest

from hosted_timing import current_frontier_refresh as q
import test_current_baseline_report_admission as legacy


class CurrentFrontierReportAdmissionTests(unittest.TestCase):
    def check_admission(self, admitted_count, wrong_report_hash=False):
        def identical(plan,manifests):
            self.assertEqual(manifests['old'],manifests['new'])
        fixture=legacy.CurrentBaselineReportAdmissionTests('runTest')
        self.addCleanup(fixture.doCleanups)
        with patch.object(q,'verify_runtime_identity',side_effect=identical):
            return fixture.check_admission(admitted_count,wrong_report_hash,
                refresh_module=q,refresh_flag='current_frontier_refresh',plan_extra={'composition_policy':q.COMPOSITION_POLICY})

    def test_partial_admission_preserves_802_rows_and_all1544_builders(self):
        after=self.check_admission(3)
        receipt=after['evidence']['harnessBaselineRefreshes'][-1]
        self.assertEqual(q.COMPOSITION_POLICY,receipt['composition_policy'])
        self.assertEqual(q.CANONICAL,receipt['snapshot_source'])
        self.assertEqual(247,receipt['remaining_unmeasured_primary_pairs'])
        self.assertEqual(0,receipt['algorithm_credit'])

    def test_full_admission_updates_exactly308_owned_rows(self):
        self.check_admission(154)

    def test_zero_admission_never_stages_a_report(self):
        self.check_admission(0)

    def test_changed_frozen_report_is_rejected_before_staging(self):
        self.check_admission(3,wrong_report_hash=True)


if __name__=='__main__':unittest.main()
