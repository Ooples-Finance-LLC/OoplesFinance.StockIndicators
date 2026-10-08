import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("performance", Path(__file__).with_name("verify-competitor-performance.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class PerformanceEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.results = [{"Method": arm, "FullName": f'LibraryPairBenchmarks.{arm}(Bars: {bars}, PairId: "A")', "Statistics": {"N": 3, "Mean": 100}}
                        for bars in (1000, 10000) for arm in ("Ooples", "Competitor")]

    def check(self, results):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "manifest.json").write_text(json.dumps([{"Id": "A", "Status": "paired"}]), encoding="utf-8")
            (root / "test-report-full.json").write_text(json.dumps({"Benchmarks": results}), encoding="utf-8")
            module.verify(root / "manifest.json", root, 0, 1)

    def test_all_measured_arms(self): self.check(self.results)

    def test_missing_arm(self):
        with self.assertRaises(ValueError): self.check(self.results[:-1])

    def test_duplicate_arm(self):
        with self.assertRaises(ValueError): self.check(self.results + [copy.deepcopy(self.results[0])])

    def test_failed_setup(self):
        self.results[0]["Statistics"] = None
        with self.assertRaises(ValueError): self.check(self.results)

    def test_dry_run_is_not_timing_evidence(self):
        self.results[0]["Statistics"]["N"] = 1
        with self.assertRaises(ValueError): self.check(self.results)

    def test_nonfinite_mean(self):
        self.results[0]["Statistics"]["Mean"] = float("nan")
        with self.assertRaises(ValueError): self.check(self.results)

    def test_unknown_pair(self):
        self.results[0]["FullName"] = 'LibraryPairBenchmarks.Ooples(Bars: 1000, PairId: "pending")'
        with self.assertRaises(ValueError): self.check(self.results)

if __name__ == "__main__": unittest.main()
