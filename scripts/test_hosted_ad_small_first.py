"""A/D scope includes all three mappings and their actual fixture parameter names."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.pipeline import REPO, read, write
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect

PLAN = REPO/'benchmarks/plans/hosted-ad-small-first-20261103.json'


class AdSmallFirstReferenceTests(unittest.TestCase):
    def test_real_source_boundary_and_complete_scope(self):
        verify_reference(read(PLAN))

    def test_missing_consumer_or_wrong_raw_parameter_rejected(self):
        for mode in ('missing','raw'):
            plan = copy.deepcopy(read(PLAN))
            task = plan['targeted_shards'][0]['tasks'][1]
            if mode == 'missing': task['operations'].clear()
            else: task['operations'][0]['raw']='TaLib.Functions.Ad'
            with self.assertRaises(AssertionError): verify_reference(plan)


class AdSmallFirstEvidenceTests(unittest.TestCase):
    def test_complete_scope_and_missing_pair_shard(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); path = root/'plan.json'; write(path,read(PLAN))
            with patch('hosted_timing.pipeline.PLAN',path), patch('hosted_timing.verify.PLAN',path), \
                    patch('test_hosted_timing.PLAN',path):
                test_hosted_timing.make_evidence(root,'targeted')
                result = collect(root,'targeted')
                self.assertEqual((90,18),(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])
                (root/'shard1/provenance.json').unlink()
                with self.assertRaises(AssertionError): collect(root,'targeted')
