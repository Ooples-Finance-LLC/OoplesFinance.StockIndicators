import json
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from merge_correctness_shards import merge
from unit_test_shards import check_trx, read_plan


class ContractShardTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.paths = []
        for index in range(2):
            root = ET.Element('correctnessEvidence', scope='shard', shardIndex=str(index), shardCount='2',
                              assembly='lib', assemblySha256='abc', sourceRevision='head', runtime='10',
                              os='Windows', pointerBits='64', requiredNumericalFixtures='tiny')
            inventory = ET.SubElement(root, 'inventory')
            for name in ['a', 'b', 'c']:
                ET.SubElement(inventory, 'name').text = name
            for name in ['a', 'b', 'c'][index::2]:
                ET.SubElement(root, 'case', name=name, passed='True')
            path = Path(self.temp.name) / f'{index}.xml'
            ET.ElementTree(root).write(path)
            self.paths.append(path)

    def mutate(self, fn):
        root = ET.parse(self.paths[1]).getroot()
        fn(root)
        ET.ElementTree(root).write(self.paths[1])

    def test_complete_disjoint_shards_merge(self):
        root = merge(self.paths, 2)
        self.assertEqual(root.get('scope'), 'all-discovered-configurations')
        self.assertEqual([c.get('name') for c in root.findall('case')], ['a', 'b', 'c'])

    def test_package_identity_is_preserved_and_must_match(self):
        for path in self.paths:
            root = ET.parse(path).getroot()
            root.set('packageSha256', 'candidate')
            root.set('packageVersion', '2.0.0-test')
            root.set('targetFramework', 'net10.0')
            ET.ElementTree(root).write(path)
        self.assertEqual(merge(self.paths, 2).get('packageSha256'), 'candidate')
        self.mutate(lambda r: r.set('packageSha256', 'different'))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_partial_package_identity_is_rejected(self):
        root = ET.parse(self.paths[0]).getroot()
        root.set('packageSha256', 'candidate')
        ET.ElementTree(root).write(self.paths[0])
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_missing_and_duplicate_shards_fail(self):
        for paths in [self.paths[:1], [self.paths[0], self.paths[0]]]:
            with self.assertRaises(ValueError):
                merge(paths, 2)

    def test_changed_binary_fails(self):
        self.mutate(lambda r: r.set('assemblySha256', 'different'))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_changed_inventory_fails(self):
        self.mutate(lambda r: setattr(r.find('./inventory/name'), 'text', 'other'))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_missing_configuration_fails(self):
        self.mutate(lambda r: r.remove(r.find('case')))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_failed_configuration_fails(self):
        self.mutate(lambda r: r.find('case').set('passed', 'False'))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_misassigned_configuration_fails(self):
        self.mutate(lambda r: r.find('case').set('name', 'a'))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)

    def test_filtered_evidence_fails(self):
        self.mutate(lambda r: r.set('scope', 'filtered'))
        with self.assertRaises(ValueError):
            merge(self.paths, 2)


class UnitShardTests(unittest.TestCase):
    def test_twenty_parts_cover_every_method_once(self):
        names = [f'Class.Test{i:04}' for i in range(421)]
        parts = [names[i::20] for i in range(20)]
        self.assertEqual(sorted(n for part in parts for n in part), names)
        self.assertLessEqual(max(map(len, parts)) - min(map(len, parts)), 1)

    def test_actual_results_must_match_selected_methods(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'results.trx'
            root = ET.fromstring('''<TestRun><Results><UnitTestResult testId="1" outcome="Passed"/>
              <UnitTestResult testId="2" outcome="Passed"/></Results><TestDefinitions>
              <UnitTest id="1"><TestMethod className="Class" name="Theory"/></UnitTest>
              <UnitTest id="2"><TestMethod className="Class" name="Theory"/></UnitTest></TestDefinitions>
              <ResultSummary><Counters total="2"/></ResultSummary></TestRun>''')
            ET.ElementTree(root).write(path)
            self.assertEqual(check_trx(path, ['Class.Theory']), 2)
            for expected in [[], ['Class.Other'], ['Class.Theory', 'Class.Missing']]:
                with self.assertRaises(ValueError):
                    check_trx(path, expected)
            root.find('.//UnitTestResult').set('outcome', 'NotExecuted')
            ET.ElementTree(root).write(path)
            with self.assertRaises(ValueError):
                check_trx(path, ['Class.Theory'])

    def test_duplicate_inventory_cannot_hide_missing_methods(self):
        with tempfile.TemporaryDirectory() as temp:
            path = Path(temp) / 'plan.json'
            path.write_text(json.dumps({'methods': ['A', 'A'], 'count': 1}), encoding='utf-8')
            with self.assertRaises(ValueError):
                read_plan(path)


if __name__ == '__main__':
    unittest.main()
