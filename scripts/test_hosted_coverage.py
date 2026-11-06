"""Coverage is complete evidence for unchanged code, never source qualification."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from types import SimpleNamespace
from unittest.mock import patch

import test_hosted_timing
from hosted_timing.coverage import tasks, verify_reference
from hosted_timing.pipeline import DLL, PLAN, digest, manifest, measure, read, write
from hosted_timing.verify import collect


class CoverageEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.plan = read(PLAN)
        # This legacy synthetic runtime deliberately has no dependency assets.
        self.plan.pop('runtime_dependency_alignment', None)
        self.plan.pop('coverage_shards', None)
        self.plan.pop('coverage_harness', None)
        self.plan['measurement_policy'].pop('operation_timeout_seconds', None)
        self.plan.pop('standalone_variants', None)
        self.plan['coverage_reference'] = {'published': 'baseline'}
        self.plan['qualify_variants'] = self.plan['trix_preflight'] = False
        self.path = self.root/'plan.json'
        write(self.path, self.plan)
        for module in ('hosted_timing.pipeline', 'hosted_timing.verify', 'test_hosted_timing'):
            self.enterContext(patch(module + '.PLAN', self.path))
        self.enterContext(patch('hosted_timing.coverage.verify_reference'))
        self.evidence = self.root/'evidence'
        self.evidence.mkdir()
        test_hosted_timing.make_evidence(self.evidence, 'coverage')

    def test_complete_coverage_cannot_qualify_even_with_faster_control_timings(self):
        result = collect(self.evidence, 'coverage')
        self.assertEqual((40, 8), (result['case_count'], result['decision_count']))
        self.assertEqual(8, result['verdicts']['faster'])
        self.assertTrue(result['coverage_complete'])
        self.assertFalse(result['accepted'])
        self.assertEqual(result['sources']['old'], result['sources']['new'])

    def test_missing_control_round_is_rejected(self):
        path = self.evidence/'shard0/status.json'
        state = read(path)
        state['rounds'].pop()
        write(path, state)
        with self.assertRaises(AssertionError):
            collect(self.evidence, 'coverage')

    def test_different_production_binary_in_one_arm_is_rejected(self):
        path = self.evidence/'shard0/provenance.json'
        proof = read(path)
        proof['binary_manifest']['new']['OoplesFinance.StockIndicators.dll'] = 'other'
        write(path, proof)
        with self.assertRaises(AssertionError):
            collect(self.evidence, 'coverage')

    def test_scope_contains_only_the_two_missing_builder_pairs(self):
        selected = tasks()
        self.assertEqual({'Trady.Indicator.DynamicMomentumIndex', 'Trady.Candlestick.RisingThreeMethods'},
                         {op['pair'] for task in selected for op in task['operations']})
        self.assertEqual(40, sum(task['cases'] for task in selected))

    def test_runtime_accepts_identical_coverage_arms_but_rejects_identical_optimization_arms(self):
        inputs = self.root/'inputs'
        for directory in ('common', 'production', 'evidence'):
            (inputs/directory).mkdir(parents=True)
        (inputs/'common'/DLL).write_bytes(b'harness-production')
        for label in ('successor', 'variants'):
            (inputs/'production'/(label+'.dll')).write_bytes(b'qualified-production')
        proof = dict(sources=self.plan['sources'], plan_sha256=digest(self.path),
                     sdk=self.plan['proposed_dotnet_sdk'], common_manifest=manifest(inputs/'common'),
                     evidence_manifest={}, production_sha256={
                         label:digest(inputs/'production'/(label+'.dll')) for label in ('successor', 'variants')})
        write(inputs/'provenance.json', proof)

        def checkpoint(**arguments):
            arguments['assert_binaries']()
            self.assertEqual('coverage', arguments['context']['phase'])
            return dict(cases=2*len(arguments['pairs'])*len(arguments['methods']))

        with patch.dict('sys.modules', {'psutil': SimpleNamespace(cpu_count=lambda:2)}), \
                patch('hosted_timing.pipeline.subprocess.check_output', return_value=proof['sdk']), \
                patch('hosted_timing.pipeline.shutil.disk_usage', return_value=SimpleNamespace(free=3*1024**3)), \
                patch('hosted_timing.pipeline.run_checkpointed', side_effect=checkpoint) as run:
            out = self.root/'coverage-runtime-check'
            measure(inputs, out, 'coverage', 0)
            self.assertEqual(8, run.call_count)
            self.assertEqual('raw-measurements-complete', read(out/'status.json')['status'])
            with self.assertRaises(AssertionError):
                measure(inputs, self.root/'variants-runtime-check', 'variants', 0)


class CoverageReferenceTests(unittest.TestCase):
    def setUp(self):
        self.decision = dict(accepted=True, phase='successor', case_count=1860, decision_count=372,
                             verdicts={'faster': 13}, run_id='accepted-run', sources={'new': 'measured'})
        self.plan = dict(sources={'successor': 'measured'}, coverage_reference=dict(
            published='baseline', decision='receipt.json', run_id='accepted-run'))

    def verify(self, source_diff=''):
        raw = json.dumps(self.decision).encode()
        self.plan['coverage_reference'].setdefault('sha256', hashlib.sha256(raw).hexdigest())
        with patch('hosted_timing.coverage.subprocess.check_output', return_value=raw), \
                patch('hosted_timing.coverage.git', return_value=source_diff):
            verify_reference(self.plan)

    def test_accepts_unchanged_qualified_source(self):
        self.verify()

    def test_rejects_changed_source_or_receipt(self):
        with self.assertRaises(AssertionError):
            self.verify(source_diff='src/Other.cs')
        self.plan['coverage_reference']['sha256'] = 'changed'
        with self.assertRaises(AssertionError):
            self.verify()

    def test_rejects_incomplete_or_rejected_baseline(self):
        for change in [dict(accepted=False), dict(case_count=48), dict(verdicts={'slower': 1}),
                       dict(sources={'new': 'other'}), dict(run_id='other')]:
            with self.subTest(change=change):
                original = self.decision.copy()
                self.decision.update(change)
                with self.assertRaises(AssertionError):
                    self.verify()
                self.decision = original
                self.plan['coverage_reference'].pop('sha256', None)
