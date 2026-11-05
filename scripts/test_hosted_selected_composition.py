"""Require complete accepted private arithmetic consumers."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO


class SelectedCompositionPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (96, 18)

    def setUp(self):
        replacement = patch('test_hosted_stoch_compact.PLAN',
            REPO / 'benchmarks/plans/hosted-combined-renko-mean-stop-20261105.json')
        replacement.start()
        self.addCleanup(replacement.stop)

    def test_fresh_complete_source_metadata(self):
        from hosted_timing.pipeline import read
        plan=read(REPO/'benchmarks/plans/hosted-combined-renko-mean-stop-20261105.json')
        self.assertNotIn('reuse_inputs',plan)
        self.assertNotIn('correctness_reuse',plan)
        self.assertEqual((53, 56, 100),tuple(plan['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))

    def test_all_published_renko_controls_and_selected_consumers_remain(self):
        from hosted_timing.pipeline import read
        plan=read(REPO/'benchmarks/plans/hosted-combined-renko-mean-stop-20261105.json')
        self.assertEqual(['Skender.GetPrs', 'Skender.GetRenko', 'Skender.GetRenkoAtr', 'Skender.GetVolatilityStop'],plan['affected_pairs'])
        self.assertEqual(4,len(plan['targeted_shards']))
