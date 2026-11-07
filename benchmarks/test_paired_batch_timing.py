import copy
import unittest

from paired_batch_timing import FIXTURES, collect


def report(fixture='KaufmanStressBatchBenchmarks'):
    data = {'HostEnvironmentInfo': {'RuntimeVersion': '.NET 10.0.12'}, 'Benchmarks': []}
    for operation in FIXTURES[fixture]:
        for count in (1000, 10000):
            for method, mean in [('Legacy', 100), ('Shared', 50)]:
                parameters = f'Count: {count}'
                if len(FIXTURES[fixture]) > 1:
                    parameters += ', Operation: ' + operation
                data['Benchmarks'].append(dict(
                    FullName=f'Example.{fixture}.{method}({parameters})', Method=method,
                    Statistics=dict(N=10, Mean=mean, ConfidenceInterval=dict(Lower=mean-5, Upper=mean+5)),
                    Memory=dict(BytesAllocatedPerOperation=100 if method == 'Legacy' else 20)))
    return data


class PairedBatchTimingTests(unittest.TestCase):
    def test_all_four_grids_keep_control_and_baseline_scope(self):
        for fixture in FIXTURES:
            result = collect(report(fixture), fixture)
            self.assertEqual(2 * len(FIXTURES[fixture]), len(result['rows']))
            self.assertEqual('statistics-validated-control-evidence-required', result['status'])
            for row in result['rows']:
                self.assertEqual('faster', row['verdict'])
                self.assertEqual(2, row['legacy_over_shared'])
                self.assertEqual(80, row['allocation_saved_bytes'])
            controls = [r for r in result['rows'] if r['role'] == 'control']
            self.assertEqual(2 if fixture == 'PairedBatchAllocationBenchmarks' else 0, len(controls))

    def test_missing_duplicate_unknown_or_cross_fixture_rows_are_rejected(self):
        original = report()
        cases = [dict(original, Benchmarks=[]), dict(original, Benchmarks=original['Benchmarks'][:-1]),
                 dict(original, Benchmarks=original['Benchmarks'] + original['Benchmarks'][:1])]
        for field, value in [('Method', 'NativeSingle'), ('FullName', 'Other.Shared(Count: 1000)'),
                             ('FullName', 'Example.KaufmanStressBatchBenchmarks.Shared(Count: 1000, Count: 1000)')]:
            changed = copy.deepcopy(original); changed['Benchmarks'][0][field] = value; cases.append(changed)
        for data in cases:
            with self.assertRaises(ValueError): collect(data, 'KaufmanStressBatchBenchmarks')

    def test_unrecognized_parameters_and_sizes_are_not_merged(self):
        for params in ['Count: 20', 'Count: 1000, Variant: Other', 'Count: 1000, Operation: Wrong']:
            data = report(); data['Benchmarks'][0]['FullName'] = 'Example.KaufmanStressBatchBenchmarks.Legacy(' + params + ')'
            with self.assertRaises(ValueError): collect(data, 'KaufmanStressBatchBenchmarks')

    def test_bad_runtime_is_rejected(self):
        for runtime in ['.NET 10.0.11', '.NET 110.0.120', '']:
            data = report(); data['HostEnvironmentInfo']['RuntimeVersion'] = runtime
            with self.assertRaises(ValueError): collect(data, 'KaufmanStressBatchBenchmarks')

    def test_nonfinite_inconsistent_and_insufficient_statistics_are_rejected(self):
        for field, value in [('N', 6), ('N', 7.5), ('N', True), ('Mean', float('nan')),
                             ('Mean', float('inf')), ('Mean', 200), ('Mean', 0)]:
            data = report(); data['Benchmarks'][0]['Statistics'][field] = value
            with self.assertRaises(ValueError): collect(data, 'KaufmanStressBatchBenchmarks')
        for allocation in [-1, float('inf'), None, True]:
            data = report(); data['Benchmarks'][0]['Memory']['BytesAllocatedPerOperation'] = allocation
            with self.assertRaises(ValueError): collect(data, 'KaufmanStressBatchBenchmarks')

    def test_touching_intervals_are_inconclusive_and_regressions_visible(self):
        for mean, lower, verdict in [(110, 105, 'inconclusive'), (111, 106, 'slower')]:
            data = report()
            for row in data['Benchmarks']:
                if row['Method'] == 'Shared':
                    row['Statistics'].update(Mean=mean, ConfidenceInterval=dict(Lower=lower, Upper=120))
                    row['Memory']['BytesAllocatedPerOperation'] = 150
            rows = collect(data, 'KaufmanStressBatchBenchmarks')['rows']
            self.assertTrue(all(r['verdict'] == verdict and r['allocation_saved_bytes'] == -50 for r in rows))


if __name__ == '__main__':
    unittest.main()
