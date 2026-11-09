import hashlib,json,unittest
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
class NativeReceiptTests(unittest.TestCase):
    def setUp(self):
        self.plan=json.loads((ROOT/'benchmarks/plans/hosted-trix-scalar-stage-storage-20261108.json').read_text())
        self.extra=self.plan['source_correctness']['additional_local_comparator']
        self.raw=(ROOT/self.extra['receipt']).read_bytes();self.receipt=json.loads(self.raw)
    def test_receipt_bytes(self):
        self.assertNotIn(b'\r',self.raw)
        self.assertEqual(hashlib.sha256(self.raw).hexdigest(),self.extra['sha256'])
    def test_source_fingerprint(self):
        self.assertEqual(self.plan['source_correctness']['source_sha256'],self.receipt['source_fingerprint'])
        self.assertEqual(self.receipt['source_fingerprint'],self.extra['source_fingerprint'])
    def test_compiled_source(self):
        self.assertEqual('cde573b62a445a3fb5f59f90e3fa6ca84eb985b8',self.receipt['compiled_source'])
        self.assertEqual('0f248f91b',self.plan['sources']['successor'][:9])
    def test_authenticated_origin(self):
        self.assertEqual(self.plan['source_correctness']['cpu_build_run'],self.receipt['origin_run'])
        self.assertEqual(self.plan['source_correctness']['cpu_build_workflow_head'],self.receipt['origin_workflow'])
    def test_counts_keep_locations_distinct(self):
        self.assertEqual((18,121,139),(self.receipt['primary_ci_comparator_passed'],self.receipt['additional_local_comparator_passed'],self.receipt['total_comparator_passed']))
        self.assertEqual(18,self.plan['source_correctness']['comparator'])
        self.assertEqual({'SharedSkenderEmaFamilyBenchmarkTests':81,'SharedTaAdaptiveRateNativeBenchmarkTests':40},self.receipt['classes'])
    def test_native_mapping_and_invalid_attempt(self):
        fixtures={t['fixture'] for s in self.plan['targeted_shards'] for t in s['tasks']}
        self.assertEqual({'LibraryPairBenchmarks','SharedSkenderEmaFamilyBenchmarks','SharedTaAdaptiveRateNativeBenchmarks'},fixtures)
        self.assertFalse(self.plan['superseded_plan']['partial_timings_reused'])
        self.assertEqual('42534150644',self.plan['superseded_plan']['run_id'])
