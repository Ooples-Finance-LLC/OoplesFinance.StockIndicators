import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("cpu_performance", Path(__file__).with_name("verify-cpu-kernel-performance.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class CpuPerformanceEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.methods = ["OoplesOwnedBatch", "CompetitorOwnedBatch", "OoplesReusableBatch", "CompetitorReusableBatch"]
        self.rows = [{"Type": "CpuKernelBenchmarks", "Method": method,
                      "Parameters": f"Bars={bars}&PairId=Pilot",
                      "FullName": f'CpuKernelBenchmarks.{method}(Bars: {bars}, PairId: "Pilot")',
                      "Statistics": {"Mean": 100, "N": 3},
                      "Memory": {"BytesAllocatedPerOperation": 0}}
                     for method in self.methods for bars in (1000, 10000)]

    def check(self, rows):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "report-full-compressed.json"
            path.write_text(json.dumps({"Benchmarks": rows}), encoding="utf-8")
            return module.verify(directory, "Pilot", self.methods)

    def test_complete(self):
        self.assertEqual(8, len(self.check(self.rows)))

    def test_empty_missing_duplicate(self):
        for rows in ([], self.rows[:-1], self.rows + [self.rows[0]]):
            with self.subTest(rows=len(rows)), self.assertRaises(ValueError):
                self.check(rows)

    def test_failed_dry_nonfinite_or_missing_allocation(self):
        for field, value in (("Statistics", None), ("Statistics", {"Mean": 100, "N": 1}),
                             ("Statistics", {"Mean": float("nan"), "N": 3}), ("Memory", None)):
            rows = copy.deepcopy(self.rows)
            rows[0][field] = value
            with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                self.check(rows)

    def test_capabilities_exclude_unmatched_workloads(self):
        self.assertEqual(2, len(module.supported_methods("Skender.GetFractal")))
        self.assertIn("CompetitorStreaming", module.supported_methods("QuanTAlib.Jma"))
        self.assertNotIn("CompetitorReusableBatch", module.supported_methods("QuanTAlib.Jma"))
        self.assertIn("CompetitorReusableBatch", module.supported_methods("TaLib.Functions.Asin"))
        self.assertNotIn("CompetitorStreaming", module.supported_methods("TaLib.Functions.Asin"))

    def test_legacy_adapter_results_are_rejected(self):
        self.rows[0]["Method"] = "Competitor"
        with self.assertRaises(ValueError):
            self.check(self.rows)

    def test_streaming_cannot_reuse_state_across_calibrated_invocations(self):
        self.methods = ["OoplesStreaming", "CompetitorStreaming"]
        self.rows = [{"Type": "CpuKernelBenchmarks", "Method": method,
                      "FullName": f'CpuKernelBenchmarks.{method}(Bars: {bars}, PairId: "Pilot")',
                      "Statistics": {"Mean": 100, "N": 3},
                      "Memory": {"BytesAllocatedPerOperation": 0},
                      "Measurements": [{"IterationMode": "Workload", "IterationStage": "Actual", "Operations": 64}]}
                     for method in self.methods for bars in (1000, 10000)]
        self.assertEqual(4, len(self.check(self.rows)))
        self.rows[0]["Measurements"][0]["Operations"] = 128
        with self.assertRaises(ValueError):
            self.check(self.rows)

    def test_unexpected_pair(self):
        self.rows[0]["FullName"] = 'CpuKernelBenchmarks.PublicApi(Bars: 1000, PairId: "Wrong")'
        with self.assertRaises(ValueError):
            self.check(self.rows)

    def test_builder_evidence_cannot_be_replaced_by_kernel_timings(self):
        rows = [{"Type": "CpuBuilderBenchmarks", "Method": method,
                 "FullName": f'CpuBuilderBenchmarks.{method}(Bars: {bars}, PairId: "Pilot")',
                 "Statistics": {"Mean": 100, "N": 3}, "Memory": {"BytesAllocatedPerOperation": 0}}
                for method in ("OoplesBuilderBatch", "CompetitorNativeBatch") for bars in (1000, 10000)]
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "builder-full-compressed.json"
            path.write_text(json.dumps({"Benchmarks": rows}), encoding="utf-8")
            self.assertEqual(4, len(module.verify(directory, "Pilot", suite="builder")))
            rows[0]["Type"] = "CpuKernelBenchmarks"
            path.write_text(json.dumps({"Benchmarks": rows}), encoding="utf-8")
            with self.assertRaises(ValueError):
                module.verify(directory, "Pilot", suite="builder")


if __name__ == "__main__":
    unittest.main()
