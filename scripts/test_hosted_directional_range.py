"""Apply the same strict completeness and baseline gates to the ADX scope."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO

class DirectionalRangeTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-sum-directional-range-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
