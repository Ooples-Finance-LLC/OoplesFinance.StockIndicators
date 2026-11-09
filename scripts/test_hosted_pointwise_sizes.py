import fnmatch
import unittest

from hosted_timing.checkpoints import combine_reports, filters


class PointwiseSizeSelectionTests(unittest.TestCase):
    def test_selects_only_approved_sizes_and_operation_in_both_parameter_orders(self):
        methods = ['BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel']
        patterns = filters('SharedPointwiseBenchmarks', 'Exp', methods)
        for method in methods:
            for count in (1000, 10000, 100000):
                for operation in ('Exp', 'Exp2', 'Ln'):
                    for parameters in (
                        f'Operation: "{operation}", Count: {count}',
                        f'Count: {count}, Operation: "{operation}"'):
                        name = f'Example.SharedPointwiseBenchmarks.{method}({parameters})'
                        with self.subTest(name=name):
                            self.assertEqual(operation == 'Exp' and count in (1000, 10000),
                                             any(fnmatch.fnmatchcase(name, pattern) for pattern in patterns))

    def test_collector_still_rejects_an_unapproved_third_size(self):
        methods = ['BuilderFull', 'NativeSingle']
        def case(count, method):
            return dict(FullName=f'Example.SharedPointwiseBenchmarks.{method}(Operation: "Exp", Count: {count})',
                        Method=method, Statistics=dict(N=10))
        cases = [case(count, method) for count in (1000, 10000) for method in methods]
        self.assertEqual(4, len(combine_reports([dict(Benchmarks=cases)], ['Exp'], methods)['Benchmarks']))
        with self.assertRaises(AssertionError):
            combine_reports([dict(Benchmarks=cases + [case(100000, 'BuilderFull')])], ['Exp'], methods)


if __name__ == '__main__':
    unittest.main()
