"""Require all four consumers after the private PVO dispatch repair."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class PvoVolumeDispatchTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (120,24)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-pvo-volume-dispatch-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
