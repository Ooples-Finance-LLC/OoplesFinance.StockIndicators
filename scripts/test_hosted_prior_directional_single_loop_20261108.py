from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read

class AlignedPrivatePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals = (270, 54)
    target_shard_index = 1
    def setUp(self):
        p=patch('test_hosted_stoch_compact.PLAN',REPO/'benchmarks/plans/hosted-prior-directional-single-loop-20261108.json')
        p.start();self.addCleanup(p.stop)
    def test_runtime_and_risk_sentinel(self):
        p=read(REPO/'benchmarks/plans/hosted-prior-directional-single-loop-20261108.json')
        self.assertEqual('0.148.0',p['runtime_dependency_alignment']['tensor_version'])
        self.assertTrue(set([]).issubset(p['affected_pairs']))
        self.assertEqual(['SelectedDirectionalStateTests', 'OwnedHistoryBatchTests', 'SharedRecursiveStateTests'],p['cpu_test_classes']['old'])
        self.assertEqual((62,70,100),tuple(p['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('reuse_inputs',p);self.assertNotIn('correctness_reuse',p)

    def test_additional_fixture_receipt(self):
        import hashlib
        p=read(REPO/'benchmarks/plans/hosted-prior-directional-single-loop-20261108.json')
        e=p['source_correctness']['additional_local_comparator']
        raw=(REPO/e['receipt']).read_bytes()
        self.assertEqual(e['sha256'],hashlib.sha256(raw).hexdigest())
        receipt=read(REPO/e['receipt'])
        self.assertEqual(7,receipt['passed'])
        self.assertEqual(77,p['source_correctness']['comparator']+receipt['passed'])
        self.assertEqual(p['source_correctness']['source_sha256'],receipt['source_fingerprint'])
        self.assertEqual(str(p['source_correctness']['cpu_build_run']),str(receipt['build_origin']['run_id']))
