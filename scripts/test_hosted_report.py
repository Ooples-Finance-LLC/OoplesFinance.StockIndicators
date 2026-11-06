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
from hosted_timing.report import stage
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

    def test_targeted_report_preserves_other_pairs_and_supports_subsequent_updates(self):
        self.check_report('targeted')

    def test_snapshot_report_keeps_builder_measurements_and_retains_snapshot_provenance(self):
        self.check_report('targeted',snapshot=True)

    def test_single_native_snapshot_preserves_complete_rows_without_parallel_invention(self):
        self.check_report('targeted',snapshot='single')

    def check_report(self,phase,snapshot=False):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); raw=root/'raw'; raw.mkdir()
            selected=PLAN
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
                write(selected,plan)
                for module in ('hosted_timing.pipeline','hosted_timing.verify','hosted_timing.report','test_hosted_timing'):
                    self.enterContext(patch(module+'.PLAN',selected))
                self.enterContext(patch('hosted_timing.targeted.verify_reference'))
            make_evidence(raw,phase)
            pairs={op['pair'] for shard in phase_shards(read(selected),phase) for t in phase_tasks(read(selected),phase,shard)
                   if t['fixture']=='LibraryPairBenchmarks' for op in t['operations']}
            data=dict(evidence=dict(sourceSha='old'),rows=[dict(pair=p,bars=n,source_sha='old',measurement_run_url='old-url',
                competitor_over_ooples=1,raw={}) for p in sorted(pairs|{'unchanged'}) for n in (1000,10000)])
            original=copy.deepcopy(data['rows'][-2:])
            # Explicit sentinel indexing avoids relying on lexical order.
            original=[copy.deepcopy(r) for r in data['rows'] if r['pair']=='unchanged']
            stats=dict(mean_ns=100,lower_ns=99,upper_ns=101,samples=10,allocated_bytes=8)
            untouched=dict(pair='unchanged-builder',bars=1000,mode='BuilderFull',ooples=stats,
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
                candidate=stage(live,raw/'..'/'raw',repo,root/'staged',phase=phase)
                if phase=='targeted':
                    refreshed=root/'refreshed.html';refreshed.write_bytes(candidate.read_bytes())
                    repeated=stage(refreshed,raw,repo,root/'repeated',phase=phase)
                    self.assertEqual(repeated.read_text(encoding='utf-8').count("r.measurement_machine ? esc"),1)
            result=candidate.read_text(encoding='utf-8')
            def embedded(key): return json.loads(re.search(r'<script[^>]*id="'+key+r'"[^>]*>(.*?)</script>',result,re.S)[1])
            after=embedded('dataset')
            self.assertEqual([r for r in after['rows'] if r['pair']=='unchanged'],original)
            changed=[r for r in after['rows'] if r['pair']!='unchanged']
            self.assertEqual(len(changed),2*len(pairs))
            self.assertGreater(len({r['measurement_shard'] for r in changed}),1 if phase=='successor' else 0)
            self.assertTrue(all(r['measurement_run_id']=='100' and r['measurement_host_nonce'] for r in changed))
            receipts=embedded('current-competitor-data')['receipts']
            self.assertEqual(receipts[0]['comparisons'],[untouched])
            self.assertTrue(all(c['measurement_machine']=='runner' for r in receipts[1:] for c in r['comparisons']))
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
            else:
                self.assertGreater(len({c['measurement_shard'] for r in receipts[1:] for c in r['comparisons']}),1)
            self.assertIn('D.rows.flatMap',result)
            self.assertIn('absolute timings are compared only within the same runner',result)
            self.assertIn('<section id="ema-constructor-results"><p>Independent constructor measurements</p></section>',result)
            self.assertNotIn('Previous summary',result)
            self.assertEqual(live.read_text(encoding='utf-8'),document)


if __name__=='__main__': unittest.main()
