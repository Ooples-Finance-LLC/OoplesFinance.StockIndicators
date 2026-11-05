"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class BetaDerivedRatioPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (30, 6)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-beta-derived-ratio-v2-20261105.json')
        replacement.start()
        self.addCleanup(replacement.stop)

    def test_correctness_metadata_is_fresh_and_source_bound(self):
        import json
        p = json.loads((REPO / 'benchmarks/plans/hosted-beta-derived-ratio-v2-20261105.json').read_text())
        self.assertNotIn('correctness_reuse', p)
        self.assertNotIn('prior_rejected_qualification', p)
        self.assertEqual('41999864438', p['source_correctness']['cpu_build_run'])
        self.assertEqual((20, 13, 100), tuple(p['source_correctness'][k] for k in ('unit', 'comparator', 'actual_local_gpu')))
