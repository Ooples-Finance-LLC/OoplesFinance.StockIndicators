"""Final source union and complete same-VM consumer coverage."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.pipeline import REPO, read
from hosted_timing.targeted import expected_totals, verify_reference
from hosted_timing.verify import collect

PLAN = REPO/'benchmarks/plans/hosted-combined-accepted-20261103.json'


class CombinedAcceptedTests(unittest.TestCase):
    def test_real_combined_source_and_complete_scope(self):
        verify_reference(read(PLAN))

    def test_missing_consumer_rejected(self):
        plan = copy.deepcopy(read(PLAN))
        plan['targeted_shards'][0]['tasks'][0]['operations'].pop()
        with self.assertRaises(AssertionError): verify_reference(plan)

    def test_all_six_shards_required_for_acceptance(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with patch('hosted_timing.pipeline.PLAN',PLAN), patch('hosted_timing.verify.PLAN',PLAN), patch('test_hosted_timing.PLAN',PLAN):
                test_hosted_timing.make_evidence(root,'targeted')
                result = collect(root,'targeted')
                self.assertEqual(expected_totals(read(PLAN)),(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])
                (root/'shard5/provenance.json').unlink()
                with self.assertRaises(AssertionError): collect(root,'targeted')
