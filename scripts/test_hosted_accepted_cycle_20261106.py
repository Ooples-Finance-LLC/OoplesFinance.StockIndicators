"""Require the complete fresh accepted-source composition."""
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AcceptedCyclePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (180, 36)
    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-combined-containment-mfi-20261106.json')
        replacement.start();self.addCleanup(replacement.stop)
    def test_fresh_complete_source_correctness(self):
        plan=read(REPO/'benchmarks/plans/hosted-combined-containment-mfi-20261106.json')
        self.assertEqual((45,144,100),tuple(plan['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('correctness_reuse',plan);self.assertNotIn('reuse_inputs',plan)
        self.assertEqual(2,len(plan['individual_qualifications']))
