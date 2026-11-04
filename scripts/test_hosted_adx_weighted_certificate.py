"""Require complete exact ADX consumers and authenticated source boundaries."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class AdxWeightedCertificatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-adx-weighted-certificate-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
