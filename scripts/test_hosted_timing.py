"""Exercise complete portable evidence, then corrupt individual trust boundaries."""
from pathlib import Path, PureWindowsPath
import copy
import json
import os
import subprocess
import sys
import tempfile
import unittest

from hosted_timing.pipeline import PLAN, DLL, read, write, digest, phase_tasks, methods_for, phase_sources, original_gpu_source, trix_diagnostic_tasks
from hosted_timing.checkpoints import case_key, combine_reports
from hosted_timing.verify import collect, archived, check_log, sample


def make_evidence(folder, phase, new_mean=50):
    plan = read(PLAN)
    inputs = dict(sources=plan['sources'], plan_sha256=digest(PLAN), sdk=plan['proposed_dotnet_sdk'],
        common_manifest={DLL: 'variant', 'harness.dll': 'common'},
        production_sha256=dict(old='old', successor='successor', variants='variant'),
        run_id='100', run_attempt='1', workflow_head='workflow')
    input_path = folder/'input.json'
    write(input_path, inputs)
    for shard in range(8 if phase == 'successor' else 1):
        root = folder/f'shard{shard}'
        raw_root = PureWindowsPath('D:/runner/raw')
        host = dict(run_id='100', run_attempt='1', machine='runner', runner_image='20261101', instance_nonce=f'vm{shard}')
        labels = ('old', 'successor') if phase == 'successor' else ('successor', 'variants')
        manifests = {arm: {**inputs['common_manifest'], DLL: inputs['production_sha256'][label]}
                     for arm, label in zip(('old', 'new'), labels)}
        proof = dict(shard=shard, phase=phase, raw_root=str(raw_root), host=host, sources=phase_sources(plan, phase),
            plan_sha256=digest(PLAN), input_provenance=inputs, input_provenance_sha256=digest(input_path), binary_manifest=manifests)
        write(root/'provenance.json', proof)
        state = dict(status='raw-measurements-complete', phase=phase, shard=shard, order=['old','new','new','old'], rounds=[])
        for index, arm in enumerate(state['order']):
            for task in phase_tasks(plan, phase, shard):
                fixture = task['fixture']
                methods = methods_for(task, index)
                receipts, reports, logs = [], [], []
                for position, operation in enumerate(task['operations']):
                    pair = operation['raw']
                    cases = []
                    for bars in (1000, 10000):
                        for method in methods:
                            parameter = f'Method: {pair}' if task.get('variant') else f'Operation: "{pair}"'
                            name = f'{fixture}.{method}(Count: {bars}, {parameter})'
                            mean = 100 if arm == 'old' else new_mean
                            cases.append(dict(FullName=name, Method=method, Statistics=dict(N=10, OriginalValues=[mean]*10,
                                Mean=mean, StandardDeviation=0, ConfidenceInterval=dict(Lower=mean-1,Upper=mean+1)), Memory=dict(BytesAllocatedPerOperation=8)))
                    report = dict(Benchmarks=cases, HostEnvironmentInfo=dict(ProcessorName='test CPU', RuntimeVersion='test runtime'))
                    log = ''.join('// Benchmark: test: IterationCount=10 WarmupCount=10\n' +
                        ''.join(f'Workload{kind} {i}: sample\n' for kind in ('Warmup','Actual') for i in range(1,11)) for _ in cases)
                    relative = Path('operation-checkpoints')/fixture/f'round{index}-{arm}'/f'operation{position}'
                    report_path = root/relative/'results'/'report.json'
                    log_path = (root/relative).with_suffix('.log')
                    write(report_path, report)
                    log_path.write_text(log, encoding='utf-8')
                    receipt = dict(pair=pair, report=str(raw_root/str(relative/'results'/'report.json')),
                        log=str(raw_root/str(relative.with_suffix('.log'))), sha256=digest(report_path), log_sha256=digest(log_path),
                        quiet_cpu_samples=[5]*12, quiet_build_free_samples=12, quiet_sequence_started_utc='2026-11-01T00:00:00Z',
                        build_monitor_observations=2, cases=len(cases), execution_context=dict(host_nonce=host['instance_nonce'],
                            production_sha256=manifests[arm][DLL], phase=phase, shard=shard))
                    write((root/relative).with_suffix('.json'),receipt)
                    receipts.append(receipt); reports.append(report); logs.append(log)
                merged = combine_reports(reports,[o['raw'] for o in task['operations']],methods)
                relative = Path(f'round{index}-{arm}-{fixture}')/'results'/'merged.json'
                write(root/relative,merged)
                (root/f'round{index}-{arm}-{fixture}.log').write_text('\n'.join(logs), encoding='utf-8')
                state['rounds'].append(dict(index=index,arm=arm,fixture=fixture,operation_checkpoints=receipts,
                    report=str(raw_root/str(relative)),sha256=digest(root/relative),cases=len(merged['Benchmarks']),
                    build_monitor_observations=sum(c['build_monitor_observations'] for c in receipts)))
        write(root/'status.json',state)


