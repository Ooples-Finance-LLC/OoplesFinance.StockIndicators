import importlib.util
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("contracts", Path(__file__).with_name("run-competitor-contracts.py"))
contracts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(contracts)


class ContractShardsTests(unittest.TestCase):
    def test_discovery_deduplicates_theories_and_excludes_trajectory_only(self):
        prefix = contracts.PREFIX
        methods = contracts.discover("\n".join([
            "VSTest version 1", "    " + prefix + "A.Contract(x: 1)",
            "    " + prefix + "A.Contract(x: 2)",
            "    " + prefix + "B.FullTrajectoryMatchesIndependentFormula(id: 1)",
            "    " + prefix + "B.Other",
        ]))
        self.assertEqual([prefix + "A.Contract", prefix + "B.Other"], methods)
        with self.assertRaises(ValueError):
            contracts.discover("No tests found")

    def test_partition_is_disjoint_complete_and_rejects_invalid_inventory(self):
        methods = [f"Method{i:03}" for i in range(123)]
        shards = [contracts.assignment(methods, i, 20) for i in range(20)]
        self.assertEqual(methods, sorted(name for shard in shards for name in shard))
        for bad, shard, total in [(methods, 20, 20), (methods, 0, 0), (methods * 2, 0, 20), ([], 0, 20)]:
            with self.assertRaises(ValueError):
                contracts.assignment(bad, shard, total)

    def test_evidence_rejects_missing_extra_skipped_and_failed_methods(self):
        xml = '''<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
          <TestDefinitions><UnitTest id="1"><TestMethod className="A" name="B"/></UnitTest></TestDefinitions>
          <Results><UnitTestResult testId="1" testName="A.B(x: 1)" outcome="Passed"/>
          <UnitTestResult testId="1" testName="A.B(x: 2)" outcome="Passed"/></Results></TestRun>'''
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "results.trx"
            path.write_text(xml)
            self.assertEqual(2, contracts.verify_trx(path, ["A.B"]))
            for expected in [[], ["A.B", "A.C"], ["A.C"]]:
                with self.assertRaises(ValueError):
                    contracts.verify_trx(path, expected)
            for outcome in ["Failed", "NotExecuted"]:
                path.write_text(xml.replace('outcome="Passed"', f'outcome="{outcome}"', 1))
                with self.assertRaises(ValueError):
                    contracts.verify_trx(path, ["A.B"])


if __name__ == "__main__":
    unittest.main()
