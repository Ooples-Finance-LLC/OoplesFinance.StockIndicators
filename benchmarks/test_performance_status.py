import hashlib
import json
from pathlib import Path
import tempfile
import unittest

from update_performance_status import formula_rows, fresh_results, ci_snapshot_html


class PerformanceStatusTests(unittest.TestCase):
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
