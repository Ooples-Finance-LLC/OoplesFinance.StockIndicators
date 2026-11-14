import unittest
from hosted_timing import pipeline as p, star_warmup_policy as policy

PLAN=p.REPO/'benchmarks/plans/hosted-abandoned-certified-blocks-20261114.json'


class StarWarmupPolicyTests(unittest.TestCase):
    def task(self,pair):
        return dict(fixture='LibraryPairBenchmarks',operations=[dict(raw=pair,pair=pair)],methods=['Ooples','Competitor'])

    def test_approved_batches_only_and_controls_unchanged(self):
        plan=p.read(PLAN);policy.validate(plan)
        for pair in policy.PAIRS:
            task=self.task(pair)
            self.assertEqual(20000,p.warmups_for(plan,'LibraryPairBenchmarks',task))
            context=p.warmup_context(plan,'LibraryPairBenchmarks',task)
            self.assertTrue(policy.permits_context([pair],['Ooples'],context))
            self.assertTrue(policy.permits_context([pair],['Ooples','Competitor'],context))
            self.assertEqual(10,p.warmups_for(plan,'SharedTaStarReversalBenchmarks'))
        crow=self.task('TaLib.Candles.ThreeBlackCrows')
        self.assertEqual(1000,p.warmups_for(plan,'LibraryPairBenchmarks',crow))
        self.assertEqual(10,p.warmups_for(plan,'SharedCrowSoldierBenchmarks'))
        self.assertNotIn('star_warmup_policy_sha256',p.warmup_context(plan,'LibraryPairBenchmarks',crow))

    def test_missing_approval_rejected(self):
        plan=p.read(PLAN);plan.pop('star_warmup_approval')
        with self.assertRaises(AssertionError):p.warmups_for(plan,'LibraryPairBenchmarks',self.task(next(iter(policy.PAIRS))))

    def test_stale_approval_rejected(self):
        plan=p.read(PLAN);plan['star_warmup_approval']['sha256']='stale'
        with self.assertRaises(AssertionError):policy.validate(plan)

    def test_global_longer_warmups_rejected(self):
        plan=p.read(PLAN);plan['measurement_policy']['fixed_invocation_warmups']=20000
        with self.assertRaises(AssertionError):p.warmups_for(plan,'LibraryPairBenchmarks',self.task(next(iter(policy.PAIRS))))

    def test_control_or_unknown_pair_cannot_gain_exception(self):
        for pair in ['TaLib.Candles.ThreeBlackCrows','unknown']:
            plan=p.read(PLAN);plan['measurement_policy']['fixed_invocation_pair_warmups'][pair]=20000
            with self.assertRaises(AssertionError):policy.validate(plan)

    def test_expanded_case_or_runner_scope_rejected(self):
        for field,value in [('cases',210),('decisions',42)]:
            plan=p.read(PLAN);plan[field]=value
            with self.assertRaises(AssertionError):policy.validate(plan)
        plan=p.read(PLAN);plan['matrix']['max_parallel']=2
        with self.assertRaises(AssertionError):policy.validate(plan)

    def test_wrong_scope_or_source_rejected(self):
        plan=p.read(PLAN);plan['targeted_reference']['scope']='unapproved'
        with self.assertRaises(AssertionError):policy.validate(plan)
        plan=p.read(PLAN);plan['sources']['successor']='stale'
        with self.assertRaises(AssertionError):policy.validate(plan)

    def test_execution_context_cannot_authorize_other_roles(self):
        pair=sorted(policy.PAIRS)[0]
        context=dict(warmup_count=20000,star_warmup_policy_sha256=policy.SHA)
        for pairs,methods in [([pair],['BuilderFull']),([pair],['Competitor']),([pair],['Ooples','Ooples']),
            (['TaLib.Candles.ThreeBlackCrows'],['Ooples']),([pair,pair],['Ooples'])]:
            self.assertFalse(policy.permits_context(pairs,methods,context))
        context['star_warmup_policy_sha256']='stale'
        self.assertFalse(policy.permits_context([pair],['Ooples'],context))


if __name__=='__main__':unittest.main()
