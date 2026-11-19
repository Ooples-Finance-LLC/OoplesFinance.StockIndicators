import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from hosted_timing import baseline_refresh_dispatch, pipeline as p, current_frontier_refresh as q

PLAN=p.REPO/'benchmarks/plans/hosted-current-frontier-refresh-20261119.json'


def decisions():
    return [dict(fixture='LibraryPairBenchmarks',pair=pair,bars=size,method='Ooples',verdict='inconclusive')
            for pair in q.PAIRS for size in (1000,10000)]


def native_rows(mean=8,lower=7,upper=9):
    rows=[]
    for pair in q.PAIRS:
        for size in (1000,10000):
            samples=[dict(round=i,mean_ns=mean,lower_ns=lower,upper_ns=upper,samples=10,allocated_bytes=8) for i in range(4)]
            control=dict(round=2,mean_ns=10,lower_ns=9.5,upper_ns=10.5,samples=10,allocated_bytes=8)
            directions=['win' if sample['upper_ns']<control['lower_ns'] else 'loss' if sample['lower_ns']>control['upper_ns'] else 'inconclusive' for sample in samples]
            rows.append(dict(pair=pair,bars=size,owned_rounds=samples,native_control=control,round_classifications=directions,
                all_four_separated_native_wins=all(value=='win' for value in directions)))
    return dict(rows=rows)


