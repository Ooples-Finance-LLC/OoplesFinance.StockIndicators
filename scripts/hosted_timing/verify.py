"""Fail closed on incomplete, relocated incorrectly, or mixed-host timing evidence."""
from collections import Counter, defaultdict
from pathlib import Path, PureWindowsPath
import hashlib
import json
import math
import re

from .checkpoints import case_key, combine_reports
from .pipeline import PLAN, DLL, read, digest, phase_shards, phase_tasks, phase_labels, methods_for, phase_sources, trix_preflight_tasks, warmups_for, warmup_context


def archived(root, raw_root, raw_path):
    base, path = PureWindowsPath(raw_root), PureWindowsPath(raw_path)
    assert base.is_absolute() and path.is_absolute()
    relative = path.relative_to(base)
    assert relative.parts and '..' not in relative.parts
    assert 'invalidated-attempts' not in relative.parts
    target = root.joinpath(*relative.parts).resolve()
    assert target.is_relative_to(root.resolve()) and target.is_file()
    return target


def check_log(log, count, *, warmups=10, native_invocations=None):
    assert native_invocations in (None,1)
    sections = re.split(r'^// Benchmark: ', log, flags=re.M)[1:]
    assert len(sections) == count
    for section in sections:
        header = section.splitlines()[0]
        assert re.search(r'\bIterationCount=10\b', header)
        assert re.search(r'\bWarmupCount=' + str(warmups) + r'\b', header)
        for kind in ('Warmup', 'Actual'):
            numbers = re.findall(r'^Workload' + kind + r'\s+(\d+):', section, re.M)
            expected = warmups if kind == 'Warmup' else 10
            assert list(map(int, numbers)) == list(range(1, expected + 1))
            if native_invocations is not None and re.search(r'\.Native(?:Single|Parallel)\b',header):
                assert re.search(r'\bInvocationCount=1\b',header)
                counts = re.findall(r'^Workload' + kind + r'\s+\d+:\s+(\d+) op,',section,re.M)
                assert len(counts) == expected and all(int(n) == native_invocations for n in counts)


def sample(case):
    stats = case['Statistics']
    values, ci = stats['OriginalValues'], stats['ConfidenceInterval']
    assert len(values) == stats['N'] and 7 <= len(values) <= 10
    assert all(math.isfinite(v) and v > 0 for v in values)
    assert math.isclose(sum(values) / len(values), stats['Mean'], rel_tol=1e-12)
    assert all(math.isfinite(ci[k]) for k in ('Lower', 'Upper'))
    assert ci['Lower'] <= stats['Mean'] <= ci['Upper']
    allocated = case['Memory']['BytesAllocatedPerOperation']
    assert math.isfinite(allocated) and allocated >= 0
    return dict(mean_ns=stats['Mean'], lower_ns=ci['Lower'], upper_ns=ci['Upper'],
                samples=stats['N'], allocated_bytes=allocated)


def collect(folder, phase):
    return _collect(folder, phase, preflight=False)


def collect_preflight(folder):
    return _collect(folder, 'successor', preflight=True)


