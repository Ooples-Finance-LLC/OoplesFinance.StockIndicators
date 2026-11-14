"""Measure one correctness-qualified paired batch on an otherwise quiet machine.

Does not suspend or terminate unrelated work. Refuses a live competitor timing
coordinator. Results retain source, raw output and process-sampling evidence.
"""
import argparse
import datetime
import gzip
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time
import xml.etree.ElementTree as ET

from paired_batch_timing import FIXTURES, collect


DLL = 'benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
QUALIFICATION = {
    'PairedBatchAllocationBenchmarks': ('paired-allocation', 'PairedBatchAllocationTests', 14, 'PairedBatchAllocationBenchmarkTests', 9),
    'KaufmanStressBatchBenchmarks': ('kaufman-stress', 'KaufmanStressBatchTests', 97, 'KaufmanStressBatchBenchmarkTests', 3),
    'RsmkBatchBenchmarks': ('rsmk', 'RsmkBatchTests', 153, 'RsmkBatchBenchmarkTests', 3),
    'SectorRotationBatchBenchmarks': ('sector-rotation', 'SectorRotationBatchTests', 33, 'SectorRotationBatchBenchmarkTests', 3),
}


def passed_trx(raw, required):
    namespace = '{http://microsoft.com/schemas/VisualStudio/TeamTest/2010}'  # NOSONAR(S5332) XML namespace, never a network request.
    root = ET.fromstring(raw)
    rows = root.findall('.//' + namespace + 'UnitTestResult')
    summary = root.find(namespace + 'ResultSummary')
    counters = summary.find(namespace + 'Counters') if summary is not None else None
    if not rows or summary is None or summary.get('outcome') != 'Completed' or counters is None:
        raise ValueError('Incomplete correctness evidence')
    if any(row.get('outcome') != 'Passed' for row in rows) or any(
            int(counters.get(key, '-1')) != len(rows) for key in ('total', 'executed', 'passed')):
        raise ValueError('Failed, skipped or inconsistent correctness evidence')
    for name, minimum in required.items():
        if sum('.' + name + '.' in row.get('testName', '') for row in rows) < minimum:
            raise ValueError('Missing correctness coverage: ' + name)
    return len(rows)


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def contained(root, name):
    path = (root / name).resolve()
    if not path.is_relative_to(root.resolve()) or path == root.resolve():
        raise ValueError('Snapshot path escapes its root')
    return path


def verify_snapshot(pending):
    if pending.get('status') != 'correctness-qualified-dedicated-timing-pending':
        raise ValueError('Correctness qualification is required')
    fixture = pending.get('fixture')
    if fixture not in FIXTURES or pending.get('operations') != list(FIXTURES[fixture]):
        raise ValueError('Unrecognized fixture/operation grid')
    if pending.get('sizes') != [1000, 10000] or pending.get('methods') != ['Legacy', 'Shared']:
        raise ValueError('Unexpected workload')
    snapshot = Path(pending['snapshot']).resolve()
    manifest = read(snapshot / 'manifest.json')
    if manifest.get('status') != 'correctness-qualified-performance-pending':
        raise ValueError('Unqualified snapshot')
    files = manifest.get('files_sha256', {})
    required = {DLL, 'qualified-working-source.zip', 'evidence/unit.trx', 'evidence/parity.trx',
                'evidence/gpu-receipt.json', 'evidence/gpu-gpu.trx.gz'}
    if not required <= files.keys():
        raise ValueError('Missing snapshot binaries/source/correctness evidence')
    for name, expected in files.items():
        if digest(contained(snapshot, name)) != expected:
            raise ValueError('Snapshot hash mismatch: ' + name)
    family, unit_class, unit_count, parity_class, parity_count = QUALIFICATION[fixture]
    if family not in manifest.get('qualified_families', []):
        raise ValueError('Snapshot qualifies a different family')
    passed_trx((snapshot / 'evidence/unit.trx').read_bytes(),
               {unit_class: unit_count, 'PairedNumericalTests': 1, 'PairedInputContractTests': 1})
    passed_trx((snapshot / 'evidence/parity.trx').read_bytes(), {parity_class: parity_count})
    gpu = read(snapshot / 'evidence/gpu-receipt.json')
    raw = gzip.decompress((snapshot / 'evidence/gpu-gpu.trx.gz').read_bytes())
    if gpu.get('passed_cases') != 100 or passed_trx(raw, {}) != 100 or hashlib.sha256(raw).hexdigest() != gpu.get('trx_sha256'):
        raise ValueError('Invalid GPU correctness receipt')
    return snapshot, digest(snapshot / 'manifest.json')


def activity(first, current):
    if current['time'] <= first['time']:
        return True
    # Disappearing processes are also uncertain: their final CPU time is unknown.
    if first['processes'].keys() != current['processes'].keys():
        return True
    total = 0
    for pid, row in current['processes'].items():
        old = first['processes'][pid]
        if any(old[k] != row[k] for k in ('created', 'cmd')):
            return True
        delta = row['cpu'] - old['cpu']
        if delta < 0:
            return True
        total += delta
    return total > 0.1 + 0.01 * (current['time'] - first['time'])


