from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO, read

class BoundedPrivateExecutionTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (60,12)
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-mfi-private-certified-ratio-20261106.json')
        p.start();self.addCleanup(p.stop)
    def test_fresh_correctness(self):
        p=read(REPO/'benchmarks/plans/hosted-mfi-private-certified-ratio-20261106.json')
        self.assertEqual((31,18,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p)
        self.assertNotIn('correctness_reuse',p)
