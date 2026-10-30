import copy
import gzip
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

from hosted_performance_results import load_results

spec = importlib.util.spec_from_file_location('hosted_fixture_report',
    Path(__file__).resolve().parents[1] / 'scripts/report-competitor-performance.py')
reporter = importlib.util.module_from_spec(spec)
spec.loader.exec_module(reporter)


class HostedResultsTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = 'a' * 40
        self.run = 123
        self.manifest = json.dumps([{'Id': 'Skender.Sma', 'Status': 'paired',
                                    'OoplesIndicator': 'Sma'}]).encode('utf-8')
        manifest_path = self.root / 'manifest.json'
        manifest_path.write_bytes(self.manifest)
        artifacts = self.root / 'artifacts'
        path = artifacts / 'competitor-performance-0/results/LibraryPairBenchmarks-report-full.json'
        path.parent.mkdir(parents=True)
        rows = [{'Method': arm,
                 'FullName': f'LibraryPairBenchmarks.{arm}(Bars: {bars}, PairId: "Skender.Sma")',
                 'Statistics': {'N': 3, 'Mean': mean, 'StandardDeviation': 1,
                                'OriginalValues': [mean-1, mean, mean+1]},
                 'Memory': {'BytesAllocatedPerOperation': 32}}
                for bars in [1000, 10000] for arm, mean in [('Ooples', 100), ('Competitor', 200)]]
        path.write_text(json.dumps({'Benchmarks': rows, 'HostEnvironmentInfo': {'RuntimeVersion': '.NET 10'}}), encoding='utf-8')
        self.folder = self.root / 'summary'
        reporter.report(manifest_path, artifacts, 1, self.folder, self.source,
            f'https://github.com/Ooples-Finance-LLC/OoplesFinance.StockIndicators/actions/runs/{self.run}')

    def load(self):
        return load_results(self.folder, self.source, self.run, self.manifest)

    def modify_evidence(self, change):
        path = self.folder / 'evidence.json'
        evidence = json.loads(path.read_text(encoding='utf-8'))
        change(evidence)
        path.write_text(json.dumps(evidence), encoding='utf-8')

    def test_complete_raw_round_trip(self):
        result = self.load()
        self.assertEqual(1, result['paired_apis'])
        self.assertEqual([1000, 10000], [r['bars'] for r in result['rows']])
        self.assertTrue(all(r['competitor_over_ooples'] == 2 for r in result['rows']))

    def test_wrong_source_or_run_rejected(self):
        for sha, run in [('b' * 40, self.run), (self.source, 124)]:
            with self.assertRaisesRegex(ValueError, 'source/run mismatch'):
                load_results(self.folder, sha, run, self.manifest)

    def test_wrong_pinned_inventory_rejected(self):
        with self.assertRaisesRegex(ValueError, 'pinned source'):
            load_results(self.folder, self.source, self.run, self.manifest + b' ')

    def test_csv_tampering_rejected(self):
        path = self.folder / 'comparisons.csv'
        path.write_bytes(path.read_bytes().replace(b'100,', b'1,'))
        with self.assertRaisesRegex(ValueError, 'comparison values'):
            self.load()

    def test_raw_tampering_rejected(self):
        path = next((self.folder / 'raw').glob('*.gz'))
        path.write_bytes(gzip.compress(b'{}'))
        with self.assertRaisesRegex(ValueError, 'hash mismatch'):
            self.load()

    def test_missing_arm_rejected_even_with_updated_hash(self):
        path = next((self.folder / 'raw').glob('*.gz'))
        report = json.loads(gzip.decompress(path.read_bytes()))
        report['Benchmarks'].pop()
        raw = json.dumps(report).encode('utf-8')
        path.write_bytes(gzip.compress(raw))
        self.modify_evidence(lambda e: e['reports'][0].update(sha256=hashlib.sha256(raw).hexdigest()))
        with self.assertRaisesRegex(ValueError, 'Missing performance arms'):
            self.load()

    def test_duplicate_raw_report_rejected(self):
        self.modify_evidence(lambda e: e['reports'].append(copy.deepcopy(e['reports'][0])))
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            self.load()

    def test_artifact_traversal_rejected(self):
        self.modify_evidence(lambda e: e['reports'][0].update(artifactPath='../escape-report-full.json'))
        with self.assertRaisesRegex(ValueError, 'artifact path'):
            self.load()

    def test_altered_counts_or_hosts_rejected(self):
        self.modify_evidence(lambda e: e.update(pairedIndicators=999))
        with self.assertRaisesRegex(ValueError, 'provenance differs'):
            self.load()


if __name__ == '__main__':
    unittest.main()
