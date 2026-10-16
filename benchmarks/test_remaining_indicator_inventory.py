import unittest
from pathlib import Path

from build_remaining_indicator_inventory import enum_members, inventory, source_text


class RemainingInventoryTests(unittest.TestCase):
    def test_unrecognized_enum_syntax_cannot_silently_drop_entries(self):
        with self.assertRaises(ValueError):
            enum_members('public enum IndicatorName { None, Example = 2 }')

    def test_duplicate_enum_members_are_rejected(self):
        with self.assertRaises(ValueError):
            enum_members('public enum IndicatorName { None, Example, Example }')

    def test_every_formula_is_retained_including_those_without_options(self):
        root = Path(__file__).resolve().parents[1]
        report = inventory(root)
        expected = enum_members(source_text(root / 'src/Enums/IndicatorName.cs'))
        self.assertEqual(expected, [row['indicator'] for row in report['batch_formulas']])
        self.assertTrue(all(row['status'] == 'pending-assessment' for row in report['batch_formulas']))
        self.assertEqual(len(report['handwritten_facades']),
                         len({row['type'] for row in report['handwritten_facades']}))


if __name__ == '__main__':
    unittest.main()
