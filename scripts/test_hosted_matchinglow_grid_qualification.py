"""Small adversarial fixtures; no build, GPU execution, or timing jobs."""
import copy
import gzip
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock

from hosted_timing import matchinglow_grid_qualification as q
from hosted_timing import targeted


class MatchingLowQualificationTests(unittest.TestCase):
    def plan(self):
        return json.loads((Path(__file__).parents[1]/'benchmarks/plans/hosted-matchinglow-only-grid-20261116.json').read_text())

    def samples(self):
        def row(mean, lower, upper):
            return dict(mean_ns=mean, lower_ns=lower, upper_ns=upper)
        return {0:dict(Ooples=row(140, 130, 150)),
                1:dict(Ooples=row(80, 78, 82)),
                2:dict(Ooples=row(81, 79, 83), Competitor=row(100, 95, 105)),
                3:dict(Ooples=row(135, 125, 145))}

    def test_draft_scope_is_exact_but_cannot_prepare_or_measure(self):
        plan = self.plan()
        plan['inputs_ready'] = False
        q.validate_scope(plan, require_ready=False)
        with self.assertRaisesRegex(AssertionError, 'not frozen'):
            q.validate_scope(plan)

    def test_all_three_pairs_both_profiles_and_both_native_controls_make_ninety(self):
        plan = self.plan(); scope = targeted.SCOPES[q.SCOPE]
        self.assertEqual((90, 18), scope['totals'])
        cases = decisions = 0
        seen = {'batch': set(), 'builder': set()}
        for task in plan['targeted_shards'][0]['tasks']:
            owned = len(set(task['methods']) & {'Ooples', 'BuilderFull', 'BuilderLatestOnly'})
            count = len(task['operations'])
            self.assertEqual(task['cases'], count*2*(3*owned+len(task['methods'])))
            self.assertEqual(task['decisions'], count*2*owned)
            cases += task['cases']; decisions += task['decisions']
            seen['batch' if task['fixture'] == 'LibraryPairBenchmarks' else 'builder'].update(
                x['pair'] for x in task['operations'])
        self.assertEqual((90, 18), (cases, decisions))
        self.assertEqual(set(q.PAIRS), seen['batch']); self.assertEqual(seen['batch'], seen['builder'])

    def test_other_source_native_policy_or_runner_count_is_not_this_scope(self):
        mutations = [lambda x:x['sources'].update(successor=q.OLD),
                     lambda x:x['matrix'].update(max_parallel=2),
                     lambda x:x['measurement_policy'].update(iterations=9),
                     lambda x:x['targeted_shards'][0]['tasks'][1]['methods'].remove('NativeParallel')]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                plan = self.plan(); mutate(plan)
                with self.assertRaises(AssertionError): q.validate_scope(plan, require_ready=False)

    def test_accepted_original_decision_and_source_allowlist_remain_bounded(self):
        plan = self.plan()
        self.assertEqual(q.QUALIFIED, plan['targeted_reference']['qualified_source'])
        self.assertEqual('43673961720', plan['targeted_reference']['run_id'])
        self.assertEqual(q.PATHS, targeted.SCOPES[q.SCOPE]['paths'])
        proof = plan['baseline_continuation']
        self.assertEqual(12, len(proof['harness_changes']))
        self.assertTrue(all(n.startswith(('benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/',
            'benchmarks/OoplesFinance.StockIndicators.CompetitorTests/')) for n in proof['harness_changes']))
        self.assertTrue(any(x['before'] is None for x in proof['harness_changes'].values()))
        self.assertEqual(q.UNIT, plan['candidate']['unit_classes'])
        self.assertEqual(q.PARITY, plan['candidate']['parity_classes'])
        self.assertEqual(3, len(q.UNIT)); self.assertEqual(2, len(q.PARITY))
        self.assertTrue(all(x.startswith('OoplesFinance.StockIndicators.Tests.Unit.ModelsTests.') for x in q.UNIT))
        self.assertTrue(all(x.startswith('OoplesFinance.StockIndicators.CompetitorTests.') for x in q.PARITY))

    def test_both_candidate_rounds_newly_cross_native_with_margin(self):
        self.assertTrue(q.native_crossover(self.samples()))

    def test_one_overlapping_or_slow_candidate_round_rejects(self):
        for index in (1, 2):
            with self.subTest(index=index):
                rounds = self.samples(); rounds[index]['Ooples']['upper_ns'] = 96
                self.assertFalse(q.native_crossover(rounds))
                rounds = self.samples(); rounds[index]['Ooples']['mean_ns'] = 91
                self.assertFalse(q.native_crossover(rounds))

    def test_existing_native_point_win_is_not_new_crossover(self):
        for index in (0, 3):
            with self.subTest(index=index):
                rounds = self.samples(); rounds[index]['Ooples']['mean_ns'] = 99
                self.assertFalse(q.native_crossover(rounds))

    def test_missing_round_and_wrong_native_gate_cannot_qualify(self):
        rounds = self.samples(); del rounds[1]
        with self.assertRaises(AssertionError): q.native_crossover(rounds)
        plan = self.plan(); plan['native_crossover_gate']['minimum_margin'] = 0
        with self.assertRaises(AssertionError): q.validate_scope(plan, require_ready=False)

    def test_runtime_pin_rejects_other_patch_and_accepts_only_exact_decorated_version(self):
        for actual in ('.NET 10.0.15', '.NET 10.0.15 (10.0.15-servicing)'):
            q.verify_runtime_version(actual, '.NET 10.0.15')
        for actual in ('.NET 10.0.14', '.NET 10.0.16', '.NET 10.0.150', '.NET 9.0.15'):
            with self.subTest(actual=actual), self.assertRaises(AssertionError):
                q.verify_runtime_version(actual, '.NET 10.0.15')

    def test_fully_qualified_cpu_classes_are_supported_only_for_this_scope(self):
        from hosted_timing import pipeline as p
        plan = self.plan()
        self.assertEqual(q.UNIT, p.cpu_test_classes(plan, 'successor'))
        for label in ('old', 'variants'):
            self.assertEqual(['OwnedHistoryBatchTests'], p.cpu_test_classes(plan, label))
        legacy = copy.deepcopy(plan); legacy.pop('matchinglow_grid_qualification')
        with self.assertRaises(AssertionError): p.cpu_test_classes(legacy, 'successor')
        for malformed in ('Namespace..Class', '.Namespace.Class', 'Namespace.Class.', '',
                          'Namespace.Class|Other', 'Namespace.1Class'):
            with self.subTest(malformed=malformed):
                bad = copy.deepcopy(plan); bad['cpu_test_classes']['successor'] = [malformed]
                with self.assertRaises(AssertionError): p.cpu_test_classes(bad, 'successor')
        altered = copy.deepcopy(plan); altered['cpu_test_classes']['successor'] = ['Wrong.Namespace.ValidClass']
        with self.assertRaises(AssertionError): q.validate_scope(altered, require_ready=False)

    def test_runtime_pin_checks_nonlibrary_original_operation_reports(self):
        from hosted_timing import verify
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); shard = root/'shard0'; shard.mkdir()
            (shard/'provenance.json').write_text(json.dumps(dict(raw_root='raw')))
            (shard/'status.json').write_text(json.dumps(dict(rounds=[dict(
                fixture='SharedMatchedLinesBenchmarks', report='merged.json',
                operation_checkpoints=[dict(report='original.json')]) ])))
            for name, runtime in [('merged.json', '.NET 10.0.15'), ('original.json', '.NET 10.0.16')]:
                (shard/name).write_text(json.dumps(dict(HostEnvironmentInfo=dict(RuntimeVersion=runtime))))
            with mock.patch.object(verify, 'archived', side_effect=lambda base, _raw, path:base/path):
                with self.assertRaises(AssertionError): q._verify_runtime(root, '.NET 10.0.15')

    def test_generic_collector_dispatches_to_native_gate_and_direct_entry_rejects(self):
        from hosted_timing import verify
        with mock.patch.object(verify, 'read', return_value={'matchinglow_grid_qualification': True}):
            with mock.patch.object(q, 'collect', return_value={'accepted': False}) as gated:
                self.assertEqual({'accepted': False}, verify.collect(Path('raw'), 'targeted'))
                gated.assert_called_once_with(Path('raw'))
            with self.assertRaisesRegex(AssertionError, 'native crossover gate'):
                verify._collect(Path('raw'), 'targeted', preflight=False)

    def test_archive_retains_authentication_and_preflight_but_not_runtime_blobs(self):
        from hosted_timing import archive as a
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp); repo = root/'repo'; prepared = root/'prepared'; raw = root/'raw'
            def write(path, content):
                path.parent.mkdir(parents=True, exist_ok=True); path.write_bytes(content)
            def document(path, value):
                write(path, (json.dumps(value)+'\n').encode())
            write(raw/'shard0/raw.txt', b'four rounds')
            document(prepared/'provenance.json', dict(run_id='test-run', run_attempt='1', workflow_head='tooling'))
            write(prepared/'evidence/gpu.json', b'actual GPU proof')
            retained = {}
            for relative in ('baseline-authentication/provenance.json', 'baseline-authentication/evidence/receipt.json',
                             'candidate-authentication/manifest.json', 'candidate-authentication/evidence/unit.trx',
                             'consumer-preflight/baseline-six-checks.json', 'consumer-preflight/jit.json'):
                path = prepared/relative; write(path, relative.encode()); retained['prepared/'+relative] = path
            write(prepared/'candidate-authentication/blobs/runtime.dll', b'never commit runtime')
            document(repo/'old-approval.json', dict(approved=True))
            document(repo/'approval.json', dict(approved=True))
            document(repo/'base-plan.json', dict(refresh_approval=dict(path='old-approval.json', sha256=a.digest(repo/'old-approval.json'))))
            for name in ('run-matchinglow-grid-qualification.py', 'test_hosted_matchinglow_grid_qualification.py'):
                write(repo/'scripts'/name, b'reviewed root helper')
            plan = dict(matchinglow_grid_qualification=True,
                scope_approval=dict(path='approval.json', sha256=a.digest(repo/'approval.json')),
                preflight=dict(files={p.name:a.digest(p) for p in (prepared/'consumer-preflight').iterdir()}))
            document(repo/'plan.json', plan)
            proof = json.loads((prepared/'provenance.json').read_text())
            summary = dict(input_provenance_sha256=a.digest(prepared/'provenance.json'),
                sources=dict(old=q.OLD, new=q.NEW), accepted=False, case_count=90, decision_count=18,
                verdicts={'inconclusive':18}, native_crossover_gate_passed=False, native_crossover_samples={})
            with mock.patch.object(a, 'PLAN', repo/'plan.json'), mock.patch.object(a, 'REPO', repo), \
                    mock.patch.object(q, 'BASE_PLAN', 'base-plan.json'), \
                    mock.patch.object(q, 'BASE_PLAN_SHA', a.digest(repo/'base-plan.json')), \
                    mock.patch.object(a, 'verify_inputs', return_value=proof), \
                    mock.patch.object(a, 'collect', return_value=summary), \
                    mock.patch.object(a.subprocess, 'check_output', side_effect=[b'harness continuation', b'candidate source']):
                destination = a.archive(raw, prepared, repo, 'targeted')
            decision = json.loads((destination/'decision.json').read_text())
            self.assertFalse(decision['accepted'] or decision['native_crossover_gate_passed'])
            self.assertNotIn('WMA/TRIMA', decision['note'])
            self.assertFalse(any('/blobs/' in name or name.endswith('.dll') for name in decision['archives']))
            for name, path in retained.items():
                self.assertEqual(path.read_bytes(), gzip.decompress((destination/(name+'.gz')).read_bytes()))
                self.assertEqual(a.digest(path), decision['archives'][name]['sha256'])
            self.assertIn('original-plan.json', decision['archives'])
            self.assertIn('scope-approval.json', decision['archives'])
            self.assertIn('original-scope-approval.json', decision['archives'])
            self.assertIn('harness-continuation.patch', decision['archives'])


if __name__ == '__main__':
    unittest.main()
