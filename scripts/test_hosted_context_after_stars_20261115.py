from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AlignedPrivatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (120, 24)
    target_shard_index = 1
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-context-after-stars-20261115.json')
        p.start();self.addCleanup(p.stop)
    def test_runtime_and_risk_sentinel(self):
        p=read(REPO/'benchmarks/plans/hosted-context-after-stars-20261115.json')
        self.assertEqual('0.150.0',p['runtime_dependency_alignment']['tensor_version'])
        self.assertTrue(set([]).issubset(p['affected_pairs']))
        self.assertEqual(['ContextReversalSharedExecutionTests', 'ContextReversalParallelExecutionTests'],p['cpu_test_classes']['old'])
        self.assertEqual((16,147,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p);self.assertNotIn('correctness_reuse',p)

    def test_frozen_baseline_consumers_and_unchanged_measurement_contract(self):
        p=read(REPO/'benchmarks/plans/hosted-context-after-stars-20261115.json')
        self.assertEqual('1225eed2132999e98d1934e6c720e7e60f208e7e',p['sources']['old'])
        self.assertEqual('83561cd5065071ff3a3deb67d9174cc144fcfc60',p['sources']['successor'])
        self.assertEqual({'TaLib.Candles.'+name for name in ('Hammer','HangingMan','InvertedHammer','ShootingStar')},set(p['affected_pairs']))
        self.assertEqual(1,p['matrix']['max_parallel'])
        self.assertEqual(dict(order=['old','new','new','old'],quiet_samples=12,max_cpu_percent=15,warmups=10,iterations=10,fixed_invocation_warmups=1000),p['measurement_policy'])
        self.assertTrue(p['require_fixture_inventory'])
