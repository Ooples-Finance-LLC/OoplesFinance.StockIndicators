"""The approved warmup exception is diagnostic-only and requires fresh complete logs."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_body_shadow_control as controls
from hosted_timing import aa_control as aa, pipeline as p, verify as v

PLAN=p.REPO/'benchmarks/plans/harami-warmup-stability-20261107.json'
ORIGINAL=p.REPO/aa.ORIGINAL


class HaramiWarmupStabilityTests(unittest.TestCase):
    def setUp(self):
        for module in (p,v):
            item=patch.object(module,'PLAN',ORIGINAL);item.start();self.addCleanup(item.stop)

    def test_only_unmeasured_warmups_change_and_old_plan_is_unchanged(self):
        new=aa.validate_plan(PLAN);old=aa.validate_plan(controls.PLAN)
        expected=dict(old['measurement_policy'],fixed_invocation_warmups=20000)
        self.assertEqual(expected,new['measurement_policy'])
        self.assertEqual(1000,old['measurement_policy']['fixed_invocation_warmups'])
        self.assertEqual((20,4,2),(new['cases'],new['comparisons'],len(new['jobs'])))
        self.assertEqual([{},{}],[aa.environment(new,i) for i in range(2)])
        self.assertEqual([('old','old'),('successor','successor')],[aa.profiles(new,i) for i in range(2)])
        plain=dict(p.read(ORIGINAL),measurement_policy=new['measurement_policy'])
        with self.assertRaises(AssertionError):p.warmups_for(plain,'LibraryPairBenchmarks')
        self.assertEqual(20000,p.warmups_for(aa.measurement_plan(p.read(ORIGINAL),new),'LibraryPairBenchmarks'))

    def test_changed_samples_policy_or_sources_rejected(self):
        d=p.read(PLAN)
        changes=[lambda x:x['measurement_policy'].update(iterations=9),
            lambda x:x['measurement_policy'].update(fixed_invocation_warmups=19999),
            lambda x:x['runtime_policy_overrides'].update(DOTNET_TieredPGO='0'),
            lambda x:x['jobs'][1]['label_profiles'].update(B='old')]
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'bad.json'
            for change in changes:
                broken=copy.deepcopy(d);change(broken);p.write(path,broken)
                with self.assertRaises(AssertionError):aa.validate_plan(path)

    def test_measure_uses20000_warmups_and_identical_complete_profiles(self):
        fixture=controls.BodyShadowControlTests('test_measure_routes_every_job_to_complete_original_profile')
        with patch.object(controls,'PLAN',PLAN):
            fixture.test_measure_routes_every_job_to_complete_original_profile()

    def test_complete20case_collection_never_accepts_source_or_old_warmup_logs(self):
        fixture=controls.BodyShadowControlTests('test_separated_controls_and_observations_never_qualify_source')
        plan=aa.measurement_plan(p.read(ORIGINAL),aa.validate_plan(PLAN))
        with tempfile.TemporaryDirectory() as tmp,patch.object(controls,'PLAN',PLAN):
            root=Path(tmp)
            with patch.object(controls.base,'read',return_value=plan):d=fixture.evidence(root)
            result=fixture.collected(root,d)
            self.assertEqual((20,4),(result['case_count'],result['comparison_count']))
            self.assertTrue(result['same_binary'])
            self.assertTrue(result['warmup_experiment'])
            self.assertEqual(20000,result['measurement_policy']['fixed_invocation_warmups'])
            self.assertNotIn('accepted',result)
            for key in ('production_integrated','report_imported','policy_changed'):self.assertFalse(result[key])
            (root/'shard1/status.json').unlink()
            with self.assertRaises((AssertionError,FileNotFoundError)):fixture.collected(root,d)
        with tempfile.TemporaryDirectory() as tmp,patch.object(controls,'PLAN',PLAN):
            root=Path(tmp);d=fixture.evidence(root)
            with self.assertRaises(AssertionError):fixture.collected(root,d)


if __name__=='__main__':unittest.main()
