"""A successful formula run cannot conceal missing numerical input classes at publication."""
from pathlib import Path
import subprocess
import unittest
import xml.etree.ElementTree as ET

CLASSES = ("tiny large-offset large alternating-scale evicted-spike zero negative subnormal overflow-adjacent cancelled-spike cascaded-cancellation "
           "volume-tiny volume-subnormal volume-overflow-adjacent volume-evicted-spike volume-alternating-scale "
           "mixed-ohlc-extremes mixed-ohlc-subnormal").split()
NAMES = ["xorshift32-v1/seed-244/" + name for name in CLASSES]

class NumericalReleaseEvidenceTests(unittest.TestCase):
    def report(self):
        root = ET.Element("correctnessEvidence", scope="all-discovered-configurations", requiredNumericalFixtures=",".join(NAMES))
        case = ET.SubElement(root, "case", name="example", passed="True")
        for name in NAMES:
            ET.SubElement(case, "fixture", name=name, passed="True", completed="True", inputBars="256", valuesChecked="256")
        return root

    def check(self, root, passes):
        script = Path(__file__).resolve().parents[1] / "Assert-CorrectnessNumericalEvidence.ps1"
        xml = ET.tostring(root, encoding="unicode")
        command = "& '" + str(script).replace("'", "''") + "' -Evidence ([xml]'" + xml.replace("'", "''") + "')"
        result = subprocess.run(["pwsh", "-NoProfile", "-NonInteractive", "-Command", command],
                                capture_output=True, text=True, timeout=30)
        self.assertEqual(passes, result.returncode == 0, result.stdout + result.stderr)

    def test_complete_numerical_evidence_passes(self):
        self.check(self.report(), True)

    def test_empty_filtered_missing_failed_duplicate_and_old_reports_fail(self):
        for change in ("empty", "filtered", "missing", "failed", "incomplete", "duplicate", "old", "empty-fixture", "missing-count", "omitted-requirement"):
            with self.subTest(change=change):
                root = self.report()
                case = root.find("case")
                fixture = case.find("fixture")
                if change == "empty": root.remove(case)
                elif change == "filtered": root.set("scope", "filtered")
                elif change == "missing": case.remove(fixture)
                elif change == "failed": fixture.set("passed", "False")
                elif change == "incomplete": fixture.set("completed", "False")
                elif change == "duplicate": case.append(ET.fromstring(ET.tostring(fixture)))
                elif change == "old": root.attrib.pop("requiredNumericalFixtures")
                elif change == "empty-fixture": fixture.set("inputBars", "0")
                elif change == "missing-count": fixture.attrib.pop("valuesChecked")
                else: root.set("requiredNumericalFixtures", ",".join(NAMES[:-1]))
                self.check(root, False)

    def overflow_report(self):
        root = self.report()
        case = root.find("case")
        case.set("overflowReferenceSlots", "0")
        case.set("independentTrajectorySlots", "0")
        case.set("values", "4352")
        fixture = case.find("fixture")
        fixture.set("valuesChecked", "2")
        fixture.set("outputOverflowRejectionsChecked", "2")
        fixture.set("outputOverflowBarIndex", "2")
        fixture.set("outputOverflowSlot", "0")
        fixture.set("outputOverflowSign", "1")
        return root

    def test_extra_rejection_receipts_are_checked_too(self):
        root = self.overflow_report()
        fixture = root.find("case/fixture")
        extra = ET.fromstring(ET.tostring(fixture))
        extra.set("name", "additional-overflow")
        extra.set("outputOverflowSign", "0")
        root.find("case").append(extra)
        self.check(root, False)

    def test_declaring_overflow_without_exercising_rejection_is_incomplete(self):
        root = self.report()
        root.find("case").set("overflowReferenceSlots", "0")
        self.check(root, False)

    def test_complete_oracle_backed_rejection_passes(self):
        self.check(self.overflow_report(), True)
        root = self.overflow_report()
        fixture = root.find("case/fixture")
        fixture.set("valuesChecked", "0")
        fixture.set("outputOverflowBarIndex", "0")
        fixture.set("outputOverflowSign", "-1")
        self.check(root, True)

    def test_incomplete_or_contradictory_rejection_fails_closed(self):
        for field, value in (("outputOverflowRejectionsChecked", "1"), ("outputOverflowRejectionsChecked", "0"),
                             ("outputOverflowRejectionsChecked", "bad"), ("outputOverflowRejectionsChecked", "-2"),
                             ("outputOverflowBarIndex", ""), ("outputOverflowBarIndex", "256"),
                             ("outputOverflowBarIndex", "-1"), ("outputOverflowBarIndex", "3"),
                             ("outputOverflowSlot", "1"), ("outputOverflowSlot", "-1"),
                             ("outputOverflowSign", "0"), ("outputOverflowSign", "")):
            with self.subTest(field=field, value=value):
                root = self.overflow_report()
                root.find("case/fixture").set(field, value)
                self.check(root, False)
        for field in ("overflowReferenceSlots", "independentTrajectorySlots", "values"):
            root = self.overflow_report()
            root.find("case").attrib.pop(field)
            self.check(root, False)

    def test_new_generator_classes_automatically_become_required(self):
        root = self.report()
        root.set("requiredNumericalFixtures", ",".join(NAMES + ["future-class"]))
        self.check(root, False)
        ET.SubElement(root.find("case"), "fixture", name="future-class", passed="True", completed="True", inputBars="256", valuesChecked="256")
        self.check(root, True)

    def test_field_classes_cannot_be_removed_from_both_manifest_and_receipts(self):
        for name in NAMES[11:]:
            with self.subTest(name=name):
                root = self.report()
                root.set("requiredNumericalFixtures", ",".join(n for n in NAMES if n != name))
                case = root.find("case")
                case.remove(next(f for f in case if f.attrib["name"] == name))
                self.check(root, False)

if __name__ == "__main__": unittest.main()
