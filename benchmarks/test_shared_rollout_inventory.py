import gzip
import json
import tempfile
import unittest
from pathlib import Path

from build_shared_rollout_inventory import inventory


class InventoryDiagnosticsTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.manifest = json.loads(Path(__file__).with_name('competitor-library-manifest.json').read_text())
        self.rows = [dict(FullName=f'Historical(PairId: "{p["Id"]}", Bars: {count})',
                          Method=method, Statistics={'Mean': 1}, Memory={'BytesAllocatedPerOperation': 64})
                     for p in self.manifest if p['Status'] == 'paired'
                     for count in (1000, 10000) for method in ('Ooples', 'Competitor')]

    def write(self):
        (self.root / 'competitor-library-manifest.json').write_text(json.dumps(self.manifest))
        folder = self.root / 'results' / '2026-10-06-library-comparisons'
        folder.mkdir(parents=True, exist_ok=True)
        with gzip.open(folder / 'fixture.json.gz', 'wt') as stream:
            json.dump({'Benchmarks': self.rows}, stream)

    def qualification(self, name):
        path = self.root / 'qualification.json'
        rows = [dict(FullName=name, Method=method,
                     Statistics={'Mean': 1, 'ConfidenceInterval': {'Lower': .9, 'Upper': 1.1}},
                     Memory={'BytesAllocatedPerOperation': 64})
                for method in ('BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel')]
        path.write_text(json.dumps({'Benchmarks': rows}))
        return path

    def test_missing_bars_names_file_and_benchmark(self):
        self.rows[0]['FullName'] = self.rows[0]['FullName'].replace('Bars: 1000', 'MissingCount')
        self.write()
        with self.assertRaisesRegex(ValueError, 'Missing Bars: fixture.json.gz: Historical'):
            inventory(self.root, [])

    def test_missing_measurement_names_pair_count_and_method(self):
        missing = self.rows.pop(0)
        self.write()
        with self.assertRaises(ValueError) as raised:
            inventory(self.root, [])
        self.assertIn(self.manifest[0]['Id'], str(raised.exception))
        self.assertIn('bars=1000', str(raised.exception))
        self.assertIn('method=' + missing['Method'], str(raised.exception))

    def test_missing_pilot_names_id(self):
        self.manifest = [p for p in self.manifest if p['Id'] != 'QuanTAlib.Jma']
        self.rows = [r for r in self.rows if '"QuanTAlib.Jma"' not in r['FullName']]
        self.write()
        with self.assertRaisesRegex(ValueError, 'Pilot ID missing.*QuanTAlib.Jma'):
            inventory(self.root, [])

    def test_unknown_qualification_names_workload_and_file(self):
        self.write()
        path = self.qualification('SharedPointwiseBenchmarks.BuilderFull(Operation: "Missing", Count: 10000)')
        with self.assertRaises(ValueError) as raised:
            inventory(self.root, [path])
        self.assertIn('TaLib.Functions.Missing/10000', str(raised.exception))
        self.assertIn(str(path), str(raised.exception))

    def test_supported_qualification_formats_keep_all_four_methods(self):
        self.write()
        for name, pair in [('SharedPointwiseBenchmarks.BuilderFull(Operation: "Sin", Count: 10000)', 'Sin'),
                           ('SharedDispersionBenchmarks.BuilderFull(Variance: False, Count: 10000)', 'StdDev'),
                           ('SharedDispersionBenchmarks.BuilderFull(Variance: True, Count: 10000)', 'Var'),
                           ('SharedRollingSumBenchmarks.BuilderFull(Count: 10000)', 'Sum')]:
            with self.subTest(pair=pair):
                result = inventory(self.root, [self.qualification(name)])
                row = next(r for r in result['pairs'] if r['pair_id'] == 'TaLib.Functions.' + pair)
                self.assertEqual(4, len(row['current'][0]['methods']))
                self.assertFalse(row['current'][0]['full_clear_win'])


if __name__ == '__main__':
    unittest.main()
