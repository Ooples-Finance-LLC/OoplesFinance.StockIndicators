"""Require both complete Renko consumers and exact snapshot routing."""
import copy
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read
from hosted_timing.targeted import verify_reference

PLAN=REPO/'benchmarks/plans/hosted-renko-price-grid-20261105.json'


class RenkoPriceGridPlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals=(44,8)

    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',PLAN)
        replacement.start();self.addCleanup(replacement.stop)

    def test_fresh_correctness_and_complete_snapshot_scope(self):
        plan=read(PLAN)
        self.assertEqual((15,22,100),tuple(plan['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertNotIn('correctness_reuse',plan)
        self.assertNotIn('reuse_inputs',plan)
        self.assertEqual(['Skender.GetRenko','Skender.GetRenkoAtr'],plan['affected_pairs'])
        for shard in plan['targeted_shards']:
            self.assertEqual((22,4),(shard['cases'],shard['decisions']))
            self.assertEqual(['Snapshot','NativeSingle','NativeParallel'],shard['tasks'][1]['methods'])

    def test_missing_snapshot_or_relabeling_as_builder_is_rejected(self):
        for methods in (['NativeSingle','NativeParallel'],['BuilderFull','NativeSingle','NativeParallel']):
            plan=copy.deepcopy(read(PLAN));plan['targeted_shards'][0]['tasks'][1]['methods']=methods
            with self.assertRaises(AssertionError):verify_reference(plan)
