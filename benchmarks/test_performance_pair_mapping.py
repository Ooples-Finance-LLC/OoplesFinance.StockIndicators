import unittest
import hashlib
import tempfile
import zipfile
from pathlib import Path
from unittest.mock import patch
from performance_pair_mapping import reconcile, source_mappings, snapshot_mappings


class PairMappingTests(unittest.TestCase):
    def test_reviewed_helper_requires_both_exact_files_and_records_dependency(self):
        fixture = b'class Example {}'
        helper = b'class Helper {}'
        reviewed = dict(fixture='F', operation='F', pair='Library.Sum', source='F.cs',
                        source_sha256=hashlib.sha256(fixture).hexdigest(), helper='H.cs',
                        helper_sha256=hashlib.sha256(helper).hexdigest())
        with tempfile.TemporaryDirectory() as folder:
            archive = Path(folder) / 'source.zip'
            for fixture_data, helper_data, pairs, expected in [
                    (fixture, helper, {'Library.Sum'}, 1),
                    (fixture + b'changed', helper, {'Library.Sum'}, 0),
                    (fixture, helper + b'changed', {'Library.Sum'}, 0),
                    (fixture, None, {'Library.Sum'}, 0),
                    (fixture, helper, {'Other.Api'}, 0)]:
                with self.subTest(fixture=fixture_data, helper=helper_data, pairs=pairs):
                    with zipfile.ZipFile(archive, 'w') as source:
                        source.writestr('F.cs', fixture_data)
                        if helper_data is not None:
                            source.writestr('H.cs', helper_data)
                    with patch('performance_pair_mapping.REVIEWED_HELPER_MAPPINGS', [reviewed]):
                        result = snapshot_mappings(archive, [dict(fixture='F', source='F.cs')], pairs)
                    self.assertEqual(expected, result['mapped_pairs'])
                    if expected:
                        row = result['mappings'][0]
                        self.assertEqual('Library.Sum', row['pair'])
                        self.assertEqual([dict(source='H.cs', sha256=reviewed['helper_sha256'])],
                                         row['dependencies'])
                        fresh = [dict(fixture='F', operation='F', bars=1000, mode='BuilderFull')]
                        matched, unmatched = reconcile(fresh, result['mappings'], pairs)
                        self.assertEqual(1, len(matched))
                        self.assertEqual([], unmatched)

    def test_direct_skender_result_requires_typed_receiver_and_full_native_body(self):
        source = ('using Skender.Stock.Indicators;\nprivate Quote[] _quotes = null!;\n'
                  'public List<FractalResult> NativeSingle() => '
                  '(List<FractalResult>)_quotes.GetFractal(Wing, Wing, EndType.HighLow);')
        pairs = {'Skender.GetFractal'}
        self.assertEqual([dict(fixture='Fractal', operation='Fractal', pair='Skender.GetFractal')],
                         source_mappings(source, 'Fractal', pairs))
        for changed in [source.replace('Quote[]', 'Other[]'),
                        source.replace('Skender.Stock.Indicators', 'Other'),
                        source.replace(');', ').Select(x => x);'),
                        source.replace('NativeSingle', 'Setup'),
                        '/*' + source + '*/']:
            self.assertEqual([], source_mappings(changed, 'Fractal', pairs))

    def test_direct_skender_call_does_not_map_every_parameterized_operation(self):
        source = ('using Skender.Stock.Indicators;\nprivate Quote[] _quotes;\n'
                  '[Params("A", "B")] public string Operation;\n'
                  'public List<FractalResult> NativeSingle() => '
                  '(List<FractalResult>)_quotes.GetFractal(Wing);')
        self.assertEqual([], source_mappings(source, 'F', {'Skender.GetFractal'}))

    def test_two_direct_native_ternary_branches_are_scoped_to_operation(self):
        source = '[Params("Min", "Max")] public string Operation; Operation == "Max" ? new QuanTAlib.Max(Period, 0) : new QuanTAlib.Min(Period, 0);'
        rows = source_mappings(source, 'Extreme', {'QuanTAlib.Min', 'QuanTAlib.Max'})
        self.assertEqual({'Min': 'QuanTAlib.Min', 'Max': 'QuanTAlib.Max'}, {r['operation']: r['pair'] for r in rows})

    def test_native_ternary_allows_nested_arguments_but_no_output_transform(self):
        source = '[Params("MidPoint", "MidPrice")] public string Operation; Operation == "MidPoint" ? Functions.MidPoint<double>(values.AsSpan(a,b), range) : Functions.MidPrice<double>(high.AsSpan(a,b), low.AsSpan(a,b), range);'
        pairs = {'TaLib.Functions.MidPoint', 'TaLib.Functions.MidPrice'}
        self.assertEqual(2, len(source_mappings(source, 'Mid', pairs)))
        self.assertEqual([], source_mappings(source.replace(', range);', ', range).Select(x => x);'), 'Mid', pairs))

    def test_ternary_does_not_guess_unrecognized_or_multiway_dispatch(self):
        source = '[Params("Min", "Max")] public string Operation; Operation == "Max" ? new QuanTAlib.Max(Period) : new QuanTAlib.Min(Period);'
        pairs = {'QuanTAlib.Min', 'QuanTAlib.Max'}
        self.assertEqual([], source_mappings(source.replace('Operation ==', 'Other =='), 'F', pairs))
        self.assertEqual([], source_mappings(source.replace('"Min", "Max"', '"Min", "Max", "Third"'), 'F', pairs))
        self.assertEqual([], source_mappings(source.replace('QuanTAlib.', 'Unrelated.'), 'F', pairs))

    def test_short_operation_requires_native_switch_evidence(self):
        source = '[Params("Wma")] public string Operation {get;set;} "Wma" => Functions.Wma<double>()'
        rows = source_mappings(source, 'TaFixture', {'TaLib.Functions.Wma', 'QuanTAlib.Wma'})
        self.assertEqual('TaLib.Functions.Wma', rows[0]['pair'])
        self.assertEqual([], source_mappings(source.replace('Functions.Wma', 'Unrelated.Wma'), 'F', {'TaLib.Functions.Wma'}))

    def test_dynamic_cases_resolve_only_explicit_native_calls(self):
        source = '[ParamsSource(nameof(Cases))] public string Operation; Cases => new[] {"Sin", "Cos"}; "Sin" => Functions.Sin<double>();'
        self.assertEqual(['TaLib.Functions.Sin'], [r['pair'] for r in source_mappings(source, 'F', {'TaLib.Functions.Sin', 'TaLib.Functions.Cos'})])

    def test_batch_timing_is_not_an_individual_api(self):
        self.assertEqual([], source_mappings('public string Group; Functions.Sin<double>();', 'F', {'TaLib.Functions.Sin'}))

    def test_fixture_names_disambiguate_identical_operations(self):
        rows = [dict(fixture=f, operation='Wma', bars=1000, mode='BuilderFull') for f in ['Ta', 'Quan']]
        mapping = [dict(fixture=f, operation='Wma', pair=p) for f, p in [('Ta', 'TaLib.Functions.Wma'), ('Quan', 'QuanTAlib.Wma')]]
        matched, unmatched = reconcile(rows, mapping, {'TaLib.Functions.Wma', 'QuanTAlib.Wma'})
        self.assertEqual(2, len(matched))
        self.assertEqual([], unmatched)

    def test_overlapping_fixtures_are_not_cherry_picked(self):
        rows = [dict(fixture=f, operation='Library.Api', bars=1000, mode='BuilderFull') for f in ['A', 'B']]
        matched, unmatched = reconcile(rows, [], {'Library.Api'})
        self.assertEqual({}, matched)
        self.assertEqual(2, len(unmatched))
        self.assertTrue(all('Multiple fixtures' in r['mapping_status'] for r in unmatched))

    def test_extra_sizes_and_unknown_operations_remain_visible(self):
        rows = [dict(fixture='F', operation=op, bars=count, mode='BuilderFull') for op, count in [('Library.Api', 100000), ('Unknown', 1000)]]
        matched, unmatched = reconcile(rows, [], {'Library.Api'})
        self.assertEqual({}, matched)
        self.assertEqual(2, len(unmatched))

    def test_mapping_conflicts_fail_closed(self):
        with self.assertRaisesRegex(ValueError, 'Conflicting'):
            reconcile([], [dict(fixture='F', operation='Op', pair=p) for p in ['A', 'B']], {'A', 'B'})

    def test_short_alias_is_not_reused_for_another_snapshot(self):
        rows = [dict(fixture='F', operation='Wma', bars=1000, mode='BuilderFull', commit='new-snapshot')]
        mapping = [dict(fixture='F', operation='Wma', pair='TaLib.Functions.Wma')]
        matched, unmatched = reconcile(rows, mapping, {'TaLib.Functions.Wma'}, 'old-snapshot')
        self.assertEqual({}, matched)
        self.assertEqual(1, len(unmatched))
