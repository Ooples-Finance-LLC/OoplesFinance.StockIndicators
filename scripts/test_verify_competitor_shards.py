import copy
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("shards", Path(__file__).with_name("verify-competitor-shards.py"))
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)

class ShardEvidenceTests(unittest.TestCase):
    def check(self, results, manifest=None):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "manifest.json").write_text(json.dumps(manifest or [
                {"Id": "A", "Status": "paired"}, {"Id": "B", "Status": "paired"},
                {"Id": "C", "Status": "pending"}]), encoding="utf-8")
            for i, result in enumerate(results):
                (root / f"shard-{i}.json").write_text(json.dumps(result), encoding="utf-8")
            module.verify(root / "manifest.json", root, 2)

    def setUp(self):
        self.results = [{"Shard": i, "Total": 2, "Results": [{"Id": name, "CheckedValues": 10}]}
                        for i, name in enumerate(["A", "B"])]

    def test_complete_union(self):
        self.check(self.results)

    def test_missing_shard(self):
        with self.assertRaises(ValueError): self.check(self.results[:1])

    def test_duplicate_shard(self):
        with self.assertRaises(ValueError): self.check([self.results[0], self.results[0]])

    def test_omitted_pair(self):
        self.results[1]["Results"] = []
        with self.assertRaises(ValueError): self.check(self.results)

    def test_pending_pair_cannot_count(self):
        self.results[1]["Results"][0]["Id"] = "C"
        with self.assertRaises(ValueError): self.check(self.results)

    def test_zero_values_cannot_count(self):
        self.results[0]["Results"][0]["CheckedValues"] = 0
        with self.assertRaises(ValueError): self.check(self.results)

    def test_duplicate_pair(self):
        self.results[1]["Results"] = copy.deepcopy(self.results[0]["Results"])
        with self.assertRaises(ValueError): self.check(self.results)

if __name__ == "__main__": unittest.main()
