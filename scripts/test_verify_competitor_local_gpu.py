import copy
import hashlib
import importlib.util
from pathlib import Path
import subprocess
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("gpu", Path(__file__).with_name("verify-competitor-local-gpu.py"))
gpu = importlib.util.module_from_spec(spec)
spec.loader.exec_module(gpu)


class LocalGpuEvidenceTests(unittest.TestCase):
    def test_partition_is_complete_and_rejects_missing_or_duplicated_gpu_methods(self):
        self.assertEqual(["A.Cpu", "C.Cpu"], gpu.partition(["A.Cpu", "B.Gpu", "C.Cpu"], ["B.Gpu"]))
        for methods in [[], ["B.Gpu", "B.Gpu"], ["Missing.Gpu"], ["A.Cpu", "B.Gpu", "C.Cpu"]]:
            with self.assertRaises(ValueError):
                gpu.partition(["A.Cpu", "B.Gpu", "C.Cpu"], methods)

    def test_receipt_requires_current_sources_and_all_passing_trx_cases(self):
        xml = b'''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions><UnitTest id="1"><TestMethod className="A" name="Gpu"/></UnitTest></TestDefinitions>
          <Results><UnitTestResult testId="1" testName="A.Gpu(1)" outcome="Passed"/>
          <UnitTestResult testId="1" testName="A.Gpu(2)" outcome="Passed"/></Results></TestRun>'''
        receipt = {"source_sha256": "current", "methods": ["A.Gpu"], "passed_cases": 2,
                   "trx_sha256": hashlib.sha256(xml).hexdigest()}
        self.assertEqual(2, gpu.verify_evidence(receipt, xml, ["A.Gpu"], "current"))
        for key, value in [("source_sha256", "stale"), ("methods", []), ("passed_cases", 1), ("trx_sha256", "wrong")]:
            changed = copy.deepcopy(receipt)
            changed[key] = value
            with self.assertRaises(ValueError):
                gpu.verify_evidence(changed, xml, ["A.Gpu"], "current")
        for outcome in [b"Failed", b"NotExecuted"]:
            changed_xml = xml.replace(b'outcome="Passed"', b'outcome="' + outcome + b'"', 1)
            changed = dict(receipt, trx_sha256=hashlib.sha256(changed_xml).hexdigest())
            with self.assertRaises(ValueError):
                gpu.verify_evidence(changed, changed_xml, ["A.Gpu"], "current")
        with self.assertRaises(ValueError):
            gpu.verify_evidence(dict(receipt, methods=["A.Gpu", "B.Gpu"]), xml, ["A.Gpu", "B.Gpu"], "current")

    def test_fingerprint_tracks_new_sources_and_resources_but_normalizes_line_endings(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            subprocess.run(["git", "init", "-q", str(root)], check=True)
            (root / "src").mkdir()
            source = root / "src/Indicator.cs"
            source.write_bytes(b"first\nsecond\n")
            before = gpu.source_fingerprint(root)
            source.write_bytes(b"first\r\nsecond\r\n")
            self.assertEqual(before, gpu.source_fingerprint(root))
            source.write_bytes(b"changed\n")
            changed = gpu.source_fingerprint(root)
            self.assertNotEqual(before, changed)
            (root / "src/reference.txt").write_text("new embedded reference")
            self.assertNotEqual(changed, gpu.source_fingerprint(root))


if __name__ == "__main__":
    unittest.main()
