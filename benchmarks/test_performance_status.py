import hashlib
import json
from pathlib import Path
import tempfile
import subprocess
import unittest

from update_performance_status import formula_rows, fresh_results, ci_snapshot_html, measurement_totals, reconcile_visible_sections, published_inventory, local_validation_rows, hosted_measurement_html


class PerformanceStatusTests(unittest.TestCase):
    def test_hosted_run_success_does_not_imply_verified_timings(self):
        result = hosted_measurement_html(dict(source_sha='a' * 40, run_id=123,
            status='completed', conclusion='success', updated_utc='2026-10-21'))
        self.assertIn('/actions/runs/123', result)
        self.assertIn('not be added to their totals', result)
        self.assertIn('not a verified performance result', result)
        self.assertIn('401 paired APIs', result)

    def test_hosted_run_requires_pinned_source_and_run_identity(self):
        for receipt in [dict(source_sha='master', run_id=1),
                        dict(source_sha='a' * 40, run_id='javascript:bad'),
                        dict(source_sha='a' * 40, run_id=True)]:
            with self.assertRaises(ValueError):
                hosted_measurement_html(receipt)
        self.assertEqual('', hosted_measurement_html({}))

    def test_publication_rows_discover_new_batches_without_duplicates_or_state_inference(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            cases = [
                ('si-pr266-kst-compat-20261021', {'status': 'checks-passed-and-published'}),
                ('si-pr266-future-indicator-20261022', {'status': 'waiting-for-integration', 'step': 'dependencies'}),
                ('unrelated-project', {'status': 'failed'})]
            for name, receipt in cases:
                path = root / name / 'status.json'
                path.parent.mkdir()
                path.write_text(json.dumps(receipt), encoding='utf-8')
            rows = local_validation_rows(root)
            self.assertEqual(2, len(rows))
            self.assertIn(['KST and portable-helper publication', 'checks-passed-and-published',
                           'not recorded', 'unknown'], rows)
            self.assertIn(['future indicator', 'waiting-for-integration', 'dependencies', 'unknown'], rows)

    def test_published_inventory_ignores_newer_commits_and_dirty_inventory(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            def git(*args):
                return subprocess.check_output(['git', '-c', 'user.name=Report Test', '-c',
                    'user.email=report-test@example.invalid', *args], cwd=root, stderr=subprocess.PIPE).decode().strip()
            git('init')
            path = root / 'benchmarks/results/remaining-indicators/inventory.json'
            path.parent.mkdir(parents=True)
            path.write_text('{"counts":{"batch_formulas":1}}', encoding='utf-8')
            git('add', '.'); git('commit', '-m', 'published inventory')
            published = git('rev-parse', 'HEAD')
            path.write_text('{"counts":{"batch_formulas":2}}', encoding='utf-8')
            git('add', '.'); git('commit', '-m', 'unpublished inventory')
            path.write_text('{"counts":{"batch_formulas":3}}', encoding='utf-8')
            self.assertEqual(1, published_inventory(root, published)['counts']['batch_formulas'])
            self.assertEqual(2, published_inventory(root, git('rev-parse', 'HEAD'))['counts']['batch_formulas'])
            with self.assertRaisesRegex(ValueError, 'unavailable'):
                published_inventory(root, '0' * 40)

    def test_inventory_requires_exact_commit_instead_of_mutable_ref(self):
        for head in ['HEAD', 'master', 'abc123', '-p', '0' * 40 + ':other']:
            with self.assertRaisesRegex(ValueError, 'exact commit'):
                published_inventory(Path('.'), head)

    def test_all_current_surfaces_use_same_ledger_and_archive_is_preserved(self):
        source = ('<section class="hero"><p class="lede">stale</p></section>'
                  '<section id="current-competitors"><p>stale wins</p></section>'
                  '<table><tr><td>Fresh candidate old</td><td>999 wins</td></tr></table>'
                  '<section id="explorer">immutable archive</section>')
        totals = dict(expected=16, measured=3, faster=1, slower=1, inconclusive=1, pending=13)
        result = reconcile_visible_sections(source, totals, 'abc123', 1)
        self.assertNotIn('stale', result)
        self.assertNotIn('999 wins', result)
        self.assertIn('3 of 16 slots measured: 1 faster, 1 slower, 1 inconclusive, 13 pending', result)
        self.assertIn('3 / 16 matched slots measured; 13 pending', result)
        self.assertIn('<section id="explorer">immutable archive</section>', result)
        self.assertEqual(result, reconcile_visible_sections(result, totals, 'abc123', 1))
        changed = reconcile_visible_sections(result, dict(expected=16, measured=4, faster=2, slower=1, inconclusive=1, pending=12), 'new', 2)
        self.assertIn('<td>4 / 16</td>', changed)
        self.assertNotIn('<td>3 / 16</td>', changed)

    def test_measurement_totals_separate_pending_and_inconclusive(self):
        totals = measurement_totals([
            ['A', 'Full', 8, 3, 1, 1, 1, 5],
            ['A', 'Latest', 8, 0, 0, 0, 0, 8]])
        self.assertEqual(dict(expected=16, measured=3, faster=1, slower=1,
                              inconclusive=1, pending=13), totals)

    def test_inconsistent_or_duplicate_summary_is_rejected(self):
        row = ['A', 'Full', 8, 3, 1, 1, 1, 5]
        for rows in [[row, row], [['A', 'Full', 8, 4, 1, 1, 1, 4]],
                     [['A', 'Full', 8, 3, 1, 1, 1, 4]],
                     [['A', 'Full', 8, 0, 0, 0, 0, -1]]]:
            with self.assertRaises(ValueError):
                measurement_totals(rows)

    def test_hosted_ci_is_tied_to_exact_published_commit(self):
        receipt = {'head': 'current', 'checked_utc': 'today', 'checks': [
            {'name': 'net461', 'status': 'completed', 'conclusion': 'failure', 'url': 'evidence'},
            {'name': 'contracts', 'status': 'in_progress', 'conclusion': None}]}
        rendered = ci_snapshot_html(receipt, 'current')
        self.assertIn('matches the published commit', rendered)
        self.assertIn('1 failure, 1 in_progress', rendered)
        self.assertIn('net461', rendered)
        self.assertIn('different commit', ci_snapshot_html(receipt, 'new'))

    def test_missing_ci_is_unknown_and_ci_text_is_escaped(self):
        self.assertIn('No hosted CI snapshot', ci_snapshot_html({}, 'current'))
        rendered = ci_snapshot_html({'head': '<old>', 'checks': [
            {'name': '<script>', 'conclusion': 'failure', 'url': '<url>'}]}, 'current')
        self.assertNotIn('<script>', rendered)
        self.assertIn('&lt;script&gt;', rendered)

    def test_partial_alias_coverage_is_not_complete(self):
        rows = formula_rows({'counts': {'batch_formulas': 3}, 'batch_formulas': [
            {'indicator': 'A', 'options': ['A', 'Alias'], 'contract_receipts': {'A': ['a.json']}},
            {'indicator': 'B', 'options': ['B'], 'contract_receipts': {'B': ['b.json']}},
            {'indicator': 'C', 'options': []}]})
        self.assertEqual('some options have historical receipts', rows[0]['state'])
        self.assertEqual(['Alias'], rows[0]['missing'])
        self.assertEqual('all listed options have historical receipts', rows[1]['state'])
        self.assertEqual('no published contract receipt', rows[2]['state'])

    def test_no_measurements_is_empty_not_a_win(self):
        with tempfile.TemporaryDirectory() as folder:
            self.assertEqual([], fresh_results(Path(folder)))

    def test_changed_raw_evidence_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            raw = root / 'raw.json'
            raw.write_text('{}')
            (root / 'SharedExampleBenchmarks.json').write_text(json.dumps({
                'report': str(raw), 'report_sha256': 'invalid', 'comparisons': []}))
            with self.assertRaisesRegex(ValueError, 'hash mismatch'):
                fresh_results(root)

    def test_duplicate_comparisons_cannot_inflate_wins(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            raw = root / 'raw.json'
            raw.write_bytes(b'{}')
            row = {'operation': 'Library.Api', 'bars': 1000, 'mode': 'BuilderFull'}
            receipt = {'report': str(raw), 'report_sha256': hashlib.sha256(b'{}').hexdigest(),
                       'fixture': 'SharedExampleBenchmarks', 'snapshot': 'snapshot',
                       'completed_utc': '2026-10-21', 'comparisons': [row, row]}
            (root / 'SharedExampleBenchmarks.json').write_text(json.dumps(receipt))
            with self.assertRaisesRegex(ValueError, 'Ambiguous'):
                fresh_results(root)


if __name__ == '__main__':
    unittest.main()