def sample(psutil, excluded=()):
    processes = {}
    for process in psutil.process_iter(['pid', 'ppid', 'name', 'create_time', 'cmdline', 'cpu_times']):
        try:
            p = process.info
            processes[p['pid']] = dict(parent=p['ppid'], name=p['name'], created=p['create_time'],
                                       cmd=p['cmdline'], cpu=p['cpu_times'].user + p['cpu_times'].system)
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            continue
    tracked = {pid for pid, p in processes.items() if p['name'].lower() in
               ('dotnet.exe', 'testhost.exe', 'msbuild.exe', 'vbcscompiler.exe', 'csc.exe', 'vbc.exe')}
    excluded = set(excluded)
    for group in (tracked, excluded):
        while True:
            extra = {pid for pid, p in processes.items() if p['parent'] in group} - group
            if not extra:
                break
            group.update(extra)
    return dict(time=time.monotonic(), processes={str(pid): processes[pid] for pid in tracked - excluded if pid in processes})


def refuse_competing_runner(psutil):
    for process in psutil.process_iter(['pid', 'cmdline']):
        command = process.info['cmdline'] or []
        if process.pid != os.getpid() and any(
                'si-competitor-refresh-20261021/' in arg.replace('\\', '/') and arg.endswith('.py')
                for arg in command):
            raise RuntimeError('Competitor timing coordinator is live; finish it before paired timing')
        if process.pid != os.getpid() and any(Path(arg).name == 'run_paired_batch_timing.py' for arg in command):
            raise RuntimeError('Another paired timing runner is live')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--pending', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--dotnet', type=Path, required=True)
    args = parser.parse_args()
    import psutil
    refuse_competing_runner(psutil)
    pending_hash = digest(args.pending)
    pending = read(args.pending)
    snapshot, manifest_hash = verify_snapshot(pending)
    if not args.dotnet.is_file():
        raise ValueError('Missing runtime executable')
    args.output.mkdir(parents=True, exist_ok=False)
    state = dict(status='waiting-quiet-machine', snapshot=str(snapshot), fixture=pending['fixture'],
                 pending_sha256=pending_hash, manifest_sha256=manifest_hash)

    def save(status, **extra):
        state.update(status=status, updated_utc=datetime.datetime.now(datetime.timezone.utc).isoformat(), **extra)
        (args.output / 'status.json').write_text(json.dumps(state, indent=2), encoding='utf-8')

    try:
        started = time.monotonic()
        first = previous = sample(psutil)
        history = [first]
        while previous['time'] - first['time'] < 240:
            if time.monotonic() - started >= 1200:
                raise TimeoutError('No quiet window within 20 minutes')
            refuse_competing_runner(psutil)
            save('waiting-quiet-machine', quiet_seconds=round(previous['time'] - first['time']))
            time.sleep(10)
            current = sample(psutil)
            if activity(first, current) or activity(previous, current):
                first = current
                history = []
            history.append(current)
            previous = current
        quiet = args.output / 'quiet.json'
        quiet.write_text(json.dumps(history), encoding='utf-8')
        command = [str(args.dotnet.resolve()), str(snapshot / DLL), '--filter',
                   '*' + pending['fixture'] + '.Legacy*', '*' + pending['fixture'] + '.Shared*',
                   '--warmupCount', '10', '--iterationCount', '10', '--exporters', 'json', 'csv',
                   '--artifacts', str(args.output.resolve() / 'artifacts')]
        before = previous = sample(psutil)
        contaminated = False
        with (args.output / 'stdout.log').open('wb') as stdout, (args.output / 'stderr.log').open('wb') as stderr:
            process = subprocess.Popen(command, cwd=(snapshot / DLL).parent, stdout=stdout, stderr=stderr)
            save('measuring', pid=process.pid, command=command)
            with (args.output / 'processes.jsonl').open('w', encoding='utf-8') as log:
                # In-process BenchmarkDotNet jobs keep their workload in this process.
                # Observe to completion; contention rejects results without killing work.
                while True:
                    code = process.poll()
                    current = sample(psutil, [process.pid])
                    log.write(json.dumps(current) + '\n'); log.flush()
                    contaminated |= activity(before, current) or activity(previous, current)
                    previous = current
                    if code is not None:
                        break
                    time.sleep(10)
            code = process.wait()
        save('execution-finished', exit_code=code, contaminated=contaminated)
        if code != 0 or contaminated:
            raise ValueError('Failed or contaminated execution; timings are not qualified')
        reports = list((args.output / 'artifacts/results').glob('*-report-full-compressed.json'))
        if len(reports) != 1:
            raise ValueError('Expected exactly one raw benchmark report')
        result = collect(read(reports[0]), pending['fixture'])
        _, after_hash = verify_snapshot(pending)
        if manifest_hash != after_hash or pending_hash != digest(args.pending):
            raise ValueError('Inputs changed during measurement')
        result.update(status='qualified-full-batch-comparison', snapshot=str(snapshot),
                      manifest_sha256=manifest_hash, pending_sha256=pending_hash,
                      report=str(reports[0]), report_sha256=digest(reports[0]),
                      quiet_sha256=digest(quiet), processes_sha256=digest(args.output / 'processes.jsonl'),
                      command=command, completed_utc=datetime.datetime.now(datetime.timezone.utc).isoformat())
        (args.output / 'receipt.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
        save('qualified-full-batch-comparison', rows=len(result['rows']))
    except BaseException as error:
        save('failed-needs-inspection', error=repr(error))
        raise


if __name__ == '__main__':
    main()
