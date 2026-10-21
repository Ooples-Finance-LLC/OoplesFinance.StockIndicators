import json

from test_hosted_performance_results import HostedResultsTests
from primary_baseline_report import measured_dataset, replace_primary, embedded, script


class PrimaryBaselineTests(HostedResultsTests):
    def test_replaces_actual_numbers_and_preserves_raw_details(self):
        dataset = measured_dataset(self.folder, self.source, self.run, self.manifest, {'Skender': '2.7.3'})
        old = dict(rows=[{'ooples_mean_ns': 999999}], evidence={'sourceSha': 'old'})
        document = ('<body><section class="hero"><p class="lede">old</p></section>'
            '<section id="rollout-status">status</section>'
            '<section id="overview"><h2>October 8 competitor baseline</h2><b id="wins">0 / 1</b></section>'
            '<section id="explorer">Explore the October 8 baseline</section>'
            + script('dataset', old) + '</body>')
        result = replace_primary(document, dataset)
        self.assertEqual(dataset, embedded(result, 'dataset'))
        self.assertEqual(old, embedded(result, 'archived-baseline-dataset'))
        self.assertEqual([99, 100, 101], embedded(result, 'dataset')['rows'][0]['raw']['Ooples']['samples'])
        self.assertEqual('Skender', dataset['rows'][0]['library'])
        self.assertIn('<b id="wins">1 / 1</b>', result)
        self.assertLess(result.index('id="explorer"'), result.index('id="rollout-status"'))
        again = replace_primary(result, dataset)
        self.assertEqual(result, again)
        self.assertEqual(1, result.count('id="dataset"'))
        self.assertNotIn('Explore the October 8 baseline', result)

    def test_primary_import_rejects_tampered_values(self):
        path = self.folder / 'comparisons.csv'
        path.write_bytes(path.read_bytes().replace(b'100,', b'1,'))
        with self.assertRaisesRegex(ValueError, 'comparison values'):
            measured_dataset(self.folder, self.source, self.run, self.manifest, {})
