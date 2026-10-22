import unittest
import json
import tempfile
from pathlib import Path
from unittest.mock import patch

from build_remaining_indicator_inventory import contract_receipts, enum_members, inventory, source_text


class RemainingInventoryTests(unittest.TestCase):
    def test_explicit_batch_receipt_does_not_claim_builder_option_coverage(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            receipts = root / 'benchmarks/results/remaining-indicators'
            receipts.mkdir(parents=True)
            (receipts / 'batch-contracts.json').write_text(json.dumps(
                {'types': ['Example'], 'receipt_scope': 'batch-formulas'}))
            batch = {}
            result = contract_receipts(root, [('ExampleSpecOptions', 'Example')],
                lambda relative: (root / relative).read_text(), batch, {'Example'})
            self.assertEqual({}, result)
            self.assertEqual({'Example': ['benchmarks/results/remaining-indicators/batch-contracts.json']}, batch)

    def test_batch_receipt_requires_a_known_enum_formula(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            receipts = root / 'benchmarks/results/remaining-indicators'
            receipts.mkdir(parents=True)
            (receipts / 'batch-contracts.json').write_text(json.dumps(
                {'types': ['Unknown'], 'receipt_scope': 'batch-formulas'}))
            read = lambda relative: (root / relative).read_text()
            with self.assertRaises(ValueError):
                contract_receipts(root, [], read)
            with self.assertRaises(ValueError):
                contract_receipts(root, [], read, {}, {'Example'})

    def test_shared_gpu_receipts_do_not_claim_facade_coverage(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            receipts = root / 'benchmarks/results/remaining-indicators'
            receipts.mkdir(parents=True)
            (receipts / 'family-cpu-contracts.json').write_text(json.dumps({'types': ['Example']}))
            (receipts / 'batch-gpu-contracts.json').write_text(json.dumps({'methods': ['GpuTest']}))
            result = contract_receipts(root, [('ExampleSpecOptions', 'Example')],
                                       lambda relative: (root / relative).read_text())
            self.assertEqual({'ExampleSpecOptions': [
                'benchmarks/results/remaining-indicators/family-cpu-contracts.json']}, result)

    def test_family_receipt_without_types_is_still_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            receipts = root / 'benchmarks/results/remaining-indicators'
            receipts.mkdir(parents=True)
            (receipts / 'family-contracts.json').write_text('{}')
            with self.assertRaises(KeyError):
                contract_receipts(root, [], lambda relative: (root / relative).read_text())

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
