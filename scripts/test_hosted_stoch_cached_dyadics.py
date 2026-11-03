"""Apply complete-scope and authenticated-baseline gates to cached samples."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class StochCachedDyadicsTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-stoch-cached-dyadics-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
