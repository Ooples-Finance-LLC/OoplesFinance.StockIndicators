import copy
from unittest.mock import patch
import test_hosted_abandoned_certified_blocks_20261114 as original
from hosted_timing import pipeline as p, star_warmup_policy as policy

PLAN=p.REPO/'benchmarks/plans/hosted-abandoned-inventory-repair-20261114.json'

class InventoryRepairTests(original.AlignedPrivatePlanTests):
    expected_totals=(176,36)
    def setUp(self):
        super().setUp()
        bound=patch.object(original,'read',side_effect=lambda path:p.read(PLAN))
        bound.start();self.addCleanup(bound.stop)
        bound=patch('test_hosted_stoch_compact.PLAN',PLAN)
        bound.start();self.addCleanup(bound.stop)
    def test_runtime_and_risk_sentinel(self):
        plan=p.read(PLAN)
        self.assertEqual((38,203,100),tuple(plan['source_correctness'][k] for k in ('unit','comparator','actual_local_gpu')))
        self.assertEqual(43610765565,plan['reuse_inputs']['run_id'])
        self.assertTrue(plan['require_fixture_inventory'])
    def test_new_approval_and_unchanged_original(self):
        policy.validate(p.read(PLAN))
        policy.validate(p.read(original.REPO/'benchmarks/plans/hosted-abandoned-certified-blocks-20261114.json'))
        plan=p.read(PLAN)
        context=p.warmup_context(plan,'LibraryPairBenchmarks',plan['targeted_shards'][0]['tasks'][0])
        self.assertEqual(policy.REPAIR_SHA,context['star_warmup_policy_sha256'])
    def test_repair_cannot_expand_or_borrow_original_approval(self):
        for mutation in ('cases','approval','inventory','crow','runner'):
            plan=copy.deepcopy(p.read(PLAN))
            if mutation=='cases':plan['cases']=180
            elif mutation=='approval':plan['star_warmup_approval']=p.read(original.REPO/'benchmarks/plans/hosted-abandoned-certified-blocks-20261114.json')['star_warmup_approval']
            elif mutation=='inventory':plan['require_fixture_inventory']=False
            elif mutation=='crow':plan['targeted_shards'][-1]['tasks'][-1]['methods'].append('NativeSingle')
            else:plan['matrix']['max_parallel']=2
            with self.subTest(mutation=mutation),self.assertRaises(AssertionError):policy.validate(plan)
