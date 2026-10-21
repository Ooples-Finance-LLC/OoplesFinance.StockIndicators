import unittest
from performance_pair_mapping import reconcile, source_mappings


class PairMappingTests(unittest.TestCase):
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
