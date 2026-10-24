import copy
import datetime
import json
from pathlib import Path
import tempfile
import unittest

from record_full_batch_performance import collect as collect_fixture, validate_quiet, FIXTURES
FIXTURE = "KaufmanStressBatchBenchmarks"
def collect(data): return collect_fixture(data, FIXTURE)


def report(fixture=FIXTURE):
    return {'HostEnvironmentInfo': {'RuntimeVersion': '.NET 10.0.12'}, 'Benchmarks': [
        {'FullName': f'{fixture}.{method}(Count: {count})', 'Method': method,
         'Statistics': {'N': 10, 'Mean': 100, 'ConfidenceInterval': {'Lower': 90, 'Upper': 110}},
         'Memory': {'BytesAllocatedPerOperation': 1000}}
        for count in (1000, 10000) for method in ('Legacy', 'Shared')]}


class RecorderTests(unittest.TestCase):
    def test_all_three_fixtures_keep_their_formula_identity(self):
        for fixture, operation in FIXTURES.items():
            rows = collect_fixture(report(fixture), fixture)
            self.assertEqual(2, len(rows))
            self.assertEqual({operation}, {row['operation'] for row in rows})

    def test_other_fixture_cannot_be_accepted(self):
        for fixture in FIXTURES:
            other = next(f for f in FIXTURES if f != fixture)
            with self.assertRaises(ValueError): collect_fixture(report(other), fixture)

    def test_counts_modes_and_control_are_explicit(self):
        rows = collect(report())
        self.assertEqual(2, len(rows))
        self.assertEqual(0, sum(r['control'] for r in rows))
        self.assertEqual({'FullPairedBatch'}, {r['history'] for r in rows})
        self.assertEqual({'inconclusive'}, {r['verdict'] for r in rows})

    def test_only_separated_intervals_establish_a_verdict(self):
        data = report()
        for row in data['Benchmarks']:
            if row['Method'] == 'Shared':
                row['Statistics'].update(Mean=50, ConfidenceInterval={'Lower': 40, 'Upper': 60})
        self.assertEqual({'faster'}, {r['verdict'] for r in collect(data)})
        for row in data['Benchmarks']:
            if row['Method'] == 'Shared':
                row['Statistics'].update(Mean=150, ConfidenceInterval={'Lower': 140, 'Upper': 160})
        self.assertEqual({'slower'}, {r['verdict'] for r in collect(data)})

    def test_missing_arm_is_rejected(self):
        data = report(); data['Benchmarks'].pop()
        with self.assertRaisesRegex(ValueError, 'Missing or extra'):
            collect(data)

    def test_duplicate_arm_is_rejected(self):
        data = report(); data['Benchmarks'].append(copy.deepcopy(data['Benchmarks'][0]))
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            collect(data)

    def test_low_sample_count_is_rejected(self):
        data = report(); data['Benchmarks'][0]['Statistics']['N'] = 6
        with self.assertRaisesRegex(ValueError, 'Insufficient'):
            collect(data)

    def test_nonfinite_or_invalid_statistics_are_rejected(self):
        for value in (float('nan'), float('inf'), -1):
            data = report(); data['Benchmarks'][0]['Statistics']['Mean'] = value
            with self.subTest(value=value), self.assertRaises(ValueError):
                collect(data)
        data = report(); data['Benchmarks'][0]['Memory']['BytesAllocatedPerOperation'] = -1
        with self.assertRaises(ValueError):
            collect(data)

    def test_builder_history_label_is_rejected(self):
        data = report(); data['Benchmarks'][0]['FullName'] += ', History: LatestOnly'
        with self.assertRaisesRegex(ValueError, 'history'):
            collect(data)

    def test_unexpected_runtime_is_rejected(self):
        data = report(); data['HostEnvironmentInfo']['RuntimeVersion'] = '.NET 10.0.13'
        with self.assertRaisesRegex(ValueError, 'runtime'):
            collect(data)

    def test_wrong_size_is_not_silently_counted(self):
        data = report(); data['Benchmarks'][0]['FullName'] = data['Benchmarks'][0]['FullName'].replace('1000', '100000')
        with self.assertRaisesRegex(ValueError, 'Missing or extra'):
            collect(data)

    def test_short_quiet_window_cannot_reach_activity_verifier(self):
        self.check_quiet(239, False)

    def test_full_quiet_window_still_requires_activity_verifier(self):
        self.check_quiet(240, True)

    def check_quiet(self, seconds, permitted):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / 'synthetic'
            start = datetime.datetime(2026, 1, 1, tzinfo=datetime.timezone.utc)
            quiet = [{'Time': (start + datetime.timedelta(seconds=i * seconds / 4)).isoformat()} for i in range(5)]
            Path(str(source) + '.quiet.json').write_text(json.dumps(quiet), encoding='utf-8')
            calls = []
            def verify(*args):
                calls.append(args)
                raise RuntimeError('Activity rejection remains binding')
            if permitted:
                with self.assertRaisesRegex(RuntimeError, 'Activity rejection'):
                    validate_quiet(source, 123, [], verify)
                self.assertEqual(1, len(calls))
            else:
                with self.assertRaisesRegex(ValueError, '240-second'):
                    validate_quiet(source, 123, [], verify)
                self.assertEqual([], calls)


if __name__ == '__main__':
    unittest.main()
