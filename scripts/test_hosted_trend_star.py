"""The five Trady star pairs require all 150 authenticated cases."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO

class TrendStarTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (150,30)

    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-trend-star-compact-ranks-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
