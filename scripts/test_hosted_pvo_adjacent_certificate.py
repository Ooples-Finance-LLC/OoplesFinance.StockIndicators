"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class PvoAdjacentCertificatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (120, 24)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-pvo-adjacent-certificate-20261105.json')
        replacement.start()
        self.addCleanup(replacement.stop)
