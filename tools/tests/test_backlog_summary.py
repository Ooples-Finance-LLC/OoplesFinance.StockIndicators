import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location('backlog_summary', Path(__file__).resolve().parents[1] / 'summarize_numerical_backlog.py')
summary = importlib.util.module_from_spec(spec)
spec.loader.exec_module(summary)

class BacklogSummaryTests(unittest.TestCase):
    def call(self, rows):
        header = dict(kind='scan', purpose='diagnostic-only', mode='all', assemblySha256='assembly', scannerSha256='scanner',
                      selection=['A/default', 'A/short', 'B/default'])
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'run.jsonl'
            path.write_text('\n'.join(json.dumps(row) for row in [header] + rows))
            return summary.summarize(path)

    def rows(self):
        return [dict(kind='result', name=name, passed=ok, numericalFixturesPassed=ok,
                     failures=[] if ok else [dict(Rule='Finite', Fixture='numerical/large')])
                for name, ok in [('A/default', True), ('A/short', False), ('B/default', True)]]

    def test_partial_family_is_not_promoted_as_complete(self):
        report, candidates, complete = self.call(self.rows())
        self.assertEqual(['A/default', 'B/default'], candidates)
        self.assertEqual(['B/default'], complete)
        self.assertEqual(1, report['failureSignatures'][0]['configurations'])

    def test_incomplete_duplicate_and_unknown_results_fail_closed(self):
        for rows in [self.rows()[:-1], self.rows() + self.rows()[:1],
                     self.rows()[:-1] + [dict(self.rows()[-1], name='Unknown/default')]]:
            with self.subTest(rows=rows), self.assertRaises(ValueError):
                self.call(rows)

    def test_passing_without_numerical_evidence_is_rejected(self):
        rows = self.rows()
        rows[0]['numericalFixturesPassed'] = False
        with self.assertRaises(ValueError):
            self.call(rows)

if __name__ == '__main__':
    unittest.main()
