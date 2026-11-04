"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class CombinedPrivateInputsTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (180, 36)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-combined-private-inputs-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