class CurrentFrontierRefreshTests(unittest.TestCase):
    def plan(self,ready=False):
        plan=p.read(PLAN)
        if ready:plan.update(inputs_ready=True,parent_review_approved=True)
        return plan

    def test_frozen_exact_union_and_both_size_case_inventory(self):
        plan=self.plan();q.validate_scope(plan,require_ready=False)
        self.assertEqual(154,len(q.PAIRS));self.assertEqual(154,len(set(q.PAIRS)))
        inventory=q.case_inventory()
        self.assertEqual(1540,len(inventory));self.assertEqual(1540,len({tuple(row.values()) for row in inventory}))
        self.assertEqual(308,sum(row['method']=='Competitor' for row in inventory))
        self.assertEqual({2},{row['round'] for row in inventory if row['method']=='Competitor'})
        self.assertEqual({1000,10000},{row['bars'] for row in inventory})
        selected=[operation['pair'] for shard in plan['refresh_shards'] for operation in shard['tasks'][0]['operations']]
        self.assertEqual(set(q.PAIRS),set(selected));self.assertEqual(154,len(selected))
        self.assertLessEqual(max(len(shard['tasks'][0]['operations']) for shard in plan['refresh_shards']),13)
        self.assertEqual(q.scope()['priority_first_shard'],[o['pair'] for o in plan['refresh_shards'][0]['tasks'][0]['operations']])

    def test_both_ready_flags_hold_before_any_runtime_or_input_execution(self):
        for first,second in [(False,False),(True,False),(False,True)]:
            plan=self.plan();plan.update(inputs_ready=first,parent_review_approved=second)
            with self.subTest(flags=(first,second)),patch.object(p,'read',return_value=plan),patch.object(p,'git',return_value=''),patch.object(q,'checked_inputs') as inputs,patch.object(q.subprocess,'check_output') as command:
                with self.assertRaisesRegex(AssertionError,'HELD'):q.prepare('candidate','out')
                inputs.assert_not_called();command.assert_not_called()

    def test_ready_scope_and_refresh_dispatch(self):
        plan=self.plan(ready=True);q.validate_scope(plan)
        self.assertIs(q,baseline_refresh_dispatch.implementation(plan))
        self.assertEqual(list(range(12)),list(p.phase_shards(plan,'baseline-refresh')))

    def test_scope_mutations_fail_closed(self):
        for key in ('source','pair','duplicate','size','warmup','iterations','runners','cap','cpu','gpu','core','harness','qualification','credit','report','scope','candidate','mixed_scope','report_source','timestamp_precedence'):
            plan=self.plan(ready=True)
            if key=='source':plan['sources']['successor']='0'*40
            elif key=='pair':plan['affected_pairs'].pop()
            elif key=='duplicate':plan['refresh_shards'][1]['tasks'][0]['operations'][0]=plan['refresh_shards'][0]['tasks'][0]['operations'][0]
            elif key=='size':plan['requested_case_inventory'][0]['bars']=9999
            elif key=='warmup':plan['measurement_policy']['fixed_invocation_warmups']=10
            elif key=='iterations':plan['measurement_policy']['iterations']=9
            elif key=='runners':plan['matrix']['max_parallel']=2
            elif key=='cap':plan['matrix']['timeout_minutes_per_shard']=120
            elif key=='cpu':plan['measurement_policy']['max_cpu_percent']=16
            elif key=='gpu':plan['gpu_sources']['old']=q.PUBLISHED
            elif key=='core':plan['candidate']['production_sha256']='0'*64
            elif key=='harness':plan['harness_sha256']='0'*64
            elif key=='qualification':plan['production_qualification']=True
            elif key=='credit':plan['algorithm_credit']=1
            elif key=='report':plan['report_contract']['whole_pair_quarantine']=False
            elif key=='scope':plan['scope_approval']['sha256']='0'*64
            elif key=='candidate':plan['candidate']['ci_run']='1'
            elif key=='mixed_scope':plan['current_baseline_ten_refresh']=True
            elif key=='report_source':plan['report_before_sha256']='f'*64
            else:plan['composition_policy']['precedence']='latest-wall-clock-timestamp'
            with self.subTest(key=key),self.assertRaises(AssertionError):q.validate_scope(plan)

    def test_runtime_identity_includes_all_dependencies_and_actual_core_harness(self):
        plan=self.plan(ready=True)
        manifest={p.DLL:q.CORE_SHA,q.ASSEMBLY:q.HARNESS_SHA,'tensor.dll':'a'*64,'runtimeconfig.json':'b'*64}
        q.verify_runtime_identity(plan,dict(old=manifest,new=dict(manifest)))
        for name in manifest:
            changed=dict(manifest);changed[name]='f'*64
            with self.subTest(name=name),self.assertRaises(AssertionError):q.verify_runtime_identity(plan,dict(old=manifest,new=changed))
        changed=dict(manifest);changed[p.DLL]='f'*64
        with self.assertRaises(AssertionError):q.verify_runtime_identity(plan,dict(old=changed,new=dict(changed)))

    def test_metadata_catalog_requires_exact401_accepted_pair_ids(self):
        ids=[row['pair'] for row in q.frozen_catalog()['rows']]
        metadata=[dict(Id=pair,Status='paired') for pair in ids]+[dict(Id='excluded',Status='unavailable')]
        self.assertEqual(sorted(ids),q.primary_pair_inventory(json.dumps(metadata).encode()))
        for changed in [metadata[:-2],metadata+[metadata[0]],metadata+[dict(Id='new-pair',Status='paired')]]:
            with self.assertRaises(AssertionError):q.primary_pair_inventory(json.dumps(changed).encode())

    def test_snapshot_validation_does_not_hold_a_new_publication(self):
        plan=self.plan(ready=True)
        with patch.object(p,'git',return_value='') as git:
            self.assertEqual(plan['published_snapshot'],q.verify_snapshot(plan))
        self.assertTrue(all(call.args[0]=='diff' for call in git.call_args_list))

    def test_frozen_report_hash_and_source_lineage_override_later_clock_time(self):
        plan=self.plan(ready=True)
        self.assertEqual(q.REPORT_SHA,plan['report_before_sha256'])
        self.assertEqual('accepted-source-lineage-and-qualified-scope',plan['composition_policy']['precedence'])
        self.assertTrue(plan['composition_policy']['wall_clock_timestamp_is_not_source_precedence'])
        self.assertTrue(plan['composition_policy']['later_accepted_overrides_must_be_preserved_or_reapplied'])
        self.assertFalse(plan['composition_policy']['automatic_import'])

    def test_whole_pair_quarantine_requires_inconclusive_at_both_sizes(self):
        values=decisions();values[0]['verdict']='faster'
        admitted=q.admission(values)
        self.assertEqual(q.PAIRS[1:],admitted['admitted_pairs'])
        self.assertEqual([dict(pair=q.PAIRS[0],sizes={'1000':'faster','10000':'inconclusive'})],admitted['quarantined_pairs'])
        self.assertEqual(306,admitted['admitted_baseline_rows']);self.assertEqual(496,admitted['unchanged_baseline_rows'])
        self.assertEqual(1544,admitted['unchanged_builder_comparisons']);self.assertTrue(admitted['admission_is_not_equivalence'])

    def test_zero_admission_never_creates_equivalence_or_credit(self):
        values=decisions()
        for value in values:value['verdict']='slower'
        result=q.admission(values)
        self.assertEqual([],result['admitted_pairs']);self.assertTrue(result['zero_admitted_no_publication'])
        self.assertEqual(802,result['unchanged_baseline_rows'])

    def test_admission_rejects_missing_or_duplicate_tuples(self):
        values=decisions()
        with self.assertRaises(AssertionError):q.admission(values[:-1])
        values[-1]=values[0]
        with self.assertRaises(AssertionError):q.admission(values)

    def test_statistical_consensus_remains_distinct_from_point_wins_and_B_rule(self):
        data=native_rows()
        target=data['rows'][0]
        target['owned_rounds'][0].update(lower_ns=7,upper_ns=11)
        target['round_classifications'][0]='inconclusive'
        result=q.classify_native(data,q.PAIRS)
        row=result['rows'][0]
        self.assertEqual('label_consistent_point_winner_without_both_label_native_CI_consensus',row['category'])
        self.assertFalse(row['A_both_native_CI_wins']);self.assertTrue(row['B_approved_both_native_CI_wins'])
        self.assertFalse(row['all_four_separated_native_wins'])
        self.assertFalse(result['current_full_401_inventory']);self.assertEqual(247,result['counts_by_size']['10000']['unmeasured_pairs'])
        self.assertEqual(0,result['algorithm_credit']);self.assertEqual(0,result['closure_credit'])

    def test_strict_native_CI_tie_and_mixed_points_cannot_become_consensus_wins(self):
        data=native_rows();target=data['rows'][0]
        target['owned_rounds'][1]['upper_ns']=target['native_control']['lower_ns']
        target['round_classifications'][1]='inconclusive'
        target['owned_rounds'][3].update(mean_ns=11,lower_ns=10,upper_ns=12)
        target['round_classifications'][3]='inconclusive'
        row=q.classify_native(data,q.PAIRS)['rows'][0]
        self.assertFalse(row['B_approved_both_native_CI_wins']);self.assertEqual('mixed_native_point_position',row['category'])

    def test_quarantine_overrides_native_winner_category_at_both_sizes(self):
        result=q.classify_native(native_rows(),q.PAIRS[1:])
        rows=[row for row in result['rows'] if row['pair']==q.PAIRS[0]]
        self.assertEqual(2,len(rows));self.assertTrue(all(row['category']=='quarantined_same_binary_label_effect' for row in rows))
        self.assertTrue(all(row['B_approved_both_native_CI_wins'] for row in rows))
        self.assertEqual(1,result['counts_by_size']['10000']['quarantined_pairs'])
        self.assertEqual(248,result['counts_by_size']['10000']['possible_total_point_nonwinners_upper'])

    def test_inconsistent_native_direction_metadata_is_rejected(self):
        data=native_rows();data['rows'][0]['round_classifications'][1]='loss'
        with self.assertRaises(AssertionError):q.classify_native(data,q.PAIRS)

    def test_collect_predeclares_verified_refresh_only_and_zero_algorithm_credit(self):
        synthetic=dict(accepted=False,case_count=1540,decision_count=308,decisions=decisions())
        with patch('hosted_timing.verify._collect',return_value=synthetic),patch('hosted_timing.current_baseline_ten_refresh._verify_runtime') as runtime, \
             patch('hosted_timing.pointwise_family_refresh.native_review',return_value=native_rows()),patch.object(p,'PLAN',PLAN):
            result=q.collect(Path('raw'))
        runtime.assert_called_once()
        self.assertTrue(result['baseline_refresh_verified']);self.assertTrue(result['report_refresh_only'])
        self.assertFalse(result['accepted']);self.assertFalse(result['production_qualification'])
        self.assertFalse(result['automatic_import']);self.assertFalse(result['report_imported'])
        self.assertEqual(0,result['algorithm_credit']);self.assertEqual(0,result['closure_credit'])

    def test_collection_requires_all_frozen_cases_and_decisions(self):
        synthetic=dict(accepted=False,case_count=1530,decision_count=306,decisions=decisions())
        with patch('hosted_timing.verify._collect',return_value=synthetic),self.assertRaises(AssertionError):q.collect(Path('raw'))

    def test_workflow_is_push_only_held_priority_then_serial_bounded_rest(self):
        text=(p.REPO/'.github/workflows/current-frontier-refresh.yml').read_text()
        self.assertNotIn('workflow_dispatch:',text)
        self.assertLess(text.index('HELD before SDK/input I/O'),text.index('actions/setup-dotnet'))
        self.assertIn('needs: measure-priority',text);self.assertIn('max-parallel: 1',text)
        self.assertIn('shard: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]',text)
        self.assertIn('fail-fast: true',text);self.assertEqual(4,text.count('timeout-minutes: 60'))
        self.assertNotIn('--phase diagnostic',text);self.assertNotIn('verify_current_publication',text)
        self.assertNotIn('dotnet build',text);self.assertNotIn('dotnet test',text)

    def test_effective_stopping_contract_is_honest_about_waiting_and_contamination(self):
        approved=q.scope()
        self.assertIn('No promised inner quiet deadline',approved['quiet_limit'])
        self.assertIn('discard/archive/reacquire',approved['contamination'])
        self.assertIn('No ad-hoc completed-result retries',approved['contamination'])
        self.assertEqual(247,approved['remaining_scope']['unmeasured_pairs'])
        self.assertFalse(approved['remaining_scope']['all401_launch'])


if __name__=='__main__':unittest.main()
