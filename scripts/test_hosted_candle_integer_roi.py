import copy
import hashlib
import json
from pathlib import Path
import runpy
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p, candle_integer_roi as roi, candle_family_roi as common, verify
from hosted_timing.roi_scope_dispatch import validate_scope

PLAN = p.REPO / 'benchmarks/plans/hosted-candle-integer-window-native-pilot-20261115.json'


class IntegerRoiTests(unittest.TestCase):
    def evidence(self, root):
        with patch.object(test_hosted_timing, 'PLAN', PLAN):
            test_hosted_timing.make_evidence(root, 'diagnostic')

    def test_twenty_complete_cases_cannot_qualify_or_import_source(self):
        with tempfile.TemporaryDirectory() as work, patch.object(p, 'PLAN', PLAN), patch.object(verify, 'PLAN', PLAN):
            root = Path(work)
            self.evidence(root)
            result = roi.collect(root)
            self.assertEqual((20, 4), (result['case_count'], result['decision_count']))
            self.assertEqual({'faster': 4}, result['verdicts'])
            self.assertFalse(result['accepted'] or result['qualification'] or result['report_imported'])
            with self.assertRaisesRegex(AssertionError, 'cannot qualify'):
                verify.collect(root, 'successor')

    def test_scope_keeps_two_pairs_counts_sources_and_policy_pinned(self):
        original = p.read(PLAN)
        validate_scope(original)
        for key in ('cases', 'decisions', 'pair', 'source', 'ci', 'warmup', 'runtime', 'workers'):
            plan = copy.deepcopy(original)
            if key == 'cases': plan['cases'] = 30
            elif key == 'decisions': plan['decisions'] = 6
            elif key == 'pair': plan['diagnostic_shards'][0]['tasks'][0]['operations'][0]['pair'] = common.PAIRS[0]
            elif key == 'source': plan['sources']['successor'] = common.NEW
            elif key == 'ci': plan['candidate']['ci_run'] = '43759123590'
            elif key == 'warmup': plan['measurement_policy']['fixed_invocation_warmups'] = 20000
            elif key == 'runtime': plan['runtime_policy_overrides'] = {'DOTNET_TieredPGO': '0'}
            else: plan['matrix']['max_parallel'] = 2
            with self.subTest(key=key), self.assertRaises(AssertionError): validate_scope(plan)
        with self.assertRaises(AssertionError): validate_scope(dict(diagnostic_kind='unknown'))

    def test_partial_cases_and_failed_cpu_gate_are_rejected(self):
        for invalid in ('cpu', 'missing'):
            with tempfile.TemporaryDirectory() as work, patch.object(p, 'PLAN', PLAN), patch.object(verify, 'PLAN', PLAN):
                root = Path(work)
                self.evidence(root)
                path = root / 'shard0/status.json'
                state = p.read(path)
                if invalid == 'cpu': state['rounds'][0]['operation_checkpoints'][0]['quiet_cpu_samples'][0] = 15.01
                else: state['rounds'].pop()
                p.write(path, state)
                with self.assertRaises(AssertionError): roi.collect(root)

    def test_cli_uses_integer_scope_and_resolves_paths_before_measurement(self):
        with patch.object(sys, 'argv', ['run-candle-family-roi.py', 'measure', '--inputs', 'inputs', '--out', 'output']), \
                patch.object(p, 'PLAN', PLAN), patch.object(roi, 'validate_scope'), \
                patch.object(p, 'verify_inputs') as inputs, patch.object(p, 'measure') as measure:
            runpy.run_path(str(p.REPO / 'scripts/run-candle-family-roi.py'), run_name='__main__')
        inputs.assert_called_once_with(Path('inputs').resolve())
        measure.assert_called_once_with(Path('inputs').resolve(), Path('output').resolve(), 'diagnostic', 0)

    def test_previous_candidate_ci_cannot_supply_integer_source_evidence(self):
        with tempfile.TemporaryDirectory() as work, patch.object(common, 'source_fingerprint', return_value='fingerprint'):
            root = Path(work)
            raw = json.dumps(dict(run_id='43759123590', workflow_head=roi.PRODUCER)).encode()
            (root / 'manifest.json').write_bytes(raw)
            plan = copy.deepcopy(p.read(PLAN))
            plan['candidate']['manifest_sha256'] = hashlib.sha256(raw).hexdigest()
            with self.assertRaises(AssertionError): roi.candidate_inputs(root, plan)

    def test_frozen_json_survives_actual_windows_checkout_filters(self):
        for path, expected in ((PLAN, hashlib.sha256(PLAN.read_bytes()).hexdigest()),
                               (p.REPO / roi.APPROVAL, roi.APPROVAL_SHA)):
            raw = subprocess.check_output(['git', '-c', 'core.autocrlf=true', '-c', 'core.eol=crlf',
                'cat-file', '--filters', 'HEAD:' + path.relative_to(p.REPO).as_posix()], cwd=p.REPO)
            self.assertNotIn(b'\r\n', raw)
            self.assertEqual(expected, hashlib.sha256(raw).hexdigest())


if __name__ == '__main__':
    unittest.main()
