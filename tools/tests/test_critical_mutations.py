import importlib.util
from pathlib import Path
import tempfile
import unittest
from unittest.mock import Mock, patch

spec = importlib.util.spec_from_file_location("mutations", Path(__file__).resolve().parents[1] / "run_critical_mutations.py")
mutations = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mutations)


class MutationEvidenceTests(unittest.TestCase):
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
