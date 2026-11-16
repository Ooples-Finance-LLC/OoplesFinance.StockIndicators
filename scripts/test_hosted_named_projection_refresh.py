import copy
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p, named_projection_refresh as refresh, verify
from hosted_timing.baseline_refresh_dispatch import implementation

PLAN = p.REPO/'benchmarks/plans/hosted-named-projection-refresh-20261116.json'


class NamedProjectionRefreshTests(unittest.TestCase):
    def test_all_eight_pairs_both_sizes_and_unchanged_warmups(self):
        plan = p.read(PLAN)
        refresh.validate_scope(plan)
        self.assertIs(implementation(plan), refresh)
        self.assertEqual(list(range(8)), list(p.phase_shards(plan, 'baseline-refresh')))
        for shard, pair in enumerate(refresh.PAIRS):
            task = p.phase_tasks(plan, 'baseline-refresh', shard)[0]
            self.assertEqual([dict(raw=pair, pair=pair)], task['operations'])
            self.assertEqual(1000, p.warmups_for(plan, 'LibraryPairBenchmarks', task))
            self.assertEqual({'warmup_count':1000}, p.warmup_context(plan, 'LibraryPairBenchmarks', task))

    def test_every_aa_faster_still_cannot_qualify_production(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(p, 'PLAN', PLAN), \
                patch.object(verify, 'PLAN', PLAN), patch.object(test_hosted_timing, 'PLAN', PLAN):
            root = Path(tmp)
            test_hosted_timing.make_evidence(root, 'baseline-refresh', same_production=True)
            result = refresh.collect(root)
            self.assertEqual((80, 16), (result['case_count'], result['decision_count']))
            self.assertEqual({'faster':16}, result['verdicts'])
            self.assertFalse(result['accepted'] or result['production_qualification'] or result['production_optimization'])
            self.assertEqual(16, len(result['native_review']['rows']))
            self.assertTrue(all(len(row['owned_rounds']) == 4 for row in result['native_review']['rows']))
            with self.assertRaisesRegex(AssertionError, 'cannot qualify'):
                verify.collect(root, 'successor')
            (root/'shard7/provenance.json').unlink()
            with self.assertRaises(AssertionError):
                refresh.collect(root)

    def test_expanded_or_weakened_scope_is_rejected(self):
        original = p.read(PLAN)
        for key in ('source', 'pair', 'warmup', 'iterations', 'runners', 'gpu', 'qualification', 'frozen', 'ambiguous'):
            plan = copy.deepcopy(original)
            if key == 'source': plan['sources']['successor'] = 'a'*40
            elif key == 'pair': plan['refresh_shards'].pop()
            elif key == 'warmup': plan['measurement_policy']['fixed_invocation_warmups'] = 20000
            elif key == 'iterations': plan['measurement_policy']['iterations'] = 9
            elif key == 'runners': plan['matrix']['max_parallel'] = 3
            elif key == 'gpu': plan['gpu_sources']['old'] = refresh.PUBLISHED
            elif key == 'qualification': plan['production_qualification'] = True
            elif key == 'frozen': plan['frozen_candidate']['source'] = refresh.FROZEN_BASE
            else: plan['pointwise_family_refresh'] = True
            with self.subTest(key=key), self.assertRaises(AssertionError):
                refresh.validate_scope(plan)
        with self.assertRaises(AssertionError): implementation({})
        with self.assertRaises(AssertionError): implementation(dict(pointwise_family_refresh=True, named_output_projection_refresh=True))

    def test_runtime_identity_includes_every_dependency(self):
        plan = p.read(PLAN)
        manifests = dict(old={p.DLL:'canonical', 'harness.dll':'new', 'tensor.dll':'tensor'},
                         new={p.DLL:'canonical', 'harness.dll':'new', 'tensor.dll':'tensor'})
        refresh.verify_runtime_identity(plan, manifests)
        for name in manifests['new']:
            broken = copy.deepcopy(manifests)
            broken['new'][name] = 'changed'
            with self.subTest(name=name), self.assertRaises(AssertionError):
                refresh.verify_runtime_identity(plan, broken)

    def test_failed_cpu_gate_cannot_be_imported(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(p, 'PLAN', PLAN), \
                patch.object(verify, 'PLAN', PLAN), patch.object(test_hosted_timing, 'PLAN', PLAN):
            root = Path(tmp)
            test_hosted_timing.make_evidence(root, 'baseline-refresh', same_production=True)
            path = root/'shard0/status.json'
            state = p.read(path)
            state['rounds'][0]['operation_checkpoints'][0]['quiet_cpu_samples'][0] = 15.01
            p.write(path, state)
            with self.assertRaises(AssertionError): refresh.collect(root)
