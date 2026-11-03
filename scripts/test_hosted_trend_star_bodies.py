"""The repaired representation needs a new complete 150-case qualification."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO

class TrendStarBodyTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (150,30)

    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-trend-star-certified-bodies-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
