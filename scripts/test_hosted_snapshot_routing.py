import unittest
from hosted_timing.pipeline import methods_for, OWNED


class SnapshotRoutingTests(unittest.TestCase):
    def test_snapshot_measures_both_owned_rounds_and_one_native_control_round(self):
        task = dict(methods=['Snapshot', 'NativeSingle', 'NativeParallel'])
        self.assertIn('Snapshot', OWNED)
        self.assertEqual([['Snapshot'], ['Snapshot'], task['methods'], ['Snapshot']],
                         [methods_for(task, i) for i in range(4)])

    def test_existing_builder_routing_is_unchanged(self):
        task = dict(methods=['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel'])
        self.assertEqual([task['methods'][:2],task['methods'][:2],task['methods'],task['methods'][:2]],
                         [methods_for(task, i) for i in range(4)])

    def test_existing_library_routing_is_unchanged(self):
        task = dict(methods=['Ooples','Competitor'])
        self.assertEqual([['Ooples'],['Ooples'],task['methods'],['Ooples']],
                         [methods_for(task, i) for i in range(4)])
