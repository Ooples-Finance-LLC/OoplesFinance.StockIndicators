"""Frozen same-binary star diagnostic; cannot qualify production."""
import math
import os
import platform
import shutil
import subprocess
import time
import uuid
from . import pipeline as p

PLAN = p.REPO/'benchmarks/plans/star-warmup-diagnostic-20261114.json'
SHA = '8fa57e2a62d1539ca1ebff23d03f57b4a03ecde2f056a2798732dd2f26bca4f0'


class StarWarmupContext(dict):
    """Only this diagnostic entry point enables the restricted one-size runner."""


def validate(inputs):
    assert p.digest(PLAN) == SHA, 'Frozen diagnostic plan changed'
    d = p.read(PLAN)
    assert d['diagnostic_only'] and d['status'] == 'frozen'
    assert p.PLAN.resolve() == (p.REPO/d['original_plan']).resolve()
    assert p.digest(p.PLAN) == d['original_plan_sha256']
    assert p.digest(inputs/'provenance.json') == d['original_input']['provenance_sha256']
    proof = p.verify_inputs(inputs)
    assert proof['sources'] == d['sources'] and proof['production_sha256'] == d['production_sha256']
    assert proof['sdk'] == d['sdk']
    assert p.git('rev-parse', d['published_source']+':src') == d['published_src_tree']
    assert p.git('rev-parse', d['sources']['successor']+':src') == d['published_src_tree']
    for key in ('run_id', 'run_attempt', 'workflow_head'):
        assert proof[key] == d['original_input'][key]
    assert d['bars'] == [1000] and d['methods'] == ['Ooples']
    assert d['warmups'] == {'A':1000, 'B':20000} and d['order'] == ['A','B','B','A']
    return d, proof


def controls():
    names = ('TieredPGO','TieredCompilation','ReadyToRun','TC_QuickJit','JitStress','JitDisasm','JitDisasmAssemblies','JitPrintInlinedMethods')
    return {k:v for k,v in os.environ.items() if k in {a+b for a in ('DOTNET_','COMPlus_') for b in names} and v}


def measure(inputs, out, shard):
    import psutil
    d, proof = validate(inputs)
    assert not controls(), 'Default runtime required'
    assert subprocess.check_output(['dotnet','--version'], text=True).strip() == d['sdk']
    assert shard in range(2) and not out.exists()
    out.mkdir(parents=True)
    runtime = out.parent/(out.name+'-runtime')
    shutil.copytree(inputs/'common', runtime)
    shutil.copy2(inputs/'production/successor.dll', runtime/p.DLL)
    binary = dict(proof['common_manifest'], **{p.DLL:proof['production_sha256']['successor']})
    assert p.manifest(runtime) == binary
    host = dict(machine=platform.node(), instance_nonce=uuid.uuid4().hex,
        run_id=os.environ['GITHUB_RUN_ID'], run_attempt=os.environ['GITHUB_RUN_ATTEMPT'],
        runner_image=os.environ['ImageVersion'], logical_cpus=psutil.cpu_count())
    provenance = dict(diagnostic_only=True, diagnostic='star-warmup', diagnostic_plan_sha256=SHA, shard=shard, raw_root=str(out),
        host=host, workflow_head=p.git('rev-parse','HEAD'), runtime_controls=controls(),
        input_provenance=proof, binary_manifest=binary)
    p.write(out/'provenance.json', provenance)
    state = dict(status='starting', shard=shard, order=d['order'], rounds=[])
    waiting = [None]
    def save():
        now = time.monotonic()
        if state['status'] == 'waiting-quiet-window':
            if waiting[0] is None: waiting[0] = now
            if now-waiting[0] > 900: raise TimeoutError('No qualifying idle window within 15 minutes')
        else: waiting[0] = None
        p.write(out/'status.json', state)
    def builds():
        return [v.pid for v in psutil.process_iter(['name','cmdline'])
            if (v.info['name'] or '').lower() in ('dotnet','dotnet.exe')
            and any(x in (v.info['cmdline'] or []) for x in ('build','test','vstest','publish','restore','pack'))]
    def assert_binaries():
        assert shutil.disk_usage(out).free > 2*1024**3
        assert p.manifest(runtime) == binary
        assert not controls()
    env = os.environ.copy()
    for key in ('COMPARISON_PAIR','COMPARISON_SHARD','COMPARISON_SHARDS','COMPARISON_FAMILY'): env.pop(key,None)
    try:
        for index,label in enumerate(d['order']):
            context = StarWarmupContext(diagnostic_plan_sha256=SHA, warmup_count=d['warmups'][label],
                pairs=d['jobs'][shard], host_nonce=host['instance_nonce'], production_sha256=binary[p.DLL])
            record = p.run_checkpointed(out=out,index=index,arm=label,fixture='LibraryPairBenchmarks',
                pairs=d['jobs'][shard],methods=['Ooples'],runtime=runtime,env=env,state=state,save=save,
                builds=builds,assert_binaries=assert_binaries,context=context,
                warmups=context['warmup_count'],bars=(1000,))
            state['rounds'].append(record)
            state['status']='round-complete'
            save()
        assert_binaries()
        state['status']='raw-measurements-complete'
        save()
    except BaseException as error:
        state.update(status='failed',error=repr(error))
        p.write(out/'status.json',state)
        raise


