"""Authenticate all eleven rolling moment mappings and their boolean fixture."""
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.pipeline import REPO, read, write
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect

PLAN = REPO/'benchmarks/plans/hosted-dispersion-20261103.json'


class DispersionScopeTests(unittest.TestCase):
    def test_real_complete_source_and_mapping_boundary(self):
        verify_reference(read(PLAN))

    def test_boolean_alias_or_missing_consumer_is_rejected(self):
        for mode in ('alias','missing'):
            plan=read(PLAN)
            task=next(t for s in plan['targeted_shards'] for t in s['tasks']
                if t['fixture']=='SharedDispersionBenchmarks')
            if mode=='alias':task['operations'][0]['raw']='TaLib.Functions.Var'
            else:task['operations'].pop()
            with self.assertRaises(AssertionError):verify_reference(plan)

    def test_complete_330_cases_and_missing_shard(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);path=root/'plan.json';write(path,read(PLAN))
            with patch('hosted_timing.pipeline.PLAN',path),patch('hosted_timing.verify.PLAN',path), \
                    patch('test_hosted_timing.PLAN',path):
                test_hosted_timing.make_evidence(root,'targeted');result=collect(root,'targeted')
                self.assertEqual((330,66),(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])
                self.assertEqual(11,len({d['pair'] for d in result['decisions']}))
                (root/'shard1/provenance.json').unlink()
                with self.assertRaises(AssertionError):collect(root,'targeted')
