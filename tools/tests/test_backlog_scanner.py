"""CLI checks against an existing scanner build; never rebuild the indicator library."""
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

SCANNER = Path(__file__).resolve().parents[1] / 'NumericalBacklogScanner/bin/Release/net10.0/NumericalBacklogScanner.dll'

class BacklogScannerTests(unittest.TestCase):
    def setUp(self):
        self.assertTrue(SCANNER.exists(), 'Build NumericalBacklogScanner in Release first.')
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.backlog = self.root / 'backlog.txt'
        self.backlog.write_text('OoplesFinance.StockIndicators.Indicators.TrendImpulseFilter/default\n')
        self.evidence = self.root / 'evidence.jsonl'

    def run_scanner(self):
        return subprocess.run(['dotnet', str(SCANNER), str(self.backlog), str(self.evidence), 'all', '2'],
                              capture_output=True, text=True, timeout=60)

    def test_positive_evidence_covers_every_fixture_and_resume_does_not_repeat(self):
        run = self.run_scanner()
        self.assertEqual(0, run.returncode, run.stdout + run.stderr)
        original = self.evidence.read_bytes()
        header, result = [json.loads(line) for line in original.splitlines()]
        self.assertTrue(result['passed'])
        seen = {f['Name'] for f in result['fixtures'] if f['Completed'] and f['Passed'] and f['InputBars'] == result['fixtureBars']}
        self.assertTrue(set(header['fixtureNames']) <= seen)
        self.assertTrue(any(name.startswith('backlog-scan-calendar/') for name in seen))
        resumed = self.run_scanner()
        self.assertEqual(0, resumed.returncode, resumed.stderr)
        self.assertIn('1 resumed', resumed.stdout)
        self.assertEqual(original, self.evidence.read_bytes())

    def test_numerical_fixtures_extend_past_long_configuration_warmup(self):
        self.backlog.write_text('OoplesFinance.StockIndicators.Indicators.TFSMboIndicator/longer-periods\n')
        run = self.run_scanner()
        self.assertEqual(0, run.returncode, run.stdout + run.stderr)
        header, result = [json.loads(line) for line in self.evidence.read_text().splitlines()]
        self.assertGreater(result['fixtureBars'], header['minimumBars'])
        for fixture in result['fixtures']:
            if fixture['Name'] in header['fixtureNames']:
                self.assertEqual(result['fixtureBars'], fixture['InputBars'])
                self.assertTrue(fixture['Completed'] and fixture['Passed'])

    def test_stale_identity_is_rejected_without_modifying_evidence(self):
        self.assertEqual(0, self.run_scanner().returncode)
        rows = self.evidence.read_text().splitlines()
        header = json.loads(rows[0])
        header['assemblySha256'] = 'stale'
        rows[0] = json.dumps(header, separators=(',', ':'))
        self.evidence.write_text('\n'.join(rows) + '\n')
        original = self.evidence.read_bytes()
        run = self.run_scanner()
        self.assertNotEqual(0, run.returncode)
        self.assertIn('different scanner, assembly', run.stderr)
        self.assertEqual(original, self.evidence.read_bytes())

    def test_duplicate_results_cannot_satisfy_resume(self):
        self.assertEqual(0, self.run_scanner().returncode)
        rows = self.evidence.read_text().splitlines()
        self.evidence.write_text('\n'.join(rows + [rows[1]]) + '\n')
        run = self.run_scanner()
        self.assertNotEqual(0, run.returncode)
        self.assertIn('duplicate or unselected', run.stderr)

    def test_unknown_configuration_is_not_silently_skipped(self):
        self.backlog.write_text('UnknownIndicator/default\n')
        run = self.run_scanner()
        self.assertNotEqual(0, run.returncode)
        self.assertIn('Unknown configurations', run.stderr)
        self.assertFalse(self.evidence.exists())

if __name__ == '__main__':
    unittest.main()
