import unittest
from pathlib import Path
from unittest.mock import patch

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
        self.assertTrue(all(row['status'] in {'pending-assessment',
                           'contract-receipts-recorded-performance-pending'} for row in report['batch_formulas']))
        self.assertEqual(len(report['handwritten_facades']),
                         len({row['type'] for row in report['handwritten_facades']}))

    def test_receipt_does_not_hide_unassessed_alias_or_claim_performance(self):
        with patch('build_remaining_indicator_inventory.contract_receipts',
                   return_value={'MidRangeSpecOptions': ['fixture-contracts.json']}):
            report = inventory(Path(__file__).resolve().parents[1])
        median = next(row for row in report['batch_formulas'] if row['indicator'] == 'MedianPrice')
        self.assertIn('MidRangeSpecOptions', median['contract_receipts'])
        self.assertIn('MedianPriceSpecOptions', median['options_without_receipts'])
        self.assertEqual('contract-receipts-recorded-performance-pending', median['status'])


if __name__ == '__main__':
    unittest.main()