class HostedTimingTests(unittest.TestCase):
    def test_selected_plan_is_shared_by_collector(self):
        env = {**os.environ, 'HOSTED_TIMING_PLAN': 'benchmarks/plans/hosted-repair-20261101.json',
               'PYTHONPATH': str(Path(__file__).resolve().parent)}
        selected = subprocess.check_output([sys.executable, '-c',
            'from hosted_timing.pipeline import PLAN; from hosted_timing.verify import PLAN as V; '
            'assert PLAN == V; print(PLAN.name)'], env=env, text=True).strip()
        self.assertEqual(selected, 'hosted-repair-20261101.json')

    def test_repaired_sources_keep_their_own_gpu_identity(self):
        plan = dict(sources=dict(old='old', successor='repaired', variants='repaired-variants'))
        self.assertEqual(original_gpu_source(plan, 'successor'), 'repaired')
        self.assertEqual(original_gpu_source(plan, 'variants'), '6bc51bec06a49e2fa2e86e634ec6e2163ee4faaa')
        plan['gpu_sources'] = dict(variants='repaired-variants')
        self.assertEqual(original_gpu_source(plan, 'variants'), 'repaired-variants')
        self.assertEqual(original_gpu_source(plan, 'old'), 'old')

    def test_full_successor_coverage_and_aliases(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            make_evidence(root,'successor')
            result = collect(root,'successor')
            self.assertEqual((result['case_count'],result['decision_count']),(1860,372))
            self.assertTrue(result['accepted'])
            self.assertEqual(result['verdicts'],{'faster':372})
            proof_path = root/'shard6/provenance.json'
            proof = read(proof_path); proof['diagnostic'] = 'trix-jit'; write(proof_path, proof)
            with self.assertRaisesRegex(AssertionError, 'Diagnostic measurements'):
                collect(root, 'successor')
            proof.pop('diagnostic'); write(proof_path, proof)
            proof_path = root/'shard1/provenance.json'
            proof = read(proof_path); proof['host']['run_attempt']='2'; write(proof_path,proof)
            with self.assertRaises(AssertionError): collect(root,'successor')

    def test_trix_diagnostic_is_bounded_and_does_not_mutate_plan(self):
        plan = read(PLAN)
        before = copy.deepcopy(plan)
        task, = trix_diagnostic_tasks(plan)
        self.assertEqual(plan, before)
        self.assertEqual([op['pair'] for op in task['operations']], ['Skender.GetTrix'])
        self.assertEqual(task['cases'], 16)
        self.assertEqual(task['methods'], ['BuilderFull', 'BuilderLatestOnly'])

    def test_variants_and_corruption(self):
        with tempfile.TemporaryDirectory() as temp:
            root = Path(temp)
            make_evidence(root,'variants')
            result = collect(root,'variants')
            self.assertEqual((result['case_count'],result['decision_count']),(32,8))
            state_path=root/'shard0/status.json'
            original=read(state_path)
            for field,value in [('quiet_cpu_samples',[16]*12),('execution_context',{}),('sha256','bad')]:
                state=copy.deepcopy(original)
                state['rounds'][0]['operation_checkpoints'][0][field]=value
                write(state_path,state)
                with self.assertRaises(AssertionError): collect(root,'variants')
            write(state_path,original)
            log=next(root.glob('shard0/operation-checkpoints/**/*.log'))
            log.write_text('corrupt')
            with self.assertRaises(AssertionError): collect(root,'variants')

    def test_path_escape_and_invalidated_evidence(self):
        with tempfile.TemporaryDirectory() as temp:
            root=Path(temp)
            for path in ('D:/other/report.json','D:/raw/../report.json','D:/raw/invalidated-attempts/report.json'):
                with self.assertRaises((AssertionError,ValueError)): archived(root,'D:/raw',path)

    def test_no_improvement_and_regression_do_not_qualify(self):
        for mean, verdict in ((100,'inconclusive'),(150,'slower')):
            with tempfile.TemporaryDirectory() as temp:
                root=Path(temp)
                make_evidence(root,'variants',new_mean=mean)
                result=collect(root,'variants')
                self.assertFalse(result['accepted'])
                self.assertEqual(result['verdicts'],{verdict:8})

    def test_iteration_numbers_not_just_counts(self):
        log='// Benchmark: x IterationCount=10 WarmupCount=10\n'+''.join(
            f'Workload{kind} {i}: x\n' for kind in ('Warmup','Actual') for i in range(1,11))
        check_log(log,1)
        with self.assertRaises(AssertionError): check_log(log.replace('WorkloadActual 10:','WorkloadActual 9:'),1)

    def test_nonfinite_and_wrong_mean_rejected(self):
        case=dict(Statistics=dict(N=7,OriginalValues=[2]*7,Mean=2,ConfidenceInterval=dict(Lower=1,Upper=3)),
                  Memory=dict(BytesAllocatedPerOperation=0))
        sample(case)
        for mean in (float('nan'),3):
            case['Statistics']['Mean']=mean
            with self.assertRaises(AssertionError): sample(case)

    def test_fixed_pair_and_variant_keys(self):
        self.assertEqual(case_key(dict(FullName='Fixture.BuilderFull(Bars: 1000)',Method='BuilderFull'),'fixed'),('fixed',1000,'BuilderFull'))
        self.assertEqual(case_key(dict(FullName='Fixture.BuilderFull(Count: 10000, Method: Trima)',Method='BuilderFull')),('Trima',10000,'BuilderFull'))


if __name__ == '__main__':
    unittest.main()
