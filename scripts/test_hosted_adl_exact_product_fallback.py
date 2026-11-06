"""Require complete private A/D batch and builder consumers."""
import copy
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import test_hosted_stoch_compact
import test_hosted_timing
from hosted_timing.pipeline import REPO,read
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect

PLAN=REPO/'benchmarks/plans/hosted-adl-exact-product-fallback-20261106.json'


class AdlExactProductFallbackPlanTests(unittest.TestCase):
    expected_totals=(30,6)
    test_real_baseline_and_source_scope = test_hosted_stoch_compact.StochCompactTests.test_real_baseline_and_source_scope
    test_unintegrated_or_unreported_baseline_rejected = test_hosted_stoch_compact.StochCompactTests.test_unintegrated_or_unreported_baseline_rejected

    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',PLAN)
        replacement.start();self.addCleanup(replacement.stop)

    def test_wrong_fixture_methods_or_pair_rejected(self):
        for key,value in [('fixture','SharedSmoothedStochasticBenchmarks'),
            ('implicit_pair',True),('methods',['BuilderFull','BuilderLatestOnly']),
            ('operations',[dict(raw='Skender.GetSmi',pair='Skender.GetSmi')])]:
            plan=copy.deepcopy(read(PLAN));plan['targeted_shards'][0]['tasks'][1][key]=value
            with self.assertRaises((AssertionError,KeyError)):verify_reference(plan)

    def test_complete_scope_and_planned_shard_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            with patch('hosted_timing.pipeline.PLAN',PLAN),patch('hosted_timing.verify.PLAN',PLAN),patch('test_hosted_timing.PLAN',PLAN):
                test_hosted_timing.make_evidence(root,'targeted')
                result=collect(root,'targeted')
                self.assertEqual(self.expected_totals,(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])
                (root/'shard0/provenance.json').unlink()
                with self.assertRaises(AssertionError):collect(root,'targeted')

    def test_fresh_correctness_and_complete_builder_scope(self):
        plan=read(PLAN)
        self.assertEqual((7,25,100),tuple(plan['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('correctness_reuse',plan)
        self.assertNotIn('reuse_inputs',plan)
        self.assertEqual(['Skender.GetAdl'],plan['affected_pairs'])
        for shard in plan['targeted_shards']:
            self.assertEqual((30,6),(shard['cases'],shard['decisions']))
            self.assertEqual(['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'],shard['tasks'][1]['methods'])

    def test_missing_builder_or_snapshot_relabeling_is_rejected(self):
        for methods in (['NativeSingle','NativeParallel'],['Snapshot','BuilderLatestOnly','NativeSingle','NativeParallel']):
            plan=copy.deepcopy(read(PLAN));plan['targeted_shards'][0]['tasks'][1]['methods']=methods
            with self.assertRaises(AssertionError):verify_reference(plan)
