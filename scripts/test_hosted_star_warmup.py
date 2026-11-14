import fnmatch
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from hosted_timing import star_warmup as a, checkpoints as c, pipeline as p


class StarWarmupTests(unittest.TestCase):
    def test_frozen_scope(self):
        self.assertEqual(a.SHA,p.digest(a.PLAN))
        d=p.read(a.PLAN)
        self.assertEqual((20,5,2),(d['cases'],d['comparisons'],d['max_parallel']))
        self.assertEqual({'A':1000,'B':20000},d['warmups'])
        self.assertEqual([1000],d['bars'])
        self.assertEqual({},d['runtime_policy_overrides'])
        self.assertEqual('0.150.0',d['tensor_version'])

    def test_filters_both_parameter_orders_and_no_10k(self):
        patterns=c.filters('LibraryPairBenchmarks','TaLib.Candles.MorningStar',['Ooples'],bars=(1000,))
        for params in ('Bars: 1000, PairId: "TaLib.Candles.MorningStar"','PairId: "TaLib.Candles.MorningStar", Bars: 1000'):
            name='LibraryPairBenchmarks.Ooples('+params+')'
            self.assertTrue(any(fnmatch.fnmatchcase(name,f) for f in patterns))
            self.assertFalse(any(fnmatch.fnmatchcase(name.replace('1000','10000'),f) for f in patterns))
        self.assertEqual(['*LibraryPairBenchmarks.Ooples*"x"*'],c.filters('LibraryPairBenchmarks','x',['Ooples']))

    def test_plain_json_cannot_enable_diagnostic_runner(self):
        with self.assertRaises(AssertionError):
            c.run_checkpointed(out=None,index=0,arm='A',fixture='LibraryPairBenchmarks',pairs=['TaLib.Candles.EveningStar'],
                methods=['Ooples'],runtime=None,env={},state={},save=None,builds=None,assert_binaries=None,
                bars=(1000,),warmups=20000,context={'warmup_count':20000})

    def test_qualification_warmups_not_extended(self):
        plan={'measurement_policy':{'warmups':10,'iterations':10,'fixed_invocation_warmups':20000}}
        with self.assertRaises(AssertionError): p.warmups_for(plan,'LibraryPairBenchmarks')

    def fixture(self, folder):
        d=p.read(a.PLAN)
        inputs=dict(common_manifest={'harness.dll':'hash'},production_sha256={'successor':'prod'})
        for shard in range(2):
            root=folder/f'shard{shard}'
            host=dict(machine='vm',runner_image='test',run_id='1',run_attempt='1',instance_nonce=str(shard))
            proof=dict(shard=shard,diagnostic_only=True,diagnostic='star-warmup',diagnostic_plan_sha256=a.SHA,runtime_controls={},
                input_provenance=inputs,binary_manifest=dict(inputs['common_manifest'],**{p.DLL:'prod'}),
                host=host,workflow_head='head',raw_root=str(root))
            p.write(root/'provenance.json',proof)
            state=dict(shard=shard,status='raw-measurements-complete',order=d['order'],rounds=[])
            for index,label in enumerate(d['order']):
                chunks=[]; originals=[]; logs=[]
                for position,pair in enumerate(d['jobs'][shard]):
                    checkpoint=root/'operation-checkpoints/LibraryPairBenchmarks'/f'round{index}-{label}'
                    report_path=checkpoint/f'operation{position}/results/result.json'
                    case=dict(FullName=f'LibraryPairBenchmarks.Ooples(Bars: 1000, PairId: "{pair}")',Method='Ooples',
                        Statistics=dict(N=10,OriginalValues=[1.0]*10,Mean=1.0,ConfidenceInterval={'Lower':0.9,'Upper':1.1}),
                        Memory={'BytesAllocatedPerOperation':0})
                    report={'Benchmarks':[case]}; p.write(report_path,report)
                    warmups=d['warmups'][label]
                    log=f'// Benchmark: LibraryPairBenchmarks.Ooples IterationCount=10 WarmupCount={warmups}\n'
                    log+=''.join(f'WorkloadWarmup {i}: 1 op\n' for i in range(1,warmups+1))
                    log+=''.join(f'WorkloadActual {i}: 1 op\n' for i in range(1,11))
                    log_path=checkpoint/f'operation{position}.log'; log_path.write_text(log)
                    context=dict(diagnostic_plan_sha256=a.SHA,warmup_count=warmups,pairs=d['jobs'][shard],
                        host_nonce=str(shard),production_sha256='prod')
                    chunk=dict(pair=pair,execution_context=context,quiet_cpu_samples=[1]*12,quiet_build_free_samples=12,
                        build_monitor_observations=1,quiet_sequence_started_utc='now',cases=1,
                        report=str(report_path),sha256=p.digest(report_path),log=str(log_path),log_sha256=p.digest(log_path))
                    p.write(checkpoint/f'operation{position}.json',chunk)
                    chunks.append(chunk); originals.append(report); logs.append(log)
                merged=root/f'round{index}-{label}-LibraryPairBenchmarks/results/merged.json'
                p.write(merged,c.combine_reports(originals,d['jobs'][shard],['Ooples'],bars=(1000,)))
                (root/f'round{index}-{label}-LibraryPairBenchmarks.log').write_text('\n'.join(logs))
                state['rounds'].append(dict(index=index,arm=label,fixture='LibraryPairBenchmarks',operation_checkpoints=chunks,
                    report=str(merged),sha256=p.digest(merged),cases=len(d['jobs'][shard]),build_monitor_observations=len(d['jobs'][shard])))
            p.write(root/'status.json',state)
        return d, inputs

    def test_complete_collection_and_tamper_rejection(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp); d,inputs=self.fixture(root)
            with patch.object(a,'validate',return_value=(d,inputs)):
                result=a.collect(root,root)
                self.assertEqual((20,5),(result['case_count'],result['comparison_count']))
                self.assertTrue(result['same_binary'])
                self.assertTrue(all(v['separation']=='overlap' for v in result['comparisons']))
                self.assertNotIn('accepted',result)
                self.assertFalse(any(result[k] for k in ('production_integrated','report_imported','policy_changed')))
                target=next(root.rglob('operation0.log')); target.write_text('tampered')
                with self.assertRaises(AssertionError): a.collect(root,root)

    def test_missing_shard_rejected(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            with patch.object(a,'validate',return_value=(p.read(a.PLAN),{})):
                with self.assertRaises(AssertionError): a.collect(root,root)


if __name__=='__main__': unittest.main()
