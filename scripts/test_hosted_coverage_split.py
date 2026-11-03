"""Each coverage pair owns one VM and all four fresh rounds."""
import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.coverage import verify_reference
from hosted_timing.pipeline import REPO, phase_shards, phase_tasks, read, write
from hosted_timing.verify import collect
from hosted_timing.archive import archive


PLAN = REPO/'benchmarks/plans/hosted-builder-coverage-repair-20261103.json'


class SplitCoverageTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        self.path = self.root/'plan.json'
        self.plan = read(PLAN)
        write(self.path,self.plan)
        for module in ('hosted_timing.pipeline','hosted_timing.verify','test_hosted_timing'):
            self.enterContext(patch(module+'.PLAN',self.path))
        self.enterContext(patch('hosted_timing.coverage.verify_reference'))
        self.evidence = self.root/'evidence'; self.evidence.mkdir()
        test_hosted_timing.make_evidence(self.evidence,'coverage')

    def test_complete_two_shards_never_qualify_source(self):
        self.assertEqual([0,1],list(phase_shards(self.plan,'coverage')))
        self.assertEqual([1,1],[len(phase_tasks(self.plan,'coverage',i)) for i in (0,1)])
        result = collect(self.evidence,'coverage')
        self.assertEqual((40,8),(result['case_count'],result['decision_count']))
        self.assertTrue(result['coverage_complete']); self.assertFalse(result['accepted'])
        self.assertEqual({0,1},{d['provenance']['shard'] for d in result['decisions']})

    def test_missing_pair_shard_rejected(self):
        (self.evidence/'shard1/provenance.json').unlink()
        with self.assertRaises(AssertionError): collect(self.evidence,'coverage')

    def test_archive_requires_unchanged_production_and_preserves_coverage_identity(self):
        prepared = self.root/'prepared'; (prepared/'evidence').mkdir(parents=True)
        proof = read(self.evidence/'input.json'); write(prepared/'provenance.json',proof)
        with patch('hosted_timing.archive.PLAN',self.path), \
                patch('hosted_timing.archive.verify_inputs',return_value=proof), \
                patch('hosted_timing.archive.subprocess.check_output',return_value=b''):
            destination = archive(self.evidence,prepared,self.root/'publishing','coverage')
        decision = read(destination/'decision.json')
        self.assertEqual('coverage-complete',decision['status'])
        self.assertTrue(decision['coverage_complete']); self.assertFalse(decision['accepted'])
        self.assertFalse(decision['source_qualification_claimed'])

    def test_partial_or_mixed_rounds_rejected(self):
        path = self.evidence/'shard1/status.json'
        state = read(path); state['rounds'].pop(); write(path,state)
        with self.assertRaises(AssertionError): collect(self.evidence,'coverage')


class CoverageHarnessBoundaryTests(unittest.TestCase):
    def test_real_frozen_sources_change_only_two_config_attributes_and_config_file(self):
        verify_reference(read(PLAN))

    def test_bad_config_receipt_or_job_budget_rejected(self):
        original = read(PLAN)
        for mode in ('hash','budget','timeout','source'):
            with self.subTest(mode=mode):
                plan = copy.deepcopy(original)
                if mode == 'hash': plan['coverage_harness']['config_sha256']='wrong'
                if mode == 'budget': plan['matrix']['timeout_minutes_per_shard']=241
                if mode == 'timeout': plan['coverage_harness']['execution_timeout_minutes']=121
                if mode == 'source': plan['sources']['variants']=plan['coverage_harness']['baseline']
                with self.assertRaises(AssertionError): verify_reference(plan)
