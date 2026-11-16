"""Verify sharded report replacement preserves unrelated measured rows."""
from pathlib import Path
import copy
import gzip
import json
import re
import tempfile
import types
import unittest
from unittest.mock import patch

from test_hosted_timing import make_evidence
from hosted_timing.pipeline import PLAN, REPO, read, write, phase_shards, phase_tasks
from hosted_timing.report import stage, pair_warmup_disclosure
from hosted_timing.archive import archive


class HostedReportTests(unittest.TestCase):
    def test_rejected_evidence_is_archived_without_integration(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); raw=root/'raw'; raw.mkdir()
            make_evidence(raw,'variants',new_mean=150)
            prepared=root/'prepared'; prepared.mkdir()
            (prepared/'provenance.json').write_bytes((raw/'input.json').read_bytes())
            write(prepared/'evidence/check.json',{'historical':True})
            inputs=read(prepared/'provenance.json')
            with patch('hosted_timing.archive.verify_inputs',return_value=inputs), \
                 patch('hosted_timing.archive.subprocess.check_output',return_value=b'source patch'):
                destination=archive(raw,prepared,root/'repo','variants')
            decision=read(destination/'decision.json')
            self.assertFalse(decision['accepted'])
            self.assertFalse(decision['production_integrated'])
            self.assertFalse(decision['report_imported'])
            self.assertEqual(gzip.decompress((destination/'candidate-source.patch.gz').read_bytes()),b'source patch')
            archived_status=destination/'raw/shard0/status.json.gz'
            self.assertEqual(gzip.decompress(archived_status.read_bytes()),(raw/'shard0/status.json').read_bytes())

    def test_sharded_provenance_and_unchanged_rows(self):
        self.check_report('successor')

    def test_full_family_refresh_preserves_builders_and_identifies_identical_binaries(self):
        self.check_report('baseline-refresh')

    def test_refresh_requires_authenticated_inputs(self):
        with self.assertRaisesRegex(AssertionError,'authenticated canonical inputs'):
            stage('missing','missing','missing','missing',phase='baseline-refresh')

    def test_refresh_rejects_evidence_for_other_authenticated_inputs(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp);write(root/'provenance.json',{'identity':'authenticated'})
            summary=dict(baseline_refresh_verified=True,accepted=False,production_qualification=False,input_provenance_sha256='other')
            with patch('hosted_timing.pipeline.verify_inputs',return_value={}), \
                 patch('hosted_timing.pointwise_family_refresh.collect',return_value=summary), \
                 self.assertRaises(AssertionError):
                stage('missing','missing','missing','missing',phase='baseline-refresh',inputs=root)

    def test_source_update_still_requires_accepted_qualification(self):
        with patch('hosted_timing.report.collect',return_value={'accepted':False}), \
             self.assertRaisesRegex(AssertionError,'complete accepted successor'):
            stage('missing','missing','missing','missing',phase='targeted')

    def test_refresh_archive_never_claims_source_qualification(self):
        selected=REPO/'benchmarks/plans/hosted-pointwise-family-refresh-20261116.json'
        for module in ('hosted_timing.pipeline','hosted_timing.verify','hosted_timing.archive','test_hosted_timing'):
            self.enterContext(patch(module+'.PLAN',selected))
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); raw=root/'raw'; raw.mkdir()
            make_evidence(raw,'baseline-refresh',same_production=True)
            prepared=root/'prepared'; prepared.mkdir()
            (prepared/'provenance.json').write_bytes((raw/'input.json').read_bytes())
            write(prepared/'evidence/check.json',{'fresh_gpu':True})
            def source_diff(args,**kwargs):
                return b'' if args[-2:]==['src','tests'] else b'harness patch'
            with patch('hosted_timing.archive.verify_inputs',return_value=read(prepared/'provenance.json')), \
                 patch('hosted_timing.archive.subprocess.check_output',side_effect=source_diff):
                destination=archive(raw,prepared,root/'repo','baseline-refresh')
            decision=read(destination/'decision.json')
            self.assertEqual(decision['status'],'baseline-refresh-verified')
            self.assertEqual((decision['case_count'],decision['decision_count']),(190,38))
            for key in ('accepted','production_integrated','production_qualification','production_optimization'):
                self.assertFalse(decision[key])
            self.assertEqual(gzip.decompress((destination/'candidate-source.patch.gz').read_bytes()),b'')
            self.assertEqual(gzip.decompress((destination/'harness-source.patch.gz').read_bytes()),b'harness patch')

    def test_targeted_report_preserves_other_pairs_and_supports_subsequent_updates(self):
        self.check_report('targeted')

    def test_snapshot_report_keeps_builder_measurements_and_retains_snapshot_provenance(self):
        self.check_report('targeted',snapshot=True)

    def test_single_native_snapshot_preserves_complete_rows_without_parallel_invention(self):
        self.check_report('targeted',snapshot='single')

    def test_builder_only_control_retains_native_comparisons_and_archives_fresh_source_rows(self):
        self.check_report('targeted',builder_only=True)

    def test_pair_warmup_disclosure_names_actual_pairs(self):
        plan=read(REPO/'benchmarks/plans/hosted-abandoned-inventory-repair-20261114.json')
        disclosure=pair_warmup_disclosure(plan)
        for pair in plan['measurement_policy']['fixed_invocation_pair_warmups']:
            self.assertIn(pair,disclosure)
        self.assertIn('20,000 unmeasured warmups',disclosure)
        self.assertNotIn('Harami',disclosure)
        self.assertEqual(pair_warmup_disclosure(dict(measurement_policy={})), '')

    def test_new_plan_retains_earlier_row_warmup_disclosures(self):
        plan=dict(measurement_policy=dict(fixed_invocation_warmups=1000))
        pairs=['TaLib.Candles.Harami','TaLib.Candles.AbandonedBaby','TaLib.Functions.Add']
        rows=[dict(pair=pair,bars=n,warmup_count=20000) for pair in pairs for n in (1000,10000)]
        rows += [dict(pair='TaLib.Candles.Hammer',bars=n,warmup_count=1000) for n in (1000,10000)]
        disclosure=pair_warmup_disclosure(plan,rows)
        for pair in pairs:self.assertIn(pair,disclosure)
        self.assertNotIn('TaLib.Candles.Hammer',disclosure)
        self.assertIn('20,000 unmeasured warmups for both libraries at both bar counts',disclosure)

    def test_single_size_warmup_provenance_does_not_claim_both_sizes(self):
        disclosure=pair_warmup_disclosure(dict(measurement_policy={}),[
            dict(pair='TaLib.Functions.Add',bars=1000,warmup_count=20000),
            dict(pair='TaLib.Functions.Add',bars=10000,warmup_count=1000)])
        self.assertIn('TaLib.Functions.Add at 1,000 bars',disclosure)
        self.assertNotIn('at both bar counts',disclosure)

    def check_report(self,phase,snapshot=False,builder_only=False):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); raw=root/'raw'; raw.mkdir()
            selected=PLAN
            refreshing=phase=='baseline-refresh'
            if refreshing:
                selected=REPO/'benchmarks/plans/hosted-pointwise-family-refresh-20261116.json'
                for module in ('hosted_timing.pipeline','hosted_timing.verify','hosted_timing.report','test_hosted_timing'):
                    self.enterContext(patch(module+'.PLAN',selected))
            if phase=='targeted':
                selected=root/'plan.json';plan=read(REPO/'benchmarks/plans/hosted-hilbert-fir-20261102.json')
                if snapshot:
                    plan['targeted_shards']=[]
                    snapshot_methods=['Snapshot','NativeSingle'] if snapshot=='single' else ['Snapshot','NativeSingle','NativeParallel']
                    snapshot_pairs=('Skender.GetZigZag',) if snapshot=='single' else ('Skender.GetRenko','Skender.GetRenkoAtr')
                    snapshot_fixture='SharedSkenderZigZagChartNativeBenchmarks' if snapshot=='single' else 'SharedSkenderRenkoChartNativeBenchmarks'
                    for index,pair in enumerate(snapshot_pairs):
                        operation=[dict(raw=pair,pair=pair)]
                        plan['targeted_shards'].append(dict(index=index,tasks=[
                            dict(fixture='LibraryPairBenchmarks',operations=operation,methods=['Ooples','Competitor'],implicit_pair=False,cases=10,decisions=2),
                            dict(fixture=snapshot_fixture,operations=operation,methods=snapshot_methods,implicit_pair=False,
                                cases=10 if snapshot=='single' else 12,decisions=2)]))
                    plan['targeted_reference']['scope']='snapshot-test'
                    self.enterContext(patch.dict('hosted_timing.targeted.SCOPES',{'snapshot-test':dict(totals=(20,4) if snapshot=='single' else (44,8))}))
                if builder_only:
                    for shard in plan['targeted_shards']:
                        for task in shard['tasks']:
                            if 'BuilderFull' in task['methods']:
                                task['methods']=['BuilderFull','BuilderLatestOnly']
                    plan['targeted_reference']['scope']='builder-control-test'
                    # Ten batch pairs: 100 batch + 160 builder-only cases.
                    self.enterContext(patch.dict('hosted_timing.targeted.SCOPES',{'builder-control-test':dict(totals=(260,60))}))
                write(selected,plan)
                for module in ('hosted_timing.pipeline','hosted_timing.verify','hosted_timing.report','test_hosted_timing'):
                    self.enterContext(patch(module+'.PLAN',selected))
                self.enterContext(patch('hosted_timing.targeted.verify_reference'))
            make_evidence(raw,phase,same_production=refreshing)
            prepared=root/'prepared'; prepared.mkdir()
            (prepared/'provenance.json').write_bytes((raw/'input.json').read_bytes())
            inputs=read(prepared/'provenance.json')
            if refreshing:
                self.enterContext(patch('hosted_timing.pipeline.verify_inputs',return_value=inputs))
            pairs={op['pair'] for shard in phase_shards(read(selected),phase) for t in phase_tasks(read(selected),phase,shard)
                   if t['fixture']=='LibraryPairBenchmarks' for op in t['operations']}
            data=dict(evidence=dict(sourceSha='old'),rows=[dict(pair=p,bars=n,source_sha='old',measurement_run_url='old-url',
                competitor_over_ooples=1,raw={}) for p in sorted(pairs|{'unchanged'}) for n in (1000,10000)])
            for row in data['rows']:
                if row['pair']=='unchanged': row['warmup_count']=20000
            original=copy.deepcopy(data['rows'][-2:])
            # Explicit sentinel indexing avoids relying on lexical order.
            original=[copy.deepcopy(r) for r in data['rows'] if r['pair']=='unchanged']
            stats=dict(mean_ns=100,lower_ns=99,upper_ns=101,samples=10,allocated_bytes=8)
            untouched=dict(pair=sorted(pairs)[0] if builder_only else 'unchanged-builder',bars=1000,mode='BuilderFull',ooples=stats,
                native=dict(NativeSingle=stats,NativeParallel=stats),verdict='inconclusive')
            builder=dict(full_comparisons=1,receipts=[dict(run_id='old-run',source_sha='old',comparisons=[untouched])],
                         status=dict(completed=[]))
            status=dict(published_head='old',measurement_totals=dict(faster=0,slower=0,inconclusive=1,expected=1604,pending=1603),
                        builder_measurement_sources=[])
            document=''.join(f'<script id="{key}" type="application/json">{json.dumps(value)}</script>'
                             for key,value in [('dataset',data),('current-competitor-data',builder),('rollout-status-data',status)])
            document+='''<section id="explorer"><p class="sub">old</p></section>
<section id="rollout-status"><p class="sub">Rollout published <code>old</code>. Previous summary</p></section>
<section id="ema-constructor-results"><p>Independent constructor measurements</p></section>
<table id="builder-current-results"><tbody></tbody></table>
<script>
$('source').innerHTML='old';
const keys=Object.keys(D.rows[0]).filter(k=>k!=='raw');
const detail=`${esc(r.raw.Ooples.host.ProcessorName)}`;
</script>'''
            live=root/'live.html'; live.write_text(document,encoding='utf-8')
            repo=root/'repo'; write(repo/'benchmarks/results/remaining-indicators/inventory.json',{})
            primary=types.ModuleType('primary_baseline_report'); primary.replace_primary=lambda s,d:s
            with patch('hosted_timing.report.subprocess.check_output',return_value='head'), \
                 patch('hosted_timing.report.subprocess.run'),patch.dict('sys.modules',{'primary_baseline_report':primary}):
                # Equivalent paths must work even when archived() canonicalizes
                # short Windows names or parent-directory components.
                candidate=stage(live,raw/'..'/'raw',repo,root/'staged',phase=phase,inputs=prepared if refreshing else None)
                if phase=='targeted':
                    refreshed=root/'refreshed.html';refreshed.write_bytes(candidate.read_bytes())
                    repeated=stage(refreshed,raw,repo,root/'repeated',phase=phase)
                    self.assertEqual(repeated.read_text(encoding='utf-8').count("r.measurement_machine ? esc"),1)
            result=candidate.read_text(encoding='utf-8')
            self.assertNotIn(b'\r\r\n',candidate.read_bytes())
            def embedded(key): return json.loads(re.search(r'<script[^>]*id="'+key+r'"[^>]*>(.*?)</script>',result,re.S)[1])
            after=embedded('dataset')
            self.assertEqual([r for r in after['rows'] if r['pair']=='unchanged'],original)
            changed=[r for r in after['rows'] if r['pair']!='unchanged']
            if refreshing:
                self.assertEqual(len(changed),38)
                self.assertEqual(embedded('current-competitor-data')['receipts'],builder['receipts'])
                self.assertEqual(read(root/'staged/staged.json')['changed_builder_slots'],0)
                self.assertTrue(all(r['measurement_kind']=='harness-baseline-refresh' and not r['production_optimization'] for r in changed))
                self.assertTrue(all(r['same_binary_verdict'] in ('faster','slower','inconclusive') and len(r['native_round_classifications'])==4 for r in changed))
                self.assertTrue(all(r['benchmark_assembly_sha256'] and r['production_assembly_sha256'] and r['canonical_gpu_receipt_sha256'] for r in changed))
                self.assertEqual(after['evidence']['harnessBaselineRefreshes'][0]['cases'],190)
                self.assertIn('identical binaries',result)
                self.assertIn('not production algorithm speedups',result)
            self.assertEqual(len(changed),2*len(pairs))
            self.assertGreater(len({r['measurement_shard'] for r in changed}),1 if phase=='successor' else 0)
            self.assertTrue(all(r['measurement_run_id']=='100' and r['measurement_host_nonce'] for r in changed))
            receipts=embedded('current-competitor-data')['receipts']
            self.assertEqual(receipts[0]['comparisons'],[untouched])
            self.assertTrue(all(c['measurement_machine']=='runner' for r in receipts[1:] for c in r['comparisons']))
            if builder_only:
                self.assertEqual(receipts,[dict(run_id='old-run',source_sha='old',comparisons=[untouched])])
                controls=after['evidence']['builderControlMeasurements']
                self.assertEqual(sum(len(r['rows']) for r in controls),40)
                self.assertTrue(all(r['complete'] and r['comparison_kind']=='source-control-only' for r in controls))
                self.assertEqual({row['method'] for r in controls for row in r['rows']},{'BuilderFull','BuilderLatestOnly'})
                staged=read(root/'staged/staged.json')
                self.assertEqual(staged['changed_builder_slots'],0)
                self.assertEqual(staged['changed_builder_control_rows'],40)
                self.assertEqual(staged['builder_control_receipts'],controls)
                self.assertIn('contain no native timings',result)
            if snapshot:
                self.assertEqual(receipts,[dict(run_id='old-run',source_sha='old',comparisons=[untouched])])
                self.assertEqual(embedded('current-competitor-data')['status'],dict(completed=[]))
                self.assertEqual(embedded('rollout-status-data')['builder_measurement_sources'],[])
                snapshots=after['evidence']['snapshotMeasurements']
                self.assertEqual(len(snapshots),len(snapshot_pairs))
                self.assertTrue(all(r['complete'] and r['run_id']=='100' for r in snapshots))
                self.assertEqual({row['method'] for r in snapshots for row in r['rows']},set(snapshot_methods))
                self.assertEqual(len({r['measurement_shard'] for r in snapshots}),len(snapshot_pairs))
                for receipt in snapshots:
                    self.assertEqual({(row['bars'],row['method']) for row in receipt['rows']},
                        {(n,m) for n in (1000,10000) for m in snapshot_methods})
                self.assertEqual(read(root/'staged/staged.json')['changed_snapshot_rows'],2*len(snapshot_pairs))
            elif not builder_only and not refreshing:
                self.assertGreater(len({c['measurement_shard'] for r in receipts[1:] for c in r['comparisons']}),1)
            self.assertIn('D.rows.flatMap',result)
            self.assertIn('absolute timings are compared only within the same runner',result)
            self.assertIn(('unchanged use the approved' if refreshing else 'Batch timings for unchanged use the approved')+' 20,000 unmeasured warmups',result)
            self.assertIn('<section id="ema-constructor-results"><p>Independent constructor measurements</p></section>',result)
            self.assertNotIn('Previous summary',result)
            self.assertEqual(live.read_text(encoding='utf-8'),document)


if __name__=='__main__': unittest.main()
