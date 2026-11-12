import copy
import unittest
from unittest.mock import patch
from hosted_timing import pipeline as p, arithmetic_warmup_policy as a
from hosted_timing.checkpoints import run_checkpointed


class ArithmeticWarmupPolicyTests(unittest.TestCase):
    def setUp(self):
        self.plan=p.read(p.REPO/'benchmarks/plans/hosted-arithmetic-warmup-policy-20261112.json')

    def test_frozen_approval_and_complete_existing_scope(self):
        a.validate(self.plan)
        self.assertEqual(a.SHA,p.digest(p.REPO/a.PROPOSAL))
        from hosted_timing.targeted import verify_reference,expected_totals
        verify_reference(self.plan)
        self.assertEqual((120,24),expected_totals(self.plan))
        self.assertEqual('43214078458',self.plan['reuse_inputs']['run_id'])

    def test_only_approved_batch_pairs_change(self):
        for shard in self.plan['targeted_shards']:
            for task in shard['tasks']:
                batch=task['fixture']=='LibraryPairBenchmarks'
                self.assertEqual(20000 if batch else 10,p.warmups_for(self.plan,task['fixture'],task))
                context=p.warmup_context(self.plan,task['fixture'],task)
                self.assertEqual(batch,'arithmetic_warmup_policy_sha256' in context)
        other=dict(fixture='LibraryPairBenchmarks',operations=[dict(raw='TaLib.Functions.Exp',pair='TaLib.Functions.Exp')],methods=['Ooples','Competitor'])
        self.assertEqual(1000,p.warmups_for(self.plan,'LibraryPairBenchmarks',other))

    def test_no_global_longer_warmups(self):
        plan=copy.deepcopy(self.plan);plan['measurement_policy']['fixed_invocation_warmups']=20000
        with self.assertRaises(AssertionError):p.warmups_for(plan,'LibraryPairBenchmarks')

    def test_missing_approval_or_changed_source_rejected(self):
        plan=copy.deepcopy(self.plan);plan.pop('arithmetic_warmup_approval')
        with self.assertRaises(AssertionError):a.validate(plan)
        for key in ('arithmetic_warmup_approval','sources'):
            plan=copy.deepcopy(self.plan);plan[key]={}
            with self.assertRaises(AssertionError):a.validate(plan)

    def test_proposal_and_policy_changes_rejected(self):
        with patch.object(a,'SHA','0'*64):
            with self.assertRaises(AssertionError):a.validate(self.plan)
        plan=copy.deepcopy(self.plan);plan['measurement_policy']['max_cpu_percent']=16
        with self.assertRaises(AssertionError):a.validate(plan)
        plan=copy.deepcopy(self.plan);plan['measurement_policy']['fixed_invocation_pair_warmups']['TaLib.Functions.Exp']=20000
        with self.assertRaises(AssertionError):a.validate(plan)

    def test_checkpoint_context_restricted(self):
        context=dict(warmup_count=20000,arithmetic_warmup_policy_sha256=a.SHA)
        self.assertTrue(a.permits_context(['TaLib.Functions.Add'],['Ooples'],context))
        for pairs,methods,ctx in [(['TaLib.Functions.Exp'],['Ooples'],context),(['TaLib.Functions.Add'],['BuilderFull'],context),(['TaLib.Functions.Add'],['Ooples'],{'warmup_count':20000})]:
            self.assertFalse(a.permits_context(pairs,methods,ctx))
            with self.assertRaises(AssertionError):
                run_checkpointed(out=None,index=0,arm='old',fixture='LibraryPairBenchmarks',pairs=pairs,methods=methods,
                    runtime=None,env={},state={},save=None,builds=None,assert_binaries=None,context=ctx,warmups=20000)

    def test_original_preparation_identity_retained(self):
        original=p.read(p.REPO/'benchmarks/plans/hosted-arithmetic-field-selection-20261112.json')
        for key in ('sources','cpu_test_classes','gpu_sources','source_correctness','targeted_shards','runtime_dependency_alignment'):
            self.assertEqual(original[key],self.plan[key])
        self.assertEqual(p.digest(p.REPO/'benchmarks/plans/hosted-arithmetic-field-selection-20261112.json'),self.plan['reuse_inputs']['plan_sha256'])


if __name__=='__main__':unittest.main()
