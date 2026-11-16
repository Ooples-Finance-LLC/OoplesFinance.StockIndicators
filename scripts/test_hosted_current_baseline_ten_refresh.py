import copy
import hashlib
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch

import test_hosted_timing
from hosted_timing import pipeline as p, current_baseline_ten_refresh as refresh, verify
from hosted_timing.baseline_refresh_dispatch import implementation

PLAN = p.REPO/'benchmarks/plans/hosted-current-baseline-ten-refresh-20261116.json'


def decisions():
    return [dict(pair=pair, bars=size, method='Ooples', verdict='inconclusive')
        for pair in refresh.PAIRS for size in (1000, 10000)]


class CurrentBaselineTenRefreshTests(unittest.TestCase):
    def test_plan_approval_and_preflight_bytes_survive_checkout_filters(self):
        plan = p.read(PLAN)
        paths = [PLAN, p.REPO/refresh.APPROVAL]
        paths.extend(p.REPO/'benchmarks/plans/evidence/current-baseline-ten-preflight-20261116'/relative
            for relative in plan['consumer_preflight']['files'])
        for path in paths:
            relative = path.relative_to(p.REPO).as_posix()
            for autocrlf in ('true', 'false'):
                raw = subprocess.check_output(['git', '-c', 'core.autocrlf='+autocrlf,
                    'cat-file', '--filters', 'HEAD:'+relative], cwd=p.REPO)
                with self.subTest(path=relative, autocrlf=autocrlf):
                    self.assertEqual(p.digest(path), hashlib.sha256(raw).hexdigest())
                    if path in (PLAN, p.REPO/refresh.APPROVAL): self.assertNotIn(b'\r\n', raw)

    def test_exact_ten_pairs_one_pair_per_shard_and_policy(self):
        plan = p.read(PLAN)
        refresh.validate_scope(plan)
        self.assertIs(implementation(plan), refresh)
        self.assertEqual(list(range(10)), list(p.phase_shards(plan, 'baseline-refresh')))
        for shard, pair in enumerate(refresh.PAIRS):
            task = p.phase_tasks(plan, 'baseline-refresh', shard)[0]
            self.assertEqual([dict(raw=pair, pair=pair)], task['operations'])
            self.assertEqual(1000, p.warmups_for(plan, 'LibraryPairBenchmarks', task))
            self.assertEqual({'warmup_count':1000}, p.warmup_context(plan, 'LibraryPairBenchmarks', task))

    def test_admission_requires_both_sizes_and_quarantines_whole_pair(self):
        data = decisions()
        data[0]['verdict'] = 'faster'
        result = refresh.admission(data)
        self.assertEqual(refresh.PAIRS[1:], result['admitted_pairs'])
        self.assertEqual([dict(pair=refresh.PAIRS[0], sizes={'1000':'faster', '10000':'inconclusive'})], result['quarantined_pairs'])
        self.assertEqual((18, 784, 1544), (result['admitted_baseline_rows'],
            result['unchanged_baseline_rows'], result['unchanged_builder_comparisons']))
        self.assertTrue(result['admission_is_not_equivalence'])

    def test_all_twenty_inconclusive_admits_only_these_twenty_rows(self):
        result = refresh.admission(decisions())
        self.assertEqual(refresh.PAIRS, result['admitted_pairs'])
        self.assertEqual((20, 782), (result['admitted_baseline_rows'], result['unchanged_baseline_rows']))
        self.assertFalse(result['zero_admitted_no_publication'])

    def test_no_admission_never_allows_report_publication(self):
        data = decisions()
        for row in data: row['verdict'] = 'slower'
        result = refresh.admission(data)
        self.assertEqual([], result['admitted_pairs'])
        self.assertEqual(10, len(result['quarantined_pairs']))
        self.assertEqual(802, result['unchanged_baseline_rows'])
        self.assertTrue(result['zero_admitted_no_publication'])

    def test_missing_duplicate_wrong_method_or_size_cannot_admit(self):
        for mutation in ('missing', 'duplicate', 'method', 'size', 'verdict'):
            data = decisions()
            if mutation == 'missing': data.pop()
            elif mutation == 'duplicate': data[-1] = dict(data[0])
            elif mutation == 'method': data[0]['method'] = 'BuilderFull'
            elif mutation == 'size': data[0]['bars'] = 100000
            else: data[0]['verdict'] = 'equivalent'
            with self.subTest(mutation=mutation), self.assertRaises(AssertionError): refresh.admission(data)

    def test_complete_raw_results_preserve_native_uncertainty_but_cannot_qualify(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(p, 'PLAN', PLAN), \
                patch.object(verify, 'PLAN', PLAN), patch.object(test_hosted_timing, 'PLAN', PLAN), \
                patch.object(refresh, '_verify_runtime'):
            root = Path(tmp)
            test_hosted_timing.make_evidence(root, 'baseline-refresh', new_mean=100, same_production=True)
            result = refresh.collect(root)
            self.assertEqual((100,20), (result['case_count'],result['decision_count']))
            self.assertEqual({'inconclusive':20}, result['verdicts'])
            self.assertFalse(result['accepted'] or result['production_qualification'] or result['production_optimization'])
            self.assertEqual(20, len(result['native_review']['rows']))
            self.assertTrue(all(len(row['owned_rounds']) == 4 for row in result['native_review']['rows']))
            for phase in ('successor', 'targeted', 'coverage'):
                with self.subTest(phase=phase), self.assertRaises(AssertionError): verify.collect(root, phase)
            (root/'shard9/provenance.json').unlink()
            with self.assertRaises(AssertionError): refresh.collect(root)

    def test_runtime_identity_covers_every_dependency(self):
        plan = p.read(PLAN)
        manifests = dict(old={p.DLL:'canonical','harness.dll':'frozen','tensor.dll':'tensor'},
                         new={p.DLL:'canonical','harness.dll':'frozen','tensor.dll':'tensor'})
        refresh.verify_runtime_identity(plan, manifests)
        for name in manifests['new']:
            broken = copy.deepcopy(manifests); broken['new'][name] = 'changed'
            with self.subTest(name=name), self.assertRaises(AssertionError): refresh.verify_runtime_identity(plan,broken)

    def test_scope_cannot_expand_change_evidence_or_relax_policy(self):
        original = p.read(PLAN)
        for key in ('source','pair','warmup','iterations','runners','gpu','qualification','ambiguous',
                'preflight','report','runtime','common','candidate'):
            plan = copy.deepcopy(original)
            if key == 'source': plan['sources']['successor'] = 'a'*40
            elif key == 'pair': plan['refresh_shards'].pop()
            elif key == 'warmup': plan['measurement_policy']['fixed_invocation_warmups'] = 20000
            elif key == 'iterations': plan['measurement_policy']['iterations'] = 9
            elif key == 'runners': plan['matrix']['max_parallel'] = 3
            elif key == 'gpu': plan['gpu_sources']['old'] = refresh.PUBLISHED
            elif key == 'qualification': plan['production_qualification'] = True
            elif key == 'ambiguous': plan['named_output_projection_refresh'] = True
            elif key == 'preflight': plan['consumer_preflight']['trx']['passed'] += 1
            elif key == 'report': plan['report_contract']['whole_pair_quarantine'] = False
            elif key == 'runtime': plan['expected_runtime_version'] = '.NET 10.0.14'
            elif key == 'common': plan['frozen_common_manifest'].pop(next(iter(plan['frozen_common_manifest'])))
            else: plan['candidate']['ci_run'] = 'another-run'
            with self.subTest(key=key), self.assertRaises(AssertionError): refresh.validate_scope(plan)

    def test_preflight_files_are_authenticated_and_changes_rejected(self):
        source = p.REPO/'benchmarks/plans/evidence/current-baseline-ten-preflight-20261116'
        with tempfile.TemporaryDirectory() as tmp:
            import shutil
            root = Path(tmp); target = root/'consumer-preflight'; shutil.copytree(source,target)
            counts = refresh._preflight(root)
            self.assertEqual(refresh.PREFLIGHT['trx']['classes'],counts)
            receipt = target/refresh.PREFLIGHT['receipt']; receipt.write_bytes(receipt.read_bytes()+b' ')
            with self.assertRaises(AssertionError): refresh._preflight(root)

    def test_wrong_runtime_and_failed_cpu_gate_cannot_be_imported(self):
        with tempfile.TemporaryDirectory() as tmp, patch.object(p,'PLAN',PLAN), \
                patch.object(verify,'PLAN',PLAN), patch.object(test_hosted_timing,'PLAN',PLAN):
            root = Path(tmp)
            test_hosted_timing.make_evidence(root,'baseline-refresh',new_mean=100,same_production=True)
            with self.assertRaises(AssertionError): refresh._verify_runtime(root)
            path = root/'shard0/status.json'; state = p.read(path)
            state['rounds'][0]['operation_checkpoints'][0]['quiet_cpu_samples'][0] = 15.01
            p.write(path,state)
            with patch.object(refresh,'_verify_runtime'), self.assertRaises(AssertionError): refresh.collect(root)


if __name__ == '__main__':
    unittest.main()
