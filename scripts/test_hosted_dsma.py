"""DSMA uses an integrated accepted baseline and complete mapped consumer scope."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.pipeline import REPO, read
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect

PLAN = REPO/'benchmarks/plans/hosted-dsma-filter-interval-20261103.json'


class DsmaQualificationTests(unittest.TestCase):
    def test_real_integrated_baseline_and_source_scope(self):
        verify_reference(read(PLAN))

    def test_only_shared_fixture_uses_implicit_pair(self):
        for index in (0, 1):
            plan = read(PLAN)
            task = plan['targeted_shards'][index]['tasks'][0]
            task['implicit_pair'] = not task['implicit_pair']
            with self.assertRaises(AssertionError): verify_reference(plan)

    def test_unintegrated_or_unreported_baseline_rejected(self):
        plan = read(PLAN)
        ref = plan['targeted_reference']
        receipt = json.loads(subprocess.check_output(['git','show',plan['sources']['published']+':'+ref['decision']],cwd=REPO))
        for flag in ('production_integrated','report_imported'):
            broken = copy.deepcopy(receipt);broken[flag] = False
            raw = json.dumps(broken).encode()
            changed = copy.deepcopy(plan);changed['targeted_reference']['sha256'] = hashlib.sha256(raw).hexdigest()
            with patch('hosted_timing.targeted.subprocess.check_output',return_value=raw):
                with self.assertRaises(AssertionError): verify_reference(changed)

    def test_complete_thirty_cases_and_both_shards_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            with patch('hosted_timing.pipeline.PLAN',PLAN),patch('hosted_timing.verify.PLAN',PLAN),patch('test_hosted_timing.PLAN',PLAN):
                test_hosted_timing.make_evidence(root,'targeted')
                result = collect(root,'targeted')
                self.assertEqual((30,6),(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])
                (root/'shard1/provenance.json').unlink()
                with self.assertRaises(AssertionError): collect(root,'targeted')
