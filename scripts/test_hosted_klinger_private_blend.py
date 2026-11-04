"""Require complete prior directional ratios consumers and authenticated source boundaries."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class KlingerPrivateBlendPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (30,6)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-klinger-private-blend-20261104.json')
        replacement.start()
        self.addCleanup(replacement.stop)
