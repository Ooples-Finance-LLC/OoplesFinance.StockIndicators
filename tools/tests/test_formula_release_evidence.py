"""The release gate must refuse incomplete formula evidence even when every fixture passed."""
from pathlib import Path
import subprocess
import unittest


class FormulaReleaseEvidenceTests(unittest.TestCase):
    def check_report(self, attributes, passes):
        script = Path(__file__).resolve().parents[1] / "Assert-CorrectnessFormulaEvidence.ps1"
        report = '<correctnessEvidence><case name="example" passed="True" ' + attributes + '/></correctnessEvidence>'
        # Literal fixture text and paths are escaped as PowerShell single-quoted arguments.
        command = "& '" + str(script).replace("'", "''") + "' -Evidence ([xml]'" + report.replace("'", "''") + "')"
        result = subprocess.run(["pwsh", "-NoProfile", "-NonInteractive", "-Command", command],
                                capture_output=True, text=True, timeout=30)
        self.assertEqual(passes, result.returncode == 0, result.stdout + result.stderr)

    def test_complete_formula_evidence_passes(self):
        self.check_report('recurrenceOnlySlots="" missingStartupSlots="" independentTrajectorySlots="0,1"', True)

    def test_recurrence_only_startup_omissions_empty_slots_and_older_schema_fail(self):
        for attributes in [
            'recurrenceOnlySlots="0" missingStartupSlots="" independentTrajectorySlots="1"',
            'recurrenceOnlySlots="" missingStartupSlots="0" independentTrajectorySlots="0"',
            'recurrenceOnlySlots="" missingStartupSlots="" independentTrajectorySlots=""',
            'recurrenceOnlySlots="" independentTrajectorySlots="0"',
        ]:
            with self.subTest(attributes=attributes):
                self.check_report(attributes, False)


if __name__ == "__main__":
    unittest.main()
