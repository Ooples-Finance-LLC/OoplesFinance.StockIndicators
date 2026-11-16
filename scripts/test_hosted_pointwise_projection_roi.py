import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p, pointwise_projection_roi as roi, verify
from hosted_timing.roi_scope_dispatch import validate_scope

PLAN = p.REPO/'benchmarks/plans/hosted-pointwise-projection-native-pilot-20261116.json'


class ProjectionRoiTests(unittest.TestCase):
    def test_complete_thirty_cases_remain_diagnostic_even_if_all_faster(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(p,'PLAN',PLAN), patch.object(verify,'PLAN',PLAN), \
                patch.object(test_hosted_timing,'PLAN',PLAN):
            root=Path(tmp)
            test_hosted_timing.make_evidence(root,'diagnostic')
            result=roi.collect(root)
            self.assertEqual((30,6),(result['case_count'],result['decision_count']))
            self.assertFalse(result['accepted'] or result['qualification'] or result['report_imported'] or result['production_optimization'])
            self.assertTrue(result['same_production_under_both_labels'])
            with self.assertRaisesRegex(AssertionError,'cannot qualify'):
                verify.collect(root,'successor')

    def test_scope_rejects_source_harness_sampling_and_consumer_expansion(self):
        original=p.read(PLAN);validate_scope(original)
        for key in ('source','harness','adapter','pair','iterations','warmups','workers','cases','runtime'):
            plan=copy.deepcopy(original)
            if key=='source':plan['sources']['successor']='a'*40
            elif key=='harness':plan['harness_source']='a'*40
            elif key=='adapter':plan['adapter_source']=roi.ADAPTER[:9]+'a'*31
            elif key=='pair':plan['diagnostic_shards'][0]['tasks'][0]['operations'][0]['pair']='TaLib.Functions.Cos'
            elif key=='iterations':plan['measurement_policy']['iterations']=9
            elif key=='warmups':plan['measurement_policy']['fixed_invocation_warmups']=20000
            elif key=='workers':plan['matrix']['max_parallel']=2
            elif key=='cases':plan['cases']=190
            else:plan['runtime_policy_overrides']={'DOTNET_TieredPGO':'0'}
            with self.subTest(key=key),self.assertRaises(AssertionError):validate_scope(plan)

    def test_missing_case_or_failed_cpu_gate_cannot_be_collected(self):
        for invalid in ('missing','cpu'):
            with tempfile.TemporaryDirectory() as tmp,patch.object(p,'PLAN',PLAN),patch.object(verify,'PLAN',PLAN), \
                    patch.object(test_hosted_timing,'PLAN',PLAN):
                root=Path(tmp);test_hosted_timing.make_evidence(root,'diagnostic')
                path=root/'shard0/status.json';state=p.read(path)
                if invalid=='missing':state['rounds'].pop()
                else:state['rounds'][0]['operation_checkpoints'][0]['quiet_cpu_samples'][0]=15.01
                p.write(path,state)
                with self.assertRaises(AssertionError):roi.collect(root)

    def test_authentication_hook_is_required_only_for_this_profile(self):
        plan=p.read(PLAN);proof={'fixture_inventory_sha256':'inventory'}
        with patch.object(p,'read',side_effect=[proof,plan]),patch.object(p,'_verify_input_contents'), \
                patch('hosted_timing.fixture_inventory.verify_inventory'),patch.object(roi,'verify_authentication') as auth:
            p.verify_inputs(Path('inputs'))
        auth.assert_called_once_with(Path('inputs'),plan,proof)
