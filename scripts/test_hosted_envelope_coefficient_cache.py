"""Require complete envelope consumers and reject source or measurement drift."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class EnvelopeCoefficientCacheTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-envelope-coefficient-cache-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
