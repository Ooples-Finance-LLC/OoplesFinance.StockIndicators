from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AlignedPrivatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (300, 60)
    target_shard_index = 1
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-hilbert-scalar-state-0150-20261111.json')
        p.start();self.addCleanup(p.stop)
    def test_runtime_and_risk_sentinel(self):
        p=read(REPO/'benchmarks/plans/hosted-hilbert-scalar-state-0150-20261111.json')
        self.assertEqual('0.150.0',p['runtime_dependency_alignment']['tensor_version'])
        self.assertTrue(set([]).issubset(p['affected_pairs']))
        self.assertEqual(['CompactHilbertPhaseTests', 'CompactPhaseAdaptiveTests', 'HilbertRoundedKernelTests', 'HilbertTrendlineSharedExecutionTests', 'HilbertVariableWindowTests', 'CertifiedHilbertProjectionTests'],p['cpu_test_classes']['old'])
        self.assertEqual((55,167,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p);self.assertNotIn('correctness_reuse',p)
