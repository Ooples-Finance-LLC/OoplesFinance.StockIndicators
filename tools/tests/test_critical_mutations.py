import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

spec = importlib.util.spec_from_file_location("mutations", Path(__file__).resolve().parents[1] / "run_critical_mutations.py")
mutations = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mutations)


class MutationEvidenceTests(unittest.TestCase):
    def test_cli_accepts_utf8_manifests_with_and_without_bom(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "tools").mkdir()
            manifest = root / "tools/critical-mutations.json"
            for encoding in ["utf-8", "utf-8-sig"]:
                with self.subTest(encoding=encoding), \
                     patch.object(mutations, "__file__", str(root / "tools/run_critical_mutations.py")), \
                     patch("sys.argv", ["mutations", "--list-shards"]), \
                     patch("builtins.print") as output, \
                     patch.object(mutations.subprocess, "run") as build:
                    manifest.write_text(json.dumps([{"id": "example"}]), encoding=encoding)
                    self.assertEqual(0, mutations.main())
                    self.assertEqual({"include": [{"shard": 0, "count": 1}]}, json.loads(output.call_args.args[0]))
                    build.assert_not_called()
                    manifest.write_text("not json", encoding=encoding)
                    with self.assertRaises(json.JSONDecodeError):
                        mutations.main()

    def test_selected_build_targets_the_runtime_whose_results_are_checked(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "results.trx").write_text(self.trx("Passed"), encoding="utf-8")
            process = Mock()
            process.wait.return_value = 0
            with patch.object(mutations.subprocess, "Popen", return_value=process) as start:
                result = mutations.run_tests(path, path, "Example", 1)
            command = start.call_args.args[0]
            self.assertEqual("net10.0", command[command.index("-f") + 1])
            self.assertIn("-p:TargetFrameworks=net10.0", command)
            self.assertIn("--no-restore", command)
            self.assertNotIn("--no-build", command)
            self.assertEqual("passed", result["outcome"])

    def test_timeout_stops_children_and_never_credits_partial_failed_tests(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            (path / "results.trx").write_text(self.trx("Failed"), encoding="utf-8")
            process = Mock()
            process.wait.side_effect = mutations.subprocess.TimeoutExpired("dotnet", 1)
            with patch.object(mutations.subprocess, "Popen", return_value=process), \
                 patch.object(mutations, "stop_process_tree") as stop:
                result = mutations.run_tests(path, path, "example", 1)
            stop.assert_called_once()
            self.assertIs(process, stop.call_args.args[0])
            self.assertEqual("inconclusive", result["outcome"])
            self.assertEqual("timeout", result["reason"])

    def test_missing_partial_skipped_and_malformed_evidence_is_inconclusive(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.trx"
            self.assertEqual("inconclusive", mutations.test_outcome(path))
            for text in ["broken", '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"/>',
                         self.trx("NotExecuted"), self.trx("Failed", total=2)]:
                path.write_text(text, encoding="utf-8")
                self.assertEqual("inconclusive", mutations.test_outcome(path))

    def test_only_completed_test_failures_count_as_failed_evidence(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "results.trx"
            for outcome, expected in [("Passed", "passed"), ("Failed", "failed")]:
                path.write_text(self.trx(outcome), encoding="utf-8")
                self.assertEqual(expected, mutations.test_outcome(path))

    def test_stale_or_ambiguous_mutation_sites_are_errors(self):
        entry = {"id": "example", "before": "old", "after": "new"}
        for source in ["absent", "old old"]:
            with self.assertRaises(ValueError):
                mutations.mutate(source, entry)
        self.assertEqual("new", mutations.mutate("old", entry))

    def test_selected_run_rejects_stale_unselected_site_before_build(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            (root / "tools").mkdir()
            (root / "source.cs").write_text("old", encoding="utf-8")
            entries = [{"id": name, "module": "test", "file": "source.cs", "before": before,
                        "after": "new", "filter": "Example"}
                       for name, before in [("selected", "old"), ("unselected", "missing")]]
            (root / "tools/critical-mutations.json").write_text(json.dumps(entries), encoding="utf-8")
            with patch.object(mutations, "__file__", str(root / "tools/run_critical_mutations.py")), \
                 patch("sys.argv", ["mutations", "--output", str(root / "evidence"), "--only", "selected"]), \
                 patch.object(mutations.subprocess, "check_output", return_value=b"source.cs\0"), \
                 patch.object(mutations.subprocess, "run") as restore, \
                 patch.object(mutations, "run_tests") as run:
                with self.assertRaisesRegex(ValueError, "unselected: expected one mutation site"):
                    mutations.main()
                restore.assert_not_called()
                run.assert_not_called()

    def test_every_current_manifest_site_applies_uniquely(self):
        repo = Path(__file__).resolve().parents[2]
        entries = json.loads((repo / "tools/critical-mutations.json").read_text(encoding="utf-8-sig"))
        sources = {entry["file"]: (repo / entry["file"]).read_text(encoding="utf-8") for entry in entries}
        for entry in entries:
            with self.subTest(mutation=entry["id"]):
                changed = mutations.mutate(sources[entry["file"]], entry)
                self.assertNotEqual(sources[entry["file"]], changed)

    def test_selected_faults_cannot_claim_full_campaign_completion(self):
        entries = [{"id": "a"}, {"id": "b"}]
        selected = mutations.select_entries(entries, ["b"])
        results = [{"id": "b", "disposition": "killed"}]
        self.assertTrue(mutations.campaign_complete(selected, results))
        self.assertFalse(mutations.campaign_complete(entries, results))
        self.assertFalse(mutations.campaign_complete(entries, results + results))
        self.assertTrue(mutations.campaign_complete(entries, results + [{"id": "a", "disposition": "killed"}]))
        self.assertFalse(mutations.campaign_complete(entries, results + [{"id": "a", "disposition": "survived"}]))
        for identifiers in [[], ["missing"], ["a", "a"]]:
            with self.assertRaises(ValueError):
                mutations.select_entries(entries, identifiers)

    @staticmethod
    def trx(outcome, total=1):
        return ('<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>'
                f'<UnitTestResult outcome="{outcome}"/></Results><ResultSummary>'
                f'<Counters total="{total}" executed="1"/></ResultSummary></TestRun>')


if __name__ == "__main__":
    unittest.main()
