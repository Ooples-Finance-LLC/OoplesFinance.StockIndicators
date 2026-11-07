"""Keep the approved qualification exception confined to Harami's two batch methods."""
import copy
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch

from hosted_timing.checkpoints import run_checkpointed
from hosted_timing.pipeline import methods_for, warmups_for, warmup_context
from hosted_timing.verify import check_log


def policy():
    return dict(measurement_policy=dict(warmups=10, iterations=10, fixed_invocation_warmups=1000,
                fixed_invocation_pair_warmups={'TaLib.Candles.Harami': 20000}),
                qualification_warmup_approval=dict(approved=True,
                    proposal='harami-qualified-warmup-policy-proposal-20261107.json'))


def task(pair='TaLib.Candles.Harami', fixture='LibraryPairBenchmarks'):
    return dict(fixture=fixture, operations=[dict(raw=pair, pair=pair)], methods=['Ooples', 'Competitor'])


class HaramiQualificationWarmupTests(unittest.TestCase):
    def test_unaffected_multioperation_batches_keep_original_count(self):
        t = task('TaLib.Candles.Marubozu')
        t['operations'].append(dict(raw='TaLib.Candles.BeltHold', pair='TaLib.Candles.BeltHold'))
        for methods in (['Ooples', 'Competitor'], ['Ooples']):
            t['methods'] = methods
            self.assertEqual(1000, warmups_for(policy(), t['fixture'], t))
            self.assertEqual({'warmup_count':1000}, warmup_context(policy(), t['fixture'], t))

    def test_exception_and_all_other_fixture_counts(self):
        p = policy()
        for methods in (['Ooples', 'Competitor'], ['Competitor', 'Ooples']):
            t = task(); t['methods'] = methods
            self.assertEqual(20000, warmups_for(p, t['fixture'], t))
        self.assertEqual(1000, warmups_for(p, 'LibraryPairBenchmarks', task('TaLib.Candles.Marubozu')))
        for fixture in ('SharedBodyShadowBenchmarks', 'SharedContainmentCandleBenchmarks'):
            self.assertEqual(10, warmups_for(p, fixture, task(fixture=fixture)))

    def test_policy_cannot_expand_pair_or_count(self):
        for override in ({'TaLib.Candles.Harami': 20001}, {'TaLib.Candles.Marubozu': 20000},
                         {'TaLib.Candles.Harami': 20000, 'TaLib.Candles.Marubozu': 20000}):
            p = policy(); p['measurement_policy']['fixed_invocation_pair_warmups'] = override
            with self.assertRaises(AssertionError): warmups_for(p, 'LibraryPairBenchmarks', task())

    def test_explicit_approval_and_original_sampling_required(self):
        for field, value in (('approved', False), ('proposal', 'diagnostic-only')):
            p = policy(); p['qualification_warmup_approval'][field] = value
            with self.assertRaises(AssertionError): warmups_for(p, 'LibraryPairBenchmarks', task())
        for field, value in (('iterations', 9), ('warmups', 9), ('fixed_invocation_warmups', 10)):
            p = policy(); p['measurement_policy'][field] = value
            with self.assertRaises(AssertionError): warmups_for(p, 'LibraryPairBenchmarks', task())

    def test_missing_mixed_or_builder_batch_task_rejected(self):
        cases = [None, task(), task(), task(), task()]
        cases[1]['methods'] = ['Ooples']
        cases[2]['methods'] = ['BuilderFull', 'BuilderLatestOnly']
        cases[3]['operations'].append(dict(raw='TaLib.Candles.Marubozu', pair='TaLib.Candles.Marubozu'))
        cases[4]['operations'][0]['raw'] = 'different'
        for t in cases:
            with self.assertRaises(AssertionError): warmups_for(policy(), 'LibraryPairBenchmarks', t)

    def test_provenance_matches_selected_count(self):
        for t, count in ((task(), 20000), (task('TaLib.Candles.Marubozu'), 1000),
                         (task(fixture='SharedContainmentCandleBenchmarks'), 10)):
            self.assertEqual({'warmup_count': count}, warmup_context(policy(), t['fixture'], t))

    def test_complete_twenty_thousand_warmups_still_require_ten_measurements(self):
        log = '// Benchmark: x IterationCount=10 WarmupCount=20000\n' + ''.join(
            f'Workload{kind} {i}: x\n' for kind, count in (('Warmup', 20000), ('Actual', 10))
            for i in range(1, count + 1))
        check_log(log, 1, warmups=20000)
        for removed in ('WorkloadWarmup 20000: x\n', 'WorkloadActual 10: x\n'):
            with self.assertRaises(AssertionError): check_log(log.replace(removed, ''), 1, warmups=20000)

    def test_checkpoint_rejects_unapproved_execution_shapes(self):
        common = dict(out=None,index=0,arm='old',fixture='LibraryPairBenchmarks',
                      pairs=['TaLib.Candles.Harami'],methods=['Ooples','Competitor'],runtime=None,
                      env=None,state=None,save=None,builds=None,assert_binaries=None,
                      context={'warmup_count':20000},warmups=20000)
        for key, value in (('fixture','SharedContainmentCandleBenchmarks'),
                           ('pairs',['TaLib.Candles.Marubozu']),('methods',['BuilderFull','BuilderLatestOnly']),
                           ('methods',['Competitor']),('methods',['Ooples','Ooples']),
                           ('context',{'warmup_count':1000})):
            args = copy.deepcopy(common); args[key] = value
            with self.assertRaises(AssertionError): run_checkpointed(**args)

    def test_all_four_arms_forward_approved_warmups_and_ten_measurements(self):
        class StopBeforeExecution(Exception):
            pass
        t = task()
        with tempfile.TemporaryDirectory() as folder:
            for index, arm in enumerate(('old','new','new','old')):
                with patch('psutil.cpu_percent', return_value=0), patch(
                        'hosted_timing.checkpoints.subprocess.Popen', side_effect=StopBeforeExecution) as launch:
                    with self.assertRaises(StopBeforeExecution):
                        run_checkpointed(out=Path(folder),index=index,arm=arm,fixture=t['fixture'],
                            pairs=['TaLib.Candles.Harami'],methods=methods_for(t,index),runtime=Path(folder)/'runtime',
                            env={},state={},save=lambda:None,builds=lambda:[],assert_binaries=lambda:None,
                            context=warmup_context(policy(),t['fixture'],t),warmups=warmups_for(policy(),t['fixture'],t))
                    args = launch.call_args.args[0]
                    self.assertEqual('20000', args[args.index('--warmupCount')+1])
                    self.assertEqual('10', args[args.index('--iterationCount')+1])
                    self.assertEqual(index == 2, any('.Competitor*' in value for value in args))
