"""Authenticate reused binaries, original checks, and the new plan binding."""
from pathlib import Path
import copy
import gzip
import hashlib
import os
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing import pipeline
from hosted_timing.reuse import prepare_reused


def original_inputs(root):
    plan = copy.deepcopy(pipeline.read(pipeline.PLAN))
    plan.pop('reuse_inputs', None)
    plan['measurement_policy'].pop('fixed_invocation_warmups', None)
    plan['gpu_sources'] = {label:pipeline.original_gpu_source(plan, label) for label in ('old','successor','variants')}
    plan['cpu_test_classes'] = {label:['SyntheticTests'] for label in ('old','successor','variants')}
    old_plan = root/'old-plan.json'
    pipeline.write(old_plan, plan)
    source = root/'original'
    for name in ('common','production','evidence'):
        (source/name).mkdir(parents=True)
    (source/'common'/pipeline.DLL).write_bytes(b'variant')
    (source/'common/harness.dll').write_bytes(b'common harness')
    checks = {}

    def trx(name, count):
        return ('<TestRun><Results>'+''.join(
            f'<UnitTestResult testName="{name}.Case{i}" outcome="Passed"/>' for i in range(count))
            +'</Results></TestRun>').encode()

    for label in ('old','successor','variants'):
        (source/'production'/(label+'.dll')).write_bytes(label.encode())
        path = source/'evidence'/(label+'.trx')
        path.write_bytes(trx('SyntheticTests', 1))
        checks[label] = pipeline.verify_trx(path, ['SyntheticTests'])
        checks[label]['original_gpu_source'] = pipeline.original_gpu_source(plan, label)
        gpu = trx('SyntheticGpuTests', 100)
        pipeline.write(source/'evidence'/(label+'-original-gpu-receipt.json'),
            dict(passed_cases=100, trx_sha256=hashlib.sha256(gpu).hexdigest()))
        (source/'evidence'/(label+'-original-gpu-gpu.trx.gz')).write_bytes(gzip.compress(gpu, mtime=0))
    fixture = source/'evidence/variant-harness.trx'
    fixture.write_bytes(trx('SharedClassicalWindowVariantBenchmarkTests', 13))
    checks['variant-harness'] = pipeline.verify_trx(fixture, ['SharedClassicalWindowVariantBenchmarkTests'])
    proof = dict(sources=plan['sources'], plan_sha256=pipeline.digest(old_plan),
        sdk=plan['proposed_dotnet_sdk'], checks=checks, run_id='123', run_attempt='1', workflow_head='original-head',
        common_manifest=pipeline.manifest(source/'common'), evidence_manifest=pipeline.manifest(source/'evidence'),
        production_sha256={label:pipeline.digest(source/'production'/(label+'.dll')) for label in ('old','successor','variants')})
    pipeline.write(source/'provenance.json', proof)
    new_plan = copy.deepcopy(plan)
    new_plan['measurement_policy']['fixed_invocation_warmups'] = 1000
    new_plan['reuse_inputs'] = dict(run_id=123, workflow_head='original-head', plan=str(old_plan),
        plan_sha256=pipeline.digest(old_plan), provenance_sha256=pipeline.digest(source/'provenance.json'))
    path = root/'new-plan.json'
    pipeline.write(path, new_plan)
    return source, path


class ReuseTests(unittest.TestCase):
    def reuse(self, source, out, plan):
        with patch.object(pipeline, 'PLAN', plan), patch.object(pipeline, 'git', return_value='new-head'), \
             patch.dict(os.environ, GITHUB_RUN_ID='456', GITHUB_RUN_ATTEMPT='2'), \
             patch.object(pipeline, 'command', side_effect=AssertionError('No rebuild is permitted')):
            return prepare_reused(source, out)

    def test_reuse_preserves_checks_and_records_original_preparation(self):
        with tempfile.TemporaryDirectory() as temp:
            source, plan = original_inputs(Path(temp))
            original = pipeline.read(source/'provenance.json')
            out = Path(temp)/'rebound'
            proof = self.reuse(source, out, plan)
            self.assertEqual(proof['run_id'], '456')
            self.assertEqual(proof['workflow_head'], 'new-head')
            self.assertEqual(proof['checks'], original['checks'])
            self.assertEqual(proof['preparation_mode'], 'reused')
            self.assertEqual((out/'original-preparation.json').read_bytes(), (source/'provenance.json').read_bytes())
            with patch.object(pipeline, 'PLAN', plan):
                self.assertEqual(pipeline.verify_inputs(out), proof)
                with (out/'original-preparation.json').open('ab') as handle:
                    handle.write(b'\n')
                with self.assertRaises(AssertionError): pipeline.verify_inputs(out)

    def test_tampered_provenance_or_binary_is_rejected_before_copy(self):
        for relative in ('provenance.json','production/old.dll'):
            with self.subTest(relative=relative), tempfile.TemporaryDirectory() as temp:
                source, plan = original_inputs(Path(temp))
                with (source/relative).open('ab') as handle:
                    handle.write(b'\n')
                out = Path(temp)/'rebound'
                with self.assertRaises(AssertionError): self.reuse(source, out, plan)
                self.assertFalse(out.exists())

    def test_different_sources_sdk_cpu_checks_or_gpu_identity_cannot_reuse(self):
        for field in ('source','sdk','cpu','gpu'):
            with self.subTest(field=field), tempfile.TemporaryDirectory() as temp:
                source, path = original_inputs(Path(temp))
                plan = pipeline.read(path)
                if field == 'source': plan['sources']['successor'] = 'unverified-source'
                elif field == 'sdk': plan['proposed_dotnet_sdk'] = 'different-sdk'
                elif field == 'cpu': plan['cpu_test_classes']['successor'].append('UnrunTests')
                else: plan['gpu_sources']['successor'] = 'unverified-gpu-source'
                pipeline.write(path, plan)
                with self.assertRaises(AssertionError): self.reuse(source, Path(temp)/'rebound', path)
