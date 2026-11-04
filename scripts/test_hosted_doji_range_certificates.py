"""Require all five range-doji consumers and both complete timing shards."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class DojiRangeCertificateTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (150,30)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-doji-range-certificates-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
