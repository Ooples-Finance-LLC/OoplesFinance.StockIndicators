"""Require the complete stochastic consumer scope and authenticated baseline."""
import copy
import hashlib
import json
import subprocess
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
import test_hosted_timing
from hosted_timing.pipeline import REPO, read
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect

PLAN = REPO/'benchmarks/plans/hosted-stoch-compact-smoothing-20261103.json'

class StochCompactTests(unittest.TestCase):
    expected_totals = (30,6)
    def test_real_baseline_and_source_scope(self):
        verify_reference(read(PLAN))

    def test_wrong_fixture_methods_or_pair_rejected(self):
        for key,value in [('fixture','SharedSmoothedStochasticBenchmarks'),
                          ('implicit_pair',True),('methods',['BuilderFull','BuilderLatestOnly']),
                          ('operations',[dict(raw='Skender.GetSmi',pair='Skender.GetSmi')])]:
            plan=copy.deepcopy(read(PLAN))
            plan['targeted_shards'][1]['tasks'][0][key]=value
            with self.assertRaises((AssertionError,KeyError)): verify_reference(plan)

    def test_unintegrated_or_unreported_baseline_rejected(self):
        plan=read(PLAN);ref=plan['targeted_reference']
        receipt=json.loads(subprocess.check_output(['git','show',plan['sources']['published']+':'+ref['decision']],cwd=REPO))
        for flag in ('production_integrated','report_imported'):
            broken=copy.deepcopy(receipt);broken[flag]=False
            raw=json.dumps(broken).encode()
            changed=copy.deepcopy(plan);changed['targeted_reference']['sha256']=hashlib.sha256(raw).hexdigest()
            with patch('hosted_timing.targeted.subprocess.check_output',return_value=raw):
                with self.assertRaises(AssertionError): verify_reference(changed)

    def test_complete_scope_and_both_shards_required(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            with patch('hosted_timing.pipeline.PLAN',PLAN),patch('hosted_timing.verify.PLAN',PLAN),patch('test_hosted_timing.PLAN',PLAN):
                test_hosted_timing.make_evidence(root,'targeted')
                result=collect(root,'targeted')
                self.assertEqual(self.expected_totals,(result['case_count'],result['decision_count']))
                self.assertTrue(result['accepted'])
                (root/'shard1/provenance.json').unlink()
                with self.assertRaises(AssertionError): collect(root,'targeted')
