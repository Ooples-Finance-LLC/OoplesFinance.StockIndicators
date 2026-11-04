"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class CombinedDirectionalAccumulationCapTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (270, 54)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-combined-directional-accumulation-cap-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
