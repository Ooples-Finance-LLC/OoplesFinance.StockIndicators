"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class LogReturnShortCertificatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (60, 12)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-log-return-short-certificate-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