def collect(inputs, folder):
    from .verify import archived, check_log, sample
    from .checkpoints import combine_reports, case_key
    d, inputs_proof = validate(inputs)
    roots = sorted(x.parent for x in folder.glob('*/provenance.json'))
    assert len(roots) == 2
    shards, nonces, runs, rows = set(), set(), set(), []
    cases = 0
    for root in roots:
        proof, state = p.read(root/'provenance.json'), p.read(root/'status.json')
        shard = proof['shard']
        assert shard in range(2) and shard not in shards
        shards.add(shard)
        assert proof['diagnostic_only'] is True and proof['diagnostic_plan_sha256'] == SHA
        assert proof['diagnostic'] == 'star-warmup'
        assert proof['runtime_controls'] == {} and proof['input_provenance'] == inputs_proof
        expected = dict(inputs_proof['common_manifest'], **{p.DLL:inputs_proof['production_sha256']['successor']})
        assert proof['binary_manifest'] == expected
        host=proof['host']
        assert host['machine'] and host['runner_image'] and host['run_id'] and host['run_attempt']
        assert host['instance_nonce'] not in nonces
        nonces.add(host['instance_nonce']); runs.add((host['run_id'],host['run_attempt'],proof['workflow_head']))
        assert state['status'] == 'raw-measurements-complete' and state['shard'] == shard
        assert state['order'] == d['order'] and len(state['rounds']) == 4
        rounds = []
        for index,record in enumerate(state['rounds']):
            label = d['order'][index]
            assert record['index'] == index and record['arm'] == label and record['fixture'] == 'LibraryPairBenchmarks'
            chunks=record['operation_checkpoints']
            assert [v['pair'] for v in chunks] == d['jobs'][shard]
            originals, logs, samples = [], [], {}
            checkpoint=root/'operation-checkpoints/LibraryPairBenchmarks'/f'round{index}-{label}'
            for position,chunk in enumerate(chunks):
                assert p.read(checkpoint/f'operation{position}.json') == chunk
                assert chunk['execution_context'] == dict(diagnostic_plan_sha256=SHA,warmup_count=d['warmups'][label],
                    pairs=d['jobs'][shard],host_nonce=host['instance_nonce'],production_sha256=expected[p.DLL])
                assert len(chunk['quiet_cpu_samples']) == chunk['quiet_build_free_samples'] == 12
                assert all(math.isfinite(v) and 0 <= v <= 15 for v in chunk['quiet_cpu_samples'])
                assert chunk['build_monitor_observations'] >= 1 and chunk['quiet_sequence_started_utc']
                assert chunk['cases'] == 1
                paths={key:archived(root,proof['raw_root'],chunk[key]) for key in ('report','log')}
                assert paths['report'].is_relative_to((checkpoint/f'operation{position}/results').resolve())
                assert paths['log'] == (checkpoint/f'operation{position}.log').resolve()
                assert p.digest(paths['report']) == chunk['sha256'] and p.digest(paths['log']) == chunk['log_sha256']
                report=p.read(paths['report']); log=paths['log'].read_text(encoding='utf-8-sig')
                combine_reports([report],[chunk['pair']],['Ooples'],bars=(1000,))
                check_log(log,1,warmups=d['warmups'][label])
                case=report['Benchmarks'][0]
                assert 'LibraryPairBenchmarks.Ooples' in case['FullName']
                assert case_key(case) == (chunk['pair'],1000,'Ooples')
                samples[chunk['pair']]=sample(case)
                originals.append(report); logs.append(log); cases+=1
            merged=archived(root,proof['raw_root'],record['report'])
            assert p.digest(merged) == record['sha256']
            assert p.read(merged) == combine_reports(originals,d['jobs'][shard],['Ooples'],bars=(1000,))
            assert (root/f'round{index}-{label}-LibraryPairBenchmarks.log').read_text(encoding='utf-8-sig') == '\n'.join(logs)
            assert record['cases'] == len(d['jobs'][shard]) and record['build_monitor_observations'] == sum(c['build_monitor_observations'] for c in chunks)
            rounds.append(samples)
        for pair in d['jobs'][shard]:
            a=[rounds[i][pair] for i in (0,3)]; b=[rounds[i][pair] for i in (1,2)]
            separation=('B_below_A' if max(v['upper_ns'] for v in b)<min(v['lower_ns'] for v in a)
                else 'B_above_A' if min(v['lower_ns'] for v in b)>max(v['upper_ns'] for v in a) else 'overlap')
            rows.append(dict(pair=pair,bars=1000,method='Ooples',A=a,B=b,separation=separation,host=host))
    assert shards == {0,1} and len(runs) == 1 and cases == d['cases'] and len(rows) == d['comparisons']
    return dict(diagnostic_only=True,same_binary=True,all_cases_verified=True,case_count=cases,
        comparison_count=len(rows),comparisons=rows,diagnostic_plan_sha256=SHA,
        original_input=d['original_input'],measurement_run=list(next(iter(runs))),
        warmups=d['warmups'],iterations=10,production_integrated=False,report_imported=False,policy_changed=False)
