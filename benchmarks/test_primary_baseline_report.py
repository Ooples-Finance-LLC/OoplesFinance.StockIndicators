import json
import copy

from test_hosted_performance_results import HostedResultsTests
from primary_baseline_report import measured_dataset, replace_primary, embedded, script, refresh


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
        for row in dataset['rows']:
            self.assertEqual(self.source, row['source_sha'])
            self.assertEqual(dataset['evidence']['runUrl'], row['measurement_run_url'])
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

    def test_status_refresh_preserves_imported_baseline_without_artifacts(self):
        dataset = measured_dataset(self.folder, self.source, self.run, self.manifest, {})
        document = ('<body><section class="hero"><p class="lede">Builder timing pending</p></section>'
            + script('dataset', dataset) + script('archived-baseline-dataset', {'rows': []}) + '</body>')
        result = refresh(document, self.root, self.root / 'different-evidence-root')
        self.assertEqual(dataset, embedded(result, 'dataset'))
        self.assertEqual({'rows': []}, embedded(result, 'archived-baseline-dataset'))
        self.assertIn('1 / 1 APIs at 10,000 bars', result)
        self.assertNotIn('Builder timing pending', result)

    def test_next_measurement_refresh_updates_existing_source_labels(self):
        dataset = measured_dataset(self.folder, self.source, self.run, self.manifest, {})
        document = ('<body><section class="hero"><p class="lede">old</p></section>'
            '<section id="explorer"><p class="sub">old source</p></section>'
            '<h3>Fresh full-library timing run</h3><p>old</p>' + script('dataset', {'rows': []}) + '</body>')
        first = replace_primary(document, dataset)
        next_data = copy.deepcopy(dataset)
        next_data['evidence']['sourceSha'] = 'b' * 40
        next_data['evidence']['runUrl'] = 'https://example.test/new-run'
        updated = replace_primary(first, next_data)
        self.assertIn('Measured source bbbbbbbb.', updated)
        self.assertIn('Refreshed baseline source: bbbbbbbb.', updated)
        self.assertNotIn('Measured source aaaaaaaa.', updated)
        self.assertNotIn('Refreshed baseline source: aaaaaaaa.', updated)

    def test_full_refresh_replaces_mixed_source_methodology(self):
        dataset = measured_dataset(self.folder, self.source, self.run, self.manifest, {})
        document = ('<body><section class="hero"><p class="lede">old</p></section>'
            '<p>Base source: oldbase (792 comparisons); SMA Analysis source: oldsma '
            '(two comparisons); Parabolic SAR source: oldsar (eight comparisons). '
            '<a href="https://example.test/old-run">Old evidence</a>.</p>'
            + script('dataset', {'rows': []}) + '</body>')
        updated = replace_primary(document, dataset)
        self.assertIn('Refreshed baseline source: ' + self.source[:8] + '.', updated)
        self.assertIn(dataset['evidence']['runUrl'], updated)
        for stale in ['Base source:', 'oldbase', 'oldsma', 'oldsar', 'https://example.test/old-run']:
            self.assertNotIn(stale, updated)
        self.assertEqual(updated, replace_primary(updated, dataset))
