"""Require complete smoothed accumulation EMA consumers and authenticated source boundaries."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class SmoothedAccumulationEmaPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (60,12)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-smoothed-accumulation-certified-ema-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
