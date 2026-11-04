"""Require complete prior directional raw stages consumers and authenticated source boundaries."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class PriorDirectionalRawStagesPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (210,42)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-prior-directional-raw-stages-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
