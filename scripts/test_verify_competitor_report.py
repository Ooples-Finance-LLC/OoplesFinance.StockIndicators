import csv
import gzip
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("report", Path(__file__).with_name("report-competitor-performance.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class PerformanceReportTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.manifest = self.root / "manifest.json"
        self.manifest.write_text(json.dumps([
            {"Id": "A.One", "Status": "paired", "OoplesIndicator": "First"},
            {"Id": "B.Two", "Status": "paired", "OoplesIndicator": "Second"},
            {"Id": "B.Stub", "Status": "unavailable", "OoplesIndicator": "Alternate", "CompetitorLimitation": "Empty stub"},
        ]))
        self.artifacts = self.root / "artifacts"
        for shard, pair in enumerate(("A.One", "B.Two")):
            rows = []
            for bars in (1000, 10000):
                for arm, mean in (("Ooples", 100), ("Competitor", 200 if shard == 0 else 50)):
                    rows.append({"Method": arm, "FullName": f'LibraryPairBenchmarks.{arm}(Bars: {bars}, PairId: "{pair}")',
                                 "Statistics": {"N": 3, "Mean": mean, "StandardDeviation": 0, "OriginalValues": [mean] * 3},
                                 "Memory": {"BytesAllocatedPerOperation": 0}})
            folder = self.artifacts / f"competitor-performance-{shard}"
            folder.mkdir(parents=True)
            (folder / "test-report-full.json").write_text(json.dumps({"Benchmarks": rows, "HostEnvironmentInfo": {"OsVersion": "test"}}))

    def run_report(self):
        return module.report(self.manifest, self.artifacts, 2, self.root / "output", "abc", "https://example.com/run")

    def test_complete_report_preserves_both_directions_and_raw_evidence(self):
        evidence = self.run_report()
        with (self.root / "output/comparisons.csv").open() as stream:
            rows = list(csv.DictReader(stream))
        self.assertEqual([2, 2, .5, .5], [float(row["competitor_over_ooples"]) for row in rows])
        self.assertEqual(8, evidence["measuredArms"])
        self.assertIn("Empty stub", (self.root / "output/README.md").read_text(encoding="utf-8"))
        for raw in evidence["reports"]:
            self.assertEqual((self.artifacts / raw["artifactPath"]).read_bytes(), gzip.decompress((self.root / "output" / raw["file"]).read_bytes()))

    def test_missing_shard_cannot_publish_partial_report(self):
        (self.artifacts / "competitor-performance-1").rename(self.artifacts / "missing")
        with self.assertRaises(ValueError): self.run_report()
        self.assertFalse((self.root / "output").exists())

    def test_wrong_shard_assignment_is_rejected(self):
        a, b = [self.artifacts / f"competitor-performance-{i}/test-report-full.json" for i in range(2)]
        first, second = a.read_bytes(), b.read_bytes()
        a.write_bytes(second)
        b.write_bytes(first)
        with self.assertRaises(ValueError): self.run_report()

    def test_missing_samples_or_allocations_are_rejected(self):
        for field in ("OriginalValues", "Memory"):
            path = self.artifacts / "competitor-performance-0/test-report-full.json"
            original = path.read_text()
            data = json.loads(original)
            row = data["Benchmarks"][0]
            if field == "Memory": del row[field]
            else: del row["Statistics"][field]
            path.write_text(json.dumps(data))
            with self.assertRaises(ValueError): self.run_report()
            path.write_text(original)


if __name__ == "__main__": unittest.main()
