from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AlignedPrivatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (150, 30)
    target_shard_index = 1
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-star-reversal-certified-difference-20261114.json')
        p.start();self.addCleanup(p.stop)
    def test_runtime_and_risk_sentinel(self):
        p=read(REPO/'benchmarks/plans/hosted-star-reversal-certified-difference-20261114.json')
        self.assertEqual('0.150.0',p['runtime_dependency_alignment']['tensor_version'])
        self.assertTrue(set([]).issubset(p['affected_pairs']))
        self.assertEqual(['StarReversalSharedExecutionTests'],p['cpu_test_classes']['old'])
        self.assertEqual((21,145,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p);self.assertNotIn('correctness_reuse',p)

    def test_complete_scope_source_gate_and_unchanged_policy(self):
        p=read(REPO/'benchmarks/plans/hosted-star-reversal-certified-difference-20261114.json')
        prior=read(REPO/'benchmarks/plans/hosted-prior-directional-word-guards-20261114.json')
        self.assertEqual(prior['measurement_policy'],p['measurement_policy'])
        self.assertEqual((1,150,30),(p['matrix']['max_parallel'],p['cases'],p['decisions']))
        self.assertEqual({'TaLib.Candles.'+name for name in ['MorningStar','EveningStar','MorningDojiStar','EveningDojiStar','AbandonedBaby']},set(p['affected_pairs']))
        self.assertEqual(p['sources']['successor'],p['execution_path_gate']['qualified_source'])
        self.assertEqual(p['source_correctness']['source_sha256'],p['execution_path_gate']['source_fingerprint'])
        self.assertTrue(p['execution_path_gate']['certificate_path_observed'])
        self.assertTrue(p['execution_path_gate']['roi_promising'])
        self.assertFalse(p['execution_path_gate']['performance_acceptance'])
        self.assertFalse(p['authorization_pending'])
