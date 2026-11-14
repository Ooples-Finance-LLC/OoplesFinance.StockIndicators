from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AlignedPrivatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (180, 36)
    target_shard_index = 1
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-abandoned-certified-blocks-20261114.json')
        p.start();self.addCleanup(p.stop)
    def test_runtime_and_risk_sentinel(self):
        p=read(REPO/'benchmarks/plans/hosted-abandoned-certified-blocks-20261114.json')
        self.assertEqual('0.150.0',p['runtime_dependency_alignment']['tensor_version'])
        self.assertTrue(set([]).issubset(p['affected_pairs']))
        self.assertEqual(['StarReversalSharedExecutionTests', 'CrowSoldierSharedExecutionTests'],p['cpu_test_classes']['old'])
        self.assertEqual((38,203,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p);self.assertNotIn('correctness_reuse',p)

    def test_source_gate_and_star_policy(self):
        import hashlib,json,subprocess
        p=read(REPO/'benchmarks/plans/hosted-abandoned-certified-blocks-20261114.json')
        from hosted_timing.star_warmup_policy import validate
        validate(p)
        gate=p['execution_path_gate']
        self.assertEqual(p['source_correctness']['source_sha256'],gate['source_fingerprint'])
        self.assertTrue(gate['roi_promising'] and gate['actual_tensor_workers_observed'])
        raw=subprocess.check_output(['git','show',p['sources']['successor']+':'+gate['evidence']],cwd=REPO)
        self.assertEqual(gate['sha256'],hashlib.sha256(raw).hexdigest())
        evidence=json.loads(raw)
        self.assertEqual(gate['source_fingerprint'],evidence['source_fingerprint'])
        self.assertTrue(evidence['optimized_block_helper_observed'] and evidence['non_star_specialization_eliminated'])
        self.assertTrue(evidence['block_path_trace_observed'] and evidence['roi_promising'])
        self.assertFalse(evidence['performance_qualified'] or evidence['local_timing_usable_for_acceptance'])
        self.assertFalse(subprocess.check_output(['git','diff',evidence['source'],p['sources']['successor'],'--','src','tests'],cwd=REPO))
