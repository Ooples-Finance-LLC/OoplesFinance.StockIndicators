"""Same-byte routing and diagnostic isolation retain the original trust gates."""
import copy
import json
import os
import runpy
from pathlib import Path
import shutil
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

import test_hosted_timing as base
from hosted_timing import aa_control as aa, pipeline as p, verify as v

PLAN = p.REPO/'benchmarks/plans/trady-macd-control-aa-20261105.json'
ORIGINAL = p.REPO/aa.ORIGINAL


class TradyMacdAATests(unittest.TestCase):
    def setUp(self):
        for module in (p,v):
            override=patch.object(module,'PLAN',ORIGINAL)
            override.start(); self.addCleanup(override.stop)

    def test_frozen_scope_preserves_full_original_identity(self):
        d=aa.validate_plan(PLAN)
        self.assertEqual((30,6),(d['cases'],d['comparisons']))
        self.assertEqual('old',d['profile_label'])
        self.assertEqual(3,len(d['sources'])-1)
        self.assertEqual(3,len(d['production_hashes']))

    def test_changed_sources_sampling_or_scope_are_rejected(self):
        d=p.read(PLAN)
        changes=[lambda x:x['sources'].update(successor=aa.SOURCE),
            lambda x:x['cpu_test_classes'].update(successor=['MacdSharedExecutionTests']),
            lambda x:x['gpu_sources'].update(successor=aa.SOURCE),
            lambda x:x['measurement_policy'].update(warmups=1),
            lambda x:x.update(profile_label='successor'),
            lambda x:x.update(order=['A','B']),
            lambda x:x['shards'][1]['tasks'][0].update(methods=['BuilderFull']),
            lambda x:x['matrix'].update(max_parallel=3)]
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'bad.json'
            for change in changes:
                broken=copy.deepcopy(d); change(broken); p.write(path,broken)
                with self.assertRaises(AssertionError): aa.validate_plan(path)

    def test_complete_original_verifier_is_mandatory(self):
        d=p.read(PLAN)
        proof=dict(sources=d['sources'],sdk=d['proposed_dotnet_sdk'],
            production_sha256=d['production_hashes'],
            **{k:d['original_input'][k] for k in ('run_id','run_attempt','workflow_head')})
        digest=p.digest
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            def hashed(path):
                path=Path(path)
                if path==root/'provenance.json': return d['original_input']['provenance_sha256']
                if path==root/'production/old.dll': return d['production_sha256']
                return digest(path)
            with patch.object(p,'digest',side_effect=hashed),patch.object(p,'verify_inputs',return_value=proof) as full:
                self.assertEqual(proof,aa.validate(PLAN,root)['_verified_inputs'])
                full.assert_called_once_with(root)
                broken=copy.deepcopy(proof); broken['sources']['successor']=aa.SOURCE
                full.return_value=broken
                with self.assertRaises(AssertionError): aa.validate(PLAN,root)

    def test_runtime_overrides_are_visible_and_rejected(self):
        with patch.dict(os.environ,{'DOTNET_TieredPGO':'0'}):
            self.assertEqual('0',aa.runtime_controls()['DOTNET_TieredPGO'])

    def test_cli_resolves_relative_paths_before_changing_runtime_directory(self):
        script=p.REPO/'scripts/run-trady-macd-aa-diagnostic.py'
        argv=[str(script),'measure','--inputs','relative-inputs','--out','relative-output','--shard','0']
        with patch.dict(os.environ),patch.object(sys,'argv',argv),patch.object(p,'measure') as measured,patch.object(aa,'runtime_controls',return_value={}):
            runpy.run_path(str(script),run_name='__main__')
        args,kwargs=measured.call_args
        self.assertEqual(Path('relative-inputs').resolve(),args[0])
        self.assertEqual(Path('relative-output').resolve(),args[1])
        self.assertTrue(kwargs['aa_plan'].is_absolute())
        self.assertEqual(('targeted',0),args[2:])

    def test_measure_copies_one_complete_profile_to_both_arms(self):
        original=p.read(ORIGINAL)
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); inputs=root/'inputs'
            (inputs/'common').mkdir(parents=True); (inputs/'evidence').mkdir()
            (inputs/'production').mkdir()
            (inputs/'common/harness.dll').write_bytes(b'common')
            for label in ('old','successor','variants'):
                (inputs/'production'/(label+'.dll')).write_bytes(label.encode())
            proof=dict(plan_sha256=p.digest(ORIGINAL),sources=original['sources'],sdk='10.0.403',
                common_manifest=p.manifest(inputs/'common'),evidence_manifest={},
                production_sha256={k:p.digest(inputs/'production'/(k+'.dll')) for k in ('old','successor','variants')})
            p.write(inputs/'provenance.json',proof)
            fake=types.SimpleNamespace(cpu_count=lambda:4,process_iter=lambda fields:[])
            def run(**kw):
                kw['assert_binaries']()
                self.assertEqual(b'old',(kw['runtime']/p.DLL).read_bytes())
                self.assertEqual(1000 if kw['fixture']=='LibraryPairBenchmarks' else 10,kw['warmups'])
                return dict(index=kw['index'],arm=kw['arm'],fixture=kw['fixture'],cases=2*len(kw['methods']))
            with patch.dict(sys.modules,{'psutil':fake}),patch.object(p.subprocess,'check_output',return_value='10.0.403'),patch.object(p,'git',return_value='diagnostic-workflow'),patch.object(aa,'runtime_controls',return_value={}),patch.object(aa,'validate',return_value={'shards':aa.shards()}),patch.object(p,'run_checkpointed',side_effect=run):
                for shard in (0,1):
                    out=root/f'raw{shard}'
                    p.measure(inputs,out,'targeted',shard,diagnostic='trady-macd-aa',aa_plan=PLAN)
                    actual=p.read(out/'provenance.json')
                    self.assertEqual(actual['binary_manifest']['old'],actual['binary_manifest']['new'])
                    self.assertEqual(dict(old=aa.SOURCE,new=aa.SOURCE),actual['sources'])
                    self.assertTrue(actual['diagnostic_only'])
                with patch.object(aa,'runtime_controls',return_value={'DOTNET_TieredPGO':'0'}):
                    with self.assertRaises(AssertionError):
                        p.measure(inputs,root/'bad-runtime','targeted',0,diagnostic='trady-macd-aa',aa_plan=PLAN)

    def evidence(self,root,new_mean=50):
        d=aa.validate_plan(PLAN)
        with patch.object(base,'PLAN',ORIGINAL),patch.object(base,'phase_shards',return_value=range(2)),patch.object(base,'phase_tasks',side_effect=lambda plan,phase,shard:aa.shards()[shard]['tasks']),patch.object(base,'phase_labels',return_value=('old','old')),patch.object(base,'phase_sources',return_value=dict(old=aa.SOURCE,new=aa.SOURCE)):
            base.make_evidence(root,'targeted',new_mean=new_mean)
        inputs=p.read(root/'input.json')
        inputs.update(production_sha256=d['production_hashes'],
            **{k:d['original_input'][k] for k in ('run_id','run_attempt','workflow_head')})
        p.write(root/'input.json',inputs)
        d['original_input']=dict(d['original_input'],provenance_sha256=p.digest(root/'input.json'))
        d['_verified_inputs']=inputs
        for shard in (0,1):
            folder=root/f'shard{shard}'; proof=p.read(folder/'provenance.json')
            manifest=dict(inputs['common_manifest']); manifest[p.DLL]=d['production_sha256']
            proof.update(input_provenance=inputs,input_provenance_sha256=p.digest(root/'input.json'),
                binary_manifest=dict(old=manifest,new=manifest),diagnostic='trady-macd-aa',diagnostic_only=True,
                diagnostic_environment={},runtime_controls={},diagnostic_plan_sha256=p.digest(PLAN),
                measured_profile_labels=dict(A='old',B='old'),measurement_workflow_head='diagnostic-workflow')
            p.write(folder/'provenance.json',proof)
            state=p.read(folder/'status.json')
            for record in state['rounds']:
                for position,chunk in enumerate(record['operation_checkpoints']):
                    chunk['execution_context']['production_sha256']=d['production_sha256']
                    target=folder/'operation-checkpoints'/record['fixture']/f"round{record['index']}-{record['arm']}"/f'operation{position}.json'
                    p.write(target,chunk)
            p.write(folder/'status.json',state)
        return d

    def collected(self,root,d):
        real_git=p.git
        def git(*args,**kwargs):
            return 'diagnostic-workflow' if args==('rev-parse','HEAD') else real_git(*args,**kwargs)
        with patch.object(aa,'validate',return_value=d),patch.object(p,'git',side_effect=git):
            return v.collect_aa(root,root,PLAN)

    def test_even_separated_identical_arms_never_accept_a_candidate(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); d=self.evidence(root)
            result=self.collected(root,d)
            self.assertEqual((30,6),(result['case_count'],result['comparison_count']))
            self.assertEqual({'B_below_A':6},result['separation_counts'])
            self.assertTrue(result['diagnostic_only'] and result['same_binary'])
            self.assertNotIn('accepted',result)
            self.assertNotIn('final_candidate_rows',result)
            with self.assertRaises(AssertionError): v.collect(root,'targeted')

    def test_missing_pair_or_mixed_binary_and_sources_rejected(self):
        changes=[lambda proof:proof['sources'].update(new=p.read(ORIGINAL)['sources']['successor']),
            lambda proof:proof['binary_manifest']['new'].update({p.DLL:'candidate'}),
            lambda proof:proof.update(diagnostic_only=False),
            lambda proof:proof.update(runtime_controls={'DOTNET_TieredPGO':'0'}),
            lambda proof:proof['measured_profile_labels'].update(B='successor'),
            lambda proof:proof['input_provenance']['sources'].update(successor=aa.SOURCE)]
        for change in changes:
            with tempfile.TemporaryDirectory() as tmp:
                root=Path(tmp); d=self.evidence(root); path=root/'shard1/provenance.json'
                proof=p.read(path); change(proof); p.write(path,proof)
                with self.assertRaises(AssertionError): self.collected(root,d)
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp); d=self.evidence(root); shutil.rmtree(root/'shard1')
            with self.assertRaises(AssertionError): self.collected(root,d)

    def test_changed_cpu_or_warmup_receipt_rejected(self):
        for mode in ('cpu','log'):
            with tempfile.TemporaryDirectory() as tmp:
                root=Path(tmp); d=self.evidence(root)
                state=p.read(root/'shard0/status.json'); chunk=state['rounds'][0]['operation_checkpoints'][0]
                if mode=='cpu':
                    chunk['quiet_cpu_samples'][0]=16
                    p.write(root/'shard0/status.json',state)
                else:
                    proof=p.read(root/'shard0/provenance.json')
                    log=v.archived(root/'shard0',proof['raw_root'],chunk['log'])
                    log.write_text(log.read_text().replace('WarmupCount=1000','WarmupCount=10'))
                with self.assertRaises(AssertionError): self.collected(root,d)