def _collect(folder, phase, *, preflight):
    plan = read(PLAN)
    if phase == 'coverage':
        from .coverage import verify_reference
        assert plan.get('coverage_reference') and not preflight
        verify_reference(plan)
    roots = sorted(p.parent for p in folder.glob('*/provenance.json'))
    if phase == 'targeted':
        from .targeted import expected_totals, verify_reference
        assert plan.get('targeted_reference') and not preflight
        verify_reference(plan)
        targeted_totals = expected_totals(plan)
    expected_shards = {0} if preflight else set(phase_shards(plan, phase))
    assert len(roots) == len(expected_shards)
    shards, nonces, input_hashes, run_ids = set(), set(), set(), set()
    decisions, candidates = [], defaultdict(list)
    case_count = 0
    for root in roots:
        proof, state = read(root/'provenance.json'), read(root/'status.json')
        if preflight:
            assert proof.get('diagnostic') == 'trix-preflight'
            assert proof.get('diagnostic_environment') == {}, 'Preflight must use the qualification runtime settings'
        else:
            assert not proof.get('diagnostic'), 'Diagnostic measurements cannot qualify production'
        shard = proof['shard']
        assert shard in expected_shards and shard not in shards
        shards.add(shard)
        assert proof['phase'] == phase and proof['sources'] == phase_sources(plan, phase)
        assert proof['plan_sha256'] == digest(PLAN)
        inputs, host = proof['input_provenance'], proof['host']
        # The embedded document has the exact canonical encoding used by prepare.
        encoded = (json.dumps(inputs, indent=2) + '\n').encode()
        assert hashlib.sha256(encoded).hexdigest() == proof['input_provenance_sha256']
        assert inputs['sources'] == plan['sources'] and inputs['plan_sha256'] == digest(PLAN)
        assert inputs['sdk'] == plan['proposed_dotnet_sdk']
        assert host['run_id'] and host['run_attempt'] and host['machine'] and host['runner_image']
        assert host['run_id'] == inputs['run_id'] and host['run_attempt'] == inputs['run_attempt']
        assert host['instance_nonce'] and host['instance_nonce'] not in nonces
        nonces.add(host['instance_nonce'])
        input_hashes.add(proof['input_provenance_sha256'])
        run_ids.add((host['run_id'], host['run_attempt'], inputs['workflow_head']))
        manifests = proof['binary_manifest']
        labels = phase_labels(phase)
        for arm, label in zip(('old', 'new'), labels):
            expected = dict(inputs['common_manifest'])
            expected[DLL] = inputs['production_sha256'][label]
            assert manifests[arm] == expected
        assert state['status'] == 'raw-measurements-complete'
        assert state['phase'] == phase and state['shard'] == shard
        assert state['order'] == ['old', 'new', 'new', 'old']
        tasks = {t['fixture']: t for t in (trix_preflight_tasks(plan) if preflight else phase_tasks(plan, phase, shard))}
        assert len(state['rounds']) == 4 * len(tasks)
        rounds = {}
        for record in state['rounds']:
            index, fixture = record['index'], record['fixture']
            assert index in range(4) and fixture in tasks and (fixture, index) not in rounds
            arm = state['order'][index]
            assert record['arm'] == arm
            task = tasks[fixture]
            mapping = {op['raw']: op['pair'] for op in task['operations']}
            methods = methods_for(task, index)
            implicit = next(iter(mapping)) if task.get('implicit_pair') else None
            chunks = record['operation_checkpoints']
            assert [c['pair'] for c in chunks] == list(mapping)
            originals, logs = [], []
            for position, chunk in enumerate(chunks):
                assert chunk['execution_context'] == dict(host_nonce=host['instance_nonce'],
                    production_sha256=manifests[arm][DLL], phase=phase, shard=shard, **warmup_context(plan, fixture, task))
                assert len(chunk['quiet_cpu_samples']) == 12
                assert all(math.isfinite(v) and 0 <= v <= 15 for v in chunk['quiet_cpu_samples'])
                assert chunk['quiet_build_free_samples'] == 12 and chunk['build_monitor_observations'] >= 1
                assert chunk['quiet_sequence_started_utc']
                assert chunk['cases'] == 2 * len(methods)
                checkpoint = root/'operation-checkpoints'/fixture/f'round{index}-{arm}'
                assert read(checkpoint/f'operation{position}.json') == chunk
                paths = {kind: archived(root, proof['raw_root'], chunk[kind]) for kind in ('report', 'log')}
                assert paths['report'].is_relative_to((checkpoint/f'operation{position}'/'results').resolve())
                assert paths['log'] == (checkpoint/f'operation{position}.log').resolve()
                assert digest(paths['report']) == chunk['sha256'] and digest(paths['log']) == chunk['log_sha256']
                original = read(paths['report'])
                combine_reports([original], [chunk['pair']], methods, implicit)
                log = paths['log'].read_text(encoding='utf-8-sig')
                check_log(log, chunk['cases'], warmups=warmups_for(plan, fixture, task),
                    native_invocations=plan.get('coverage_harness',{}).get('native_invocation_count') if phase == 'coverage' else None)
                originals.append(original)
                logs.append(log)
            merged_path = archived(root, proof['raw_root'], record['report'])
            assert digest(merged_path) == record['sha256']
            merged = read(merged_path)
            assert combine_reports(originals, list(mapping), methods, implicit) == merged
            assert (root/f'round{index}-{arm}-{fixture}.log').read_text(encoding='utf-8-sig') == '\n'.join(logs)
            assert record['build_monitor_observations'] == sum(c['build_monitor_observations'] for c in chunks)
            cases = {}
            for case in merged['Benchmarks']:
                assert fixture + '.' in case['FullName']
                raw_pair, bars, method = case_key(case, implicit)
                key = mapping[raw_pair], bars, method
                assert key not in cases
                cases[key] = dict(pair=key[0], bars=bars, method=method, **sample(case))
            assert len(cases) == record['cases']
            rounds[fixture, index] = cases
            case_count += len(cases)
        for fixture in tasks:
            provenance = dict(host=host, shard=shard, phase=phase, sources=proof['sources'],
                workflow_head=inputs['workflow_head'], input_provenance_sha256=proof['input_provenance_sha256'], **warmup_context(plan, fixture, tasks[fixture]))
            for key in sorted(rounds[fixture, 0]):
                old = [rounds[fixture, i][key] for i in (0, 3)]
                new = [rounds[fixture, i][key] for i in (1, 2)]
                verdict = ('faster' if max(r['upper_ns'] for r in new) < min(r['lower_ns'] for r in old) else
                           'slower' if min(r['lower_ns'] for r in new) > max(r['upper_ns'] for r in old) else 'inconclusive')
                decisions.append(dict(fixture=fixture, pair=key[0], bars=key[1], method=key[2],
                    old_means_ms=[r['mean_ns']/1e6 for r in old], new_means_ms=[r['mean_ns']/1e6 for r in new],
                    verdict=verdict, provenance=provenance))
            candidates[fixture].extend(dict(**row, provenance=provenance) for row in rounds[fixture, 2].values())
    assert shards == expected_shards and len(input_hashes) == len(run_ids) == 1
    expected_cases, expected_decisions = ((48, 12) if preflight else (1860, 372) if phase == 'successor'
                                          else targeted_totals if phase == 'targeted'
                                          else (40, 8) if phase == 'coverage' else (32, 8))
    assert case_count == expected_cases and len(decisions) == expected_decisions
    assert len({(d['pair'], d['bars'], d['method']) for d in decisions}) == expected_decisions
    counts = Counter(d['verdict'] for d in decisions)
    return dict(all_cases_verified=True, phase='trix-preflight' if preflight else phase, case_count=case_count, decision_count=len(decisions),
        input_provenance_sha256=next(iter(input_hashes)),
        sources=phase_sources(plan, phase), decisions=decisions, verdicts=dict(counts),
        accepted=phase != 'coverage' and counts['faster'] > 0 and counts['slower'] == 0,
        final_candidate_round=2, final_candidate_rows=dict(candidates),
        **({'coverage_complete': True} if phase == 'coverage' else {}))
