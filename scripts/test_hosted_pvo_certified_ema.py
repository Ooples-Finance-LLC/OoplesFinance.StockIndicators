"""Require all four mapped consumers of the changed EMA-difference dispatch."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class PvoCertifiedEmaTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (120,24)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-pvo-certified-ema-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
