import copy
import unittest
from current_report_status import render_status, STATUS_FILTER_SCRIPT
from primary_baseline_report import embedded, script


class CurrentReportStatusTests(unittest.TestCase):
    def setUp(self):
        self.data = {'evidence': {'sourceSha': 'a'*40, 'runUrl': 'https://example.test/run'},
                     'rows': [{'pair': 'Library.Pair', 'bars': 1000}, {'pair': 'Library.Pair', 'bars': 10000}]}
        self.builder = {'full_comparisons': 4, 'verdicts': {'faster': 1, 'slower': 0, 'inconclusive': 1}}
        self.ledger = {'measurement_totals': {'expected': 4}}
        self.inventory = {'batch_formulas': [{'status': 'contract-receipts-recorded-performance-pending'},
                                           {'status': 'pending-assessment'}],
                          'handwritten_facades': [{'status': 'pending-assessment'}]}
        self.document = ('<body><section id="rollout-status"><p>obsolete queue and old CI</p></section>'
            + script('rollout-status-data', self.ledger) + script('dataset', self.data)
            + script('archived-baseline-dataset', {'rows': []}) + script('current-competitor-data', self.builder)
            + '<script>'+STATUS_FILTER_SCRIPT+'</script><script>window.keep = 1;</script></body>')

    def test_current_counts_replace_stale_status_without_changing_measurements(self):
        result, status = render_status(self.document, 'b'*40, self.inventory, True, 'fixed-time')
        self.assertEqual(self.data, embedded(result, 'dataset'))
        self.assertEqual(self.builder, embedded(result, 'current-competitor-data'))
        self.assertEqual({'total': 2, 'correctness_receipts': 1}, status['formula_counts'])
        self.assertEqual(2, status['measurement_totals']['measured'])
        self.assertEqual(2, status['measurement_totals']['pending'])
        self.assertNotIn('obsolete queue', result)
        self.assertNotIn(STATUS_FILTER_SCRIPT, result)
        self.assertIn('window.keep = 1;', result)
        self.assertIn('matches the published production tree', result)
        self.assertEqual(status, embedded(result, 'rollout-status-data'))
        self.assertEqual(result, render_status(result, 'b'*40, self.inventory, True, 'fixed-time')[0])

    def test_later_production_changes_are_not_claimed_measured(self):
        result, status = render_status(self.document, 'b'*40, self.inventory, False, 'fixed-time')
        self.assertFalse(status['source_matches_published_production'])
        self.assertIn('not measured by this baseline run', result)

    def test_inconsistent_builder_counts_are_rejected(self):
        broken = copy.deepcopy(self.ledger)
        broken['measurement_totals']['expected'] = 1
        document = self.document.replace(script('rollout-status-data', self.ledger), script('rollout-status-data', broken))
        with self.assertRaisesRegex(ValueError, 'Invalid builder'):
            render_status(document, 'b'*40, self.inventory, True, 'fixed-time')

    def test_duplicate_status_sections_are_rejected(self):
        with self.assertRaisesRegex(ValueError, 'one current status section'):
            render_status(self.document+'<section id="rollout-status">duplicate</section>', 'b'*40, self.inventory, True, 'fixed-time')
