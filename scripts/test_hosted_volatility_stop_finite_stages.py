import copy
from unittest.mock import patch
import test_hosted_stoch_compact
from hosted_timing.pipeline import REPO,read
from hosted_timing.targeted import verify_reference

PLAN=REPO/'benchmarks/plans/hosted-volatility-stop-finite-stages-20261105.json'


class VolatilityStopFiniteStagePlanTests(test_hosted_stoch_compact.StochCompactTests):
    expected_totals=(22,4)

    def setUp(self):
        replacement=patch('test_hosted_stoch_compact.PLAN',PLAN)
        replacement.start();self.addCleanup(replacement.stop)

    def test_source_correctness_and_snapshot_scope(self):
        plan=read(PLAN)
        self.assertEqual((15,21,100),tuple(plan['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertEqual(['Skender.GetVolatilityStop'],plan['affected_pairs'])
        self.assertEqual(['Snapshot','NativeSingle','NativeParallel'],plan['targeted_shards'][1]['tasks'][0]['methods'])
        self.assertNotIn('reuse_inputs',plan)
        self.assertNotIn('correctness_reuse',plan)

    def test_missing_snapshot_or_builder_substitution_is_rejected(self):
        for methods in (['NativeSingle','NativeParallel'],['BuilderFull','NativeSingle','NativeParallel']):
            plan=copy.deepcopy(read(PLAN));plan['targeted_shards'][1]['tasks'][0]['methods']=methods
            with self.assertRaises(AssertionError):verify_reference(plan)
