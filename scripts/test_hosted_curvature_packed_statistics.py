"""Require exact source scope and complete packed-curvature timing."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class CurvaturePackedStatisticsTests(test_hosted_stoch_compact.StochCompactTests):
    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-curvature-packed-statistics-20261103.json')
        replacement.start()
        self.addCleanup(replacement.stop)
