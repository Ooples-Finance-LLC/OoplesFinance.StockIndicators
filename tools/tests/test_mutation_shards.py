import hashlib
import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from merge_critical_mutations import merge_shards
from run_critical_mutations import select_shard, shard_matrix


def trx(outcome):
    return ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
            f'<UnitTestResult outcome="{outcome}"/></Results><ResultSummary>'
            '<Counters total="1" executed="1"/></ResultSummary></TestRun>')


class MutationShardTests(unittest.TestCase):
    def test_matrix_partitions_every_fault_once_with_bounded_shards(self):
        for size in (1, 10, 11, 352, 2560):
            entries = [{"id": str(i)} for i in range(size)]
            matrix = shard_matrix(entries)["include"]
            parts = [select_shard(entries, item["shard"], item["count"]) for item in matrix]
            self.assertTrue(all(0 < len(part) <= 10 for part in parts))
            self.assertEqual(list(range(size)), sorted(int(item["id"]) for part in parts for item in part))
        for size in (0, 2561):
            with self.assertRaises(ValueError):
                shard_matrix([{}] * size)
        for index, count in ((None, 2), (0, None), (-1, 1), (1, 1), (0, 0), (0, 3)):
            with self.assertRaises(ValueError):
                select_shard([{}, {}], index, count)

    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.manifest = [{"id": "a"}, {"id": "b"}]
        self.directories = [self.make_shard("a"), self.make_shard("b")]

    def make_shard(self, identifier, source=b"source"):
        directory = self.root / identifier
        directory.mkdir(exist_ok=True)
        archive = directory / "source-snapshot.zip"
        with zipfile.ZipFile(archive, "w") as bundle:
            bundle.writestr("src/example.cs", source)
        digest = hashlib.sha256(source).digest()
        evidence = {"manifest": self.manifest, "selectedMutationIds": [identifier],
                    "sourceFiles": [{"path": "src/example.cs", "sha256": digest.hex()}],
                    "sourceSnapshotSha256": hashlib.sha256(b"src/example.cs\0" + digest).hexdigest(),
                    "sourceArchiveSha256": hashlib.sha256(archive.read_bytes()).hexdigest(),
                    "baseline": {"outcome": "passed", "exitCode": 0},
                    "mutations": [{"id": identifier, "disposition": "killed", "outcome": "failed", "exitCode": 1}]}
        (directory / "evidence.json").write_text(json.dumps(evidence))
        for name, outcome in (("baseline", "Passed"), (identifier, "Failed")):
            (directory / name).mkdir(exist_ok=True)
            (directory / name / "results.trx").write_text(trx(outcome))
        return directory

    def change_evidence(self, edit):
        path = self.directories[0] / "evidence.json"
        data = json.loads(path.read_text())
        edit(data)
        path.write_text(json.dumps(data))

    def test_complete_shards_produce_full_evidence(self):
        result = merge_shards(self.directories, self.manifest)
        self.assertTrue(result["complete"])
        self.assertEqual(2, len(result["mutations"]))

    def test_missing_or_duplicate_shards_cannot_complete(self):
        for directories in ([], self.directories[:1], self.directories + self.directories[:1]):
            with self.assertRaises(ValueError):
                merge_shards(directories, self.manifest)

    def test_baseline_metadata_cannot_override_failed_tests(self):
        (self.directories[0] / "baseline" / "results.trx").write_text(trx("Failed"))
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)

    def test_timeout_cannot_credit_partial_failure(self):
        self.change_evidence(lambda data: data["mutations"][0].update(outcome="inconclusive"))
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)

    def test_skipped_or_missing_test_evidence_is_rejected(self):
        path = self.directories[0] / "a" / "results.trx"
        path.write_text(trx("NotExecuted"))
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)
        path.unlink()
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)

    def test_different_source_snapshots_cannot_be_combined(self):
        self.make_shard("b", b"changed source")
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)

    def test_archive_tampering_is_rejected(self):
        archive = self.directories[0] / "source-snapshot.zip"
        archive.write_bytes(archive.read_bytes() + b"tampered")
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)

    def test_manifest_mismatch_is_rejected(self):
        self.change_evidence(lambda data: data.update(manifest=[{"id": "a"}]))
        with self.assertRaises(ValueError):
            merge_shards(self.directories, self.manifest)


if __name__ == "__main__":
    unittest.main()
