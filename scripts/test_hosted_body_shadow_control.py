"""Route exact original profiles and reject altered or incomplete diagnostic evidence."""
import copy
import os
from pathlib import Path
import runpy
import sys
import tempfile
import types
import unittest
from unittest.mock import patch

import test_hosted_timing as base
from hosted_timing import aa_control as aa, pipeline as p, verify as v

PLAN=p.REPO/'benchmarks/plans/harami-body-shadow-control-20261107.json'
ORIGINAL=p.REPO/aa.ORIGINAL


class BodyShadowControlTests(unittest.TestCase):
    def setUp(self):
        for module in (p,v):
            item=patch.object(module,'PLAN',ORIGINAL);item.start();self.addCleanup(item.stop)

    def test_frozen_profiles_and_instrumentation(self):
        d=aa.validate_plan(PLAN)
        self.assertEqual([('old','old'),('successor','successor'),('old','successor')],
            [aa.profiles(d,i) for i in range(3)])
        self.assertEqual([{}, {}, d['instrumentation_only']],[aa.environment(d,i) for i in range(3)])
        self.assertEqual((30,6),(d['cases'],d['comparisons']))

    def test_changed_sources_sampling_scope_or_instrumentation_rejected(self):
        d=p.read(PLAN)
        changes=[lambda x:x['sources'].update(old='wrong'),
            lambda x:x['measurement_policy'].update(warmups=1),
            lambda x:x['jobs'][1]['label_profiles'].update(B='old'),
            lambda x:x['instrumentation_only'].update(DOTNET_TieredPGO='0'),
            lambda x:x['matrix'].update(max_parallel=4)]
        with tempfile.TemporaryDirectory() as tmp:
            path=Path(tmp)/'plan.json'
            for change in changes:
                broken=copy.deepcopy(d);change(broken);p.write(path,broken)
                with self.assertRaises(AssertionError): aa.validate_plan(path)

    def test_complete_original_verification_is_mandatory(self):
        d=p.read(PLAN)
        proof=dict(sources=d['sources'],sdk=d['proposed_dotnet_sdk'],production_sha256=d['production_hashes'],
            **{k:d['original_input'][k] for k in ('run_id','run_attempt','workflow_head')})
        digest=p.digest
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)
            def hashed(path):
                return d['original_input']['provenance_sha256'] if Path(path)==root/'provenance.json' else digest(path)
            with patch.object(p,'digest',side_effect=hashed),patch.object(p,'verify_inputs',return_value=proof) as full:
                self.assertEqual(proof,aa.validate(PLAN,root)['_verified_inputs'])
                full.assert_called_once_with(root)
                broken=copy.deepcopy(proof);broken['production_sha256']['successor']='wrong';full.return_value=broken
                with self.assertRaises(AssertionError): aa.validate(PLAN,root)

    def test_runtime_policy_and_trace_overrides_are_visible(self):
        for name in ('DOTNET_TieredPGO','COMPlus_ReadyToRun','DOTNET_JitDisasmAssemblies'):
            with patch.dict(os.environ,{name:'unexpected'}):
                self.assertEqual('unexpected',aa.runtime_controls()[name])

    def test_cli_resolves_all_paths_before_measurement(self):
        script=p.REPO/'scripts/run-body-shadow-control.py'
        argv=[str(script),'measure','--inputs','relative-inputs','--out','relative-output','--shard','2']
        with patch.dict(os.environ),patch.object(sys,'argv',argv),patch.object(p,'measure') as measured,patch.object(aa,'runtime_controls',return_value={}):
            runpy.run_path(str(script),run_name='__main__')
        args,kw=measured.call_args
        self.assertEqual(Path('relative-inputs').resolve(),args[0])
        self.assertEqual(Path('relative-output').resolve(),args[1])
        self.assertTrue(kw['aa_plan'].is_absolute())
        self.assertEqual(('targeted',2),args[2:])

    def test_measure_routes_every_job_to_complete_original_profile(self):
        d=aa.validate_plan(PLAN);original=p.read(ORIGINAL)
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);inputs=root/'inputs'
            for folder in ('common','evidence','production'): (inputs/folder).mkdir(parents=True)
            (inputs/'common/harness.dll').write_bytes(b'common')
            for label in ('old','successor','variants'): (inputs/'production'/(label+'.dll')).write_bytes(label.encode())
            proof=dict(plan_sha256=p.digest(ORIGINAL),sources=original['sources'],sdk='10.0.403',
                common_manifest=p.manifest(inputs/'common'),evidence_manifest={},
                production_sha256={k:p.digest(inputs/'production'/(k+'.dll')) for k in ('old','successor','variants')})
            p.write(inputs/'provenance.json',proof)
            fake=types.SimpleNamespace(cpu_count=lambda:4,process_iter=lambda fields:[])
            for shard in range(len(d['jobs'])):
                labels=aa.profiles(d,shard)
                def run(**kw):
                    kw['assert_binaries']()
                    label=labels[kw['arm']=='new']
                    self.assertEqual(label.encode(),(kw['runtime']/p.DLL).read_bytes())
                    self.assertEqual(d['measurement_policy']['fixed_invocation_warmups'] if kw['fixture']=='LibraryPairBenchmarks' else 10,kw['warmups'])
                    for key,value in aa.environment(d,shard).items(): self.assertEqual(value,kw['env'][key])
                    return dict(index=kw['index'],arm=kw['arm'],fixture=kw['fixture'],cases=2*len(kw['methods']))
                with patch.dict(sys.modules,{'psutil':fake}),patch.object(p.subprocess,'check_output',return_value='10.0.403'),patch.object(p,'git',return_value='workflow'),patch.object(aa,'runtime_controls',return_value={}),patch.object(aa,'validate',return_value=d),patch('hosted_timing.runtime_dependencies.verify_input_alignment') as alignment,patch.object(p,'run_checkpointed',side_effect=run):
                    out=root/f'raw{shard}'
                    p.measure(inputs,out,'targeted',shard,diagnostic='body-shadow-control',aa_plan=PLAN)
                    alignment.assert_called_once()
                actual=p.read(out/'provenance.json')
                self.assertEqual(dict(zip(('A','B'),labels)),actual['measured_profile_labels'])
                self.assertEqual(aa.environment(d,shard),actual['diagnostic_environment'])
                self.assertTrue(actual['diagnostic_only'])

    def evidence(self,root):
        d=aa.validate_plan(PLAN)
        with patch.object(base,'PLAN',ORIGINAL),patch.object(base,'phase_shards',return_value=range(len(d['jobs']))),patch.object(base,'phase_tasks',side_effect=lambda plan,phase,shard:d['shards'][shard]['tasks']):
            base.make_evidence(root,'targeted',new_mean=50)
        inputs=p.read(root/'input.json');inputs.update(production_sha256=d['production_hashes'],
            **{k:d['original_input'][k] for k in ('run_id','run_attempt','workflow_head')})
        p.write(root/'input.json',inputs)
        d['original_input']=dict(d['original_input'],provenance_sha256=p.digest(root/'input.json'));d['_verified_inputs']=inputs
        for shard in range(len(d['jobs'])):
            folder=root/f'shard{shard}';proof=p.read(folder/'provenance.json');labels=aa.profiles(d,shard)
            manifests={arm:{**inputs['common_manifest'],p.DLL:d['production_hashes'][label]} for arm,label in zip(('old','new'),labels)}
            proof.update(input_provenance=inputs,input_provenance_sha256=p.digest(root/'input.json'),
                sources=dict(zip(('old','new'),(d['sources'][label] for label in labels))),binary_manifest=manifests,
                diagnostic='body-shadow-control',diagnostic_only=True,diagnostic_environment=aa.environment(d,shard),
                runtime_controls={},diagnostic_plan_sha256=p.digest(PLAN),measured_profile_labels=dict(zip(('A','B'),labels)),measurement_workflow_head='workflow')
            p.write(folder/'provenance.json',proof)
            state=p.read(folder/'status.json')
            for record in state['rounds']:
                for position,chunk in enumerate(record['operation_checkpoints']):
                    chunk['execution_context']['production_sha256']=manifests[record['arm']][p.DLL]
                    path=folder/'operation-checkpoints'/record['fixture']/f"round{record['index']}-{record['arm']}"/f'operation{position}.json'
                    p.write(path,chunk)
            p.write(folder/'status.json',state)
        return d

    def collected(self,root,d,receipt=None):
        real_git=p.git
        def git(*args,**kw):return 'workflow' if args==('rev-parse','HEAD') else real_git(*args,**kw)
        with patch.object(aa,'validate',return_value=d),patch.object(aa,'collection_receipt',return_value=receipt),patch.object(p,'git',side_effect=git):
            return v.collect_aa(root,root,PLAN,Path('receipt.json') if receipt else None)

    def test_collection_receipt_is_immutable(self):
        path=p.REPO/'benchmarks/plans/body-shadow-collection-repair-20261107.json'
        d=aa.validate_plan(PLAN);receipt=aa.collection_receipt(path,d)
        self.assertEqual(4,len(receipt['benchmark_logger_logs']))
        with tempfile.TemporaryDirectory() as tmp:
            broken=p.read(path);broken['measurement_run']='another-run';target=Path(tmp)/'receipt.json';p.write(target,broken)
            with self.assertRaises(AssertionError):aa.collection_receipt(target,d)

    def test_interleaved_console_requires_hash_bound_complete_logger_iterations(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);d=self.evidence(root);folder=root/'shard2';state=p.read(folder/'status.json')
            receipt=dict(measurement_workflow_head='workflow',measurement_run='100',measurement_attempt='1',benchmark_logger_logs={},sha256='receipt')
            for record in state['rounds']:
                checkpoint=folder/'operation-checkpoints'/record['fixture']/f"round{record['index']}-{record['arm']}"
                raw=checkpoint/'operation0.log';own=checkpoint/'operation0'/'logger.log'
                original=raw.read_text();own.write_text(original)
                key='shard2/'+own.relative_to(folder).as_posix();receipt['benchmark_logger_logs'][key]=p.digest(own)
                raw.write_text(original.replace('WorkloadWarmup 1:', 'WorkloadWar; Assembly listing for method BodyShadowCandleState:Update (Tier1)\nmup 1:',1))
                (folder/f"round{record['index']}-{record['arm']}-{record['fixture']}.log").write_text(raw.read_text())
                chunk=record['operation_checkpoints'][0];chunk['log_sha256']=p.digest(raw);p.write(checkpoint/'operation0.json',chunk)
            p.write(folder/'status.json',state)
            with self.assertRaises(AssertionError):self.collected(root,d)
            result=self.collected(root,d,receipt)
            self.assertEqual((30,6),(result['case_count'],result['comparison_count']))
            own.write_text(original+'tampered')
            with self.assertRaises(AssertionError):self.collected(root,d,receipt)
            own.write_text(original.replace('WorkloadWarmup 1:', 'MissingWarmup 1:',1))
            receipt['benchmark_logger_logs'][key]=p.digest(own)
            with self.assertRaises(AssertionError):self.collected(root,d,receipt)

    def test_separated_controls_and_observations_never_qualify_source(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);d=self.evidence(root);result=self.collected(root,d)
            self.assertEqual((30,6),(result['case_count'],result['comparison_count']))
            self.assertEqual({0,1,2},{c['diagnostic_job'] for c in result['comparisons']})
            self.assertNotIn('accepted',result)
            for key in ('production_integrated','report_imported','policy_changed'):self.assertFalse(result[key])

    def test_missing_job_wrong_profile_or_trace_policy_rejected(self):
        changes=[lambda x:x['measured_profile_labels'].update(B='old'),
            lambda x:x['binary_manifest']['new'].update({p.DLL:'wrong'}),
            lambda x:x['diagnostic_environment'].update(DOTNET_TieredPGO='0')]
        for change in changes:
            with tempfile.TemporaryDirectory() as tmp:
                root=Path(tmp);d=self.evidence(root);path=root/'shard1/provenance.json';proof=p.read(path);change(proof);p.write(path,proof)
                with self.assertRaises(AssertionError):self.collected(root,d)
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);d=self.evidence(root);(root/'shard2/status.json').unlink()
            with self.assertRaises((AssertionError,FileNotFoundError)):self.collected(root,d)


if __name__=='__main__':unittest.main()
