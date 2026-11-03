"""Acceleration uses an integrated accepted baseline and complete mapped consumer scope."""
import copy
import gzip
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
import xml.etree.ElementTree as ET
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.pipeline import REPO, read
from hosted_timing.targeted import verify_reference
from hosted_timing.verify import collect
from hosted_timing.reuse import origin

PLAN = REPO/'benchmarks/plans/hosted-accbands-packed-20261103.json'


class PackedAccelerationQualificationTests(unittest.TestCase):
    def test_real_integrated_baseline_and_source_scope(self):
        verify_reference(read(PLAN))

    def test_builder_only_fixture_is_rejected(self):
        plan = read(PLAN)
        plan['targeted_shards'][1]['tasks'][0]['fixture'] = 'SharedAccelerationBenchmarks'
        with self.assertRaises(KeyError): verify_reference(plan)

    def test_actual_four_arm_fixture_passed_on_matching_binary(self):
        plan = read(PLAN)
        ref = plan['supplemental_fixture_validation']
        def blob(path):
            return subprocess.check_output(['git','show',ref['commit']+':'+ref['directory']+'/'+path],cwd=REPO)
        proof = json.loads(blob('verification.json'))
        trx = gzip.decompress(blob('fixture.trx.gz'))
        self.assertEqual(hashlib.sha256(trx).hexdigest(),proof['sha256'])
        results = ET.fromstring(trx).findall('.//{*}UnitTestResult')
        self.assertEqual(27,len(results))
        self.assertTrue(all(r.attrib['outcome']=='Passed' for r in results))
        self.assertTrue(any('FourEagerArmsCoverBothMappingsAndSizes' in r.attrib['testName'] for r in results))
        self.assertEqual(plan['sources']['successor'],proof['source'])
        original = json.loads(subprocess.check_output(['git','show',proof['source']+
            ':benchmarks/results/remaining-indicators/accbands-compact-20261103/verification.json'],cwd=REPO))
        self.assertEqual(original['production_hashes']['src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll'],proof['production_sha256'])
        self.assertEqual(original['source_fingerprint'],plan['source_correctness']['source_sha256'])

    def test_real_reused_preparation_identity(self):
        plan = read(PLAN)
        ref = plan['supplemental_fixture_validation']
        path = 'benchmarks/results/remaining-indicators/accbands-incomplete-'+plan['reuse_inputs']['run_id']+'/prepared/provenance.json.gz'
        raw = gzip.decompress(subprocess.check_output(['git','show',ref['commit']+':'+path],cwd=REPO))
        with tempfile.TemporaryDirectory() as temp:
            proof = Path(temp)/'original.json';proof.write_bytes(raw)
            _, original = origin(plan,proof)
        self.assertEqual(plan['sources'],original['sources'])

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
