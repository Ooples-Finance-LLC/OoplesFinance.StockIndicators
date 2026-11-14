from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing.fixture_inventory import ASSEMBLY, PREFIX, inspect_methods, parse_listing, record_inventory, requested_methods, verify_inventory
from hosted_timing.pipeline import methods_for


class FixtureInventoryTests(unittest.TestCase):
    def plan(self, native=False):
        return {'targeted_shards': [{'tasks': [
            {'fixture': 'LibraryPairBenchmarks', 'methods': ['Ooples', 'Competitor']},
            {'fixture': 'SharedCrowSoldierBenchmarks', 'methods': ['BuilderFull', 'BuilderLatestOnly'] + (['NativeSingle', 'NativeParallel'] if native else [])}
        ]}]}

    def listing(self, command, **kwargs):
        fixture = command[-1].strip('*')
        methods = ['Ooples', 'Competitor'] if fixture == 'LibraryPairBenchmarks' else ['BuilderFull', 'BuilderLatestOnly']
        self.assertEqual(command[2:4], ['--list', 'flat'])
        return '\n'.join(PREFIX + fixture + '.' + name for name in methods)

    def test_wrong_crow_native_inventory_fails_before_measurement(self):
        with self.assertRaisesRegex(AssertionError, 'SharedCrowSoldierBenchmarks lacks requested methods'):
            inspect_methods(Path('.'), self.plan(native=True), self.listing)

    def test_builder_only_control_retains_all_owned_rounds(self):
        result = inspect_methods(Path('.'), self.plan(), self.listing)
        self.assertEqual(result['SharedCrowSoldierBenchmarks']['available'], ['BuilderFull', 'BuilderLatestOnly'])
        task = self.plan()['targeted_shards'][0]['tasks'][1]
        self.assertEqual(sum(2 * len(methods_for(task, index)) for index in range(4)), 16)
        self.assertEqual(2 * len(methods_for(task, 0)), 4)

    def test_native_fixture_case_count_is_unchanged(self):
        task = self.plan(native=True)['targeted_shards'][0]['tasks'][1]
        self.assertEqual(sum(2 * len(methods_for(task, index)) for index in range(4)), 20)

    def test_listing_is_scoped_to_exact_fixture(self):
        text = PREFIX + 'Other.BuilderFull\n' + PREFIX + 'Wanted.NativeSingle\n' + 'unrelated output\n'
        self.assertEqual(parse_listing(text, 'Wanted'), ['NativeSingle'])

    def receipt(self, root):
        (root / 'common').mkdir()
        (root / 'common' / ASSEMBLY).write_bytes(b'immutable benchmark')
        plan = self.plan()
        inventories = inspect_methods(root / 'common', plan, self.listing)
        with patch('hosted_timing.fixture_inventory.inspect_methods', return_value=inventories):
            sha = record_inventory(root, plan, 'plan-sha')
        return plan, {'plan_sha256': 'plan-sha', 'fixture_inventory_sha256': sha}

    def test_receipt_binds_method_list_plan_and_assembly(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            plan, proof = self.receipt(root)
            verify_inventory(root, plan, proof)
            (root / 'common' / ASSEMBLY).write_bytes(b'changed benchmark')
            with self.assertRaises(AssertionError):
                verify_inventory(root, plan, proof)

    def test_plan_change_cannot_reuse_receipt(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            _, proof = self.receipt(root)
            with self.assertRaises(AssertionError):
                verify_inventory(root, self.plan(native=True), proof)

    def test_receipt_tampering_is_rejected(self):
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            plan, proof = self.receipt(root)
            receipt = root / 'fixture-method-inventory.json'
            receipt.write_bytes(receipt.read_bytes() + b' ')
            with self.assertRaises(AssertionError):
                verify_inventory(root, plan, proof)

    def test_request_union_preserves_methods_across_shards(self):
        plan = self.plan()
        plan['targeted_shards'].append({'tasks': [{'fixture': 'LibraryPairBenchmarks', 'methods': ['Ooples']}]})
        self.assertEqual(requested_methods(plan)['LibraryPairBenchmarks'], ['Competitor', 'Ooples'])


if __name__ == '__main__':
    unittest.main()
