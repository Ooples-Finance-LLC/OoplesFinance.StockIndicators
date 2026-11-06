"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class DojiCachedWeightPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (150, 30)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-doji-cached-weight-20261106.json')
        replacement.start()
        self.addCleanup(replacement.stop)

    def test_fresh_correctness_metadata(self):
        import json
        p = json.loads((REPO / 'benchmarks/plans/hosted-doji-cached-weight-20261106.json').read_text())
        self.assertNotIn('correctness_reuse', p)
        self.assertNotIn('prior_rejected_qualification', p)
        self.assertNotIn('superseded_plan', p)
        self.assertEqual((11, 69, 100), tuple(p['source_correctness'][k] for k in ('unit', 'comparator', 'actual_local_gpu')))
