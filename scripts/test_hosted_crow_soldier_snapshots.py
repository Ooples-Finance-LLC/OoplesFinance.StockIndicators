"""Require complete Crow/Soldier snapshots consumers and authenticated source boundaries."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class CrowSoldierSnapshotsPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (90,18)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-crow-soldier-active-snapshots-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
