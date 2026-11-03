"""Checkpoint successor operations with raw aliases and fixed-pair support."""
from pathlib import Path
import datetime, hashlib, json, re, subprocess, time

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

def case_key(case, implicit_pair=None):
    name = case['FullName']
    operation = re.search(r'(?:Operation|PairId): "([^"]+)"', name)
    variant = re.search(r'Method: (Wma|Trima)(?:,|\))', name)
    variance = re.search(r'Variance: (True|False)(?:,|\))', name)
    pair = operation[1] if operation else variant[1] if variant else variance[1] if variance else implicit_pair
    assert pair is not None, name
    return (pair, int(re.search(r'(?:Count|Bars): (\d+)', name)[1]), case['Method'])

def combine_reports(reports, pairs, methods, implicit_pair=None):
    merged = dict(reports[0])
    cases = [case for report in reports for case in report['Benchmarks']]
    keys = [case_key(case, implicit_pair) for case in cases]
    expected = {(p, n, m) for p in pairs for n in (1000, 10000) for m in methods}
    assert len(keys) == len(set(keys)) == len(expected)
    assert set(keys) == expected
    assert all(c.get('Statistics') and c['Statistics']['N'] >= 7 for c in cases)
    merged['Benchmarks'] = cases
    return merged


def filters(fixture, pair, methods, *, implicit=False, variant=False, variance=False):
    assert sum(map(bool, (implicit, variant, variance))) <= 1
    if variance: assert fixture == 'SharedDispersionBenchmarks' and pair in ('True','False')
    return [(f'*{fixture}.{method}*' if implicit else
             f'*{fixture}.{method}*Method: {pair}*' if variant else
             f'*{fixture}.{method}*Variance: {pair}*' if variance else
             f'*{fixture}.{method}*"{pair}"*') for method in methods]

def run_checkpointed(*, out, index, arm, fixture, pairs, methods, runtime, env,
                    state, save, builds, assert_binaries, implicit=False, variant=False, variance=False, context=None, warmups=10,
                    operation_timeout_seconds=3600):
    assert warmups in (10, 1000)
    assert operation_timeout_seconds in (3600,10800)
    assert operation_timeout_seconds == 3600 or context and context.get('phase') == 'coverage'
    from .cleanup import capture_children, stop_owned_benchmark, archive_with_retry
    root = out / 'operation-checkpoints' / fixture / f'round{index}-{arm}'
    root.mkdir(parents=True, exist_ok=True)
    receipts = []
    quiet = None
    quiet_started = None
    for position, pair in enumerate(pairs):
        receipt_path = root / f'operation{position}.json'
        if receipt_path.exists():
            receipt = json.loads(receipt_path.read_text())
            assert receipt['pair'] == pair
            assert receipt.get('execution_context') == context
            assert digest(Path(receipt['report'])) == receipt['sha256']
            assert digest(Path(receipt['log'])) == receipt['log_sha256']
            receipts.append(receipt)
            continue
        while True:
            state.update(round=index, arm=arm, fixture=fixture, operation=pair,
                         checkpointed_operations=len(receipts), operation_count=len(pairs))
            if quiet is None or builds():
                quiet = []
                quiet_started = datetime.datetime.now(datetime.timezone.utc).isoformat()
                state['status'] = 'waiting-quiet-window'
                save()
                import psutil
                while len(quiet) < 12 or any(v > 15 for v in quiet[-12:]):
                    active = builds()
                    quiet.append(psutil.cpu_percent(interval=5))
                    active += builds()
                    if active:
                        quiet = []
                    state.update(waiting_build_pids=sorted(set(active)), quiet_cpu_samples=quiet[-12:])
                    save()
            if builds():
                quiet = None
                continue
            assert_binaries()
            folder = root / f'operation{position}'
            log_path = folder.with_suffix('.log')
            if folder.exists() or log_path.exists():
                rejected = out / 'invalidated-attempts' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')
                rejected.mkdir(parents=True, exist_ok=False)
                for source in (folder, log_path):
                    if source.exists():
                        destination = rejected / source.name
                        assert source.resolve().is_relative_to(out.resolve())
                        assert destination.resolve().is_relative_to(out.resolve()) and not destination.exists()
                        archive_with_retry(source, destination, out)
                (rejected/'interruption.json').write_text(json.dumps(dict(round=index,arm=arm,fixture=fixture,pair=pair,
                    reason='Unreceipted raw attempt preserved before retry after restart.'),indent=2))
            state.update(status='measuring', waiting_build_pids=[])
            save()
            args = ['dotnet', str(runtime / 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'),
                    '--filter', *filters(fixture,pair,methods,implicit=implicit,variant=variant,variance=variance),
                    '--warmupCount', str(warmups), '--iterationCount', '10', '--exporters', 'json',
                    '--artifacts', str(folder)]
            interrupted = []
            observations = 0
            children = {}
            with log_path.open('wb') as log:
                process = subprocess.Popen(args, cwd=runtime, env=env, stdout=log, stderr=subprocess.STDOUT)
                started = time.monotonic()
                while True:
                    capture_children(process, children)
                    interrupted = builds()
                    observations += 1
                    if interrupted:
                        stop_owned_benchmark(process, children)
                        break
                    code = process.poll()
                    if code is not None:
                        if code:
                            raise subprocess.CalledProcessError(code, args)
                        break
                    if time.monotonic() - started > operation_timeout_seconds:
                        stop_owned_benchmark(process, children)
                        raise subprocess.TimeoutExpired(args, operation_timeout_seconds)
                    time.sleep(1)
            if interrupted:
                rejected = out / 'invalidated-attempts' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%S%f')
                rejected.mkdir(parents=True, exist_ok=False)
                for source in (folder, log_path):
                    if source.exists():
                        destination = rejected / source.name
                        assert source.resolve().is_relative_to(out.resolve())
                        assert destination.resolve().is_relative_to(out.resolve()) and not destination.exists()
                        archive_with_retry(source, destination, out)
                (rejected / 'interruption.json').write_text(json.dumps(dict(round=index, arm=arm,
                    fixture=fixture, pair=pair, external_build_pids=interrupted,
                    reason='Only this operation is excluded; completed operation receipts remain valid.'), indent=2))
                quiet = None
                continue
            reports = list((folder / 'results').glob('*-report-full-compressed.json'))
            assert len(reports) == 1
            combine_reports([json.loads(reports[0].read_bytes())], [pair], methods, pair if implicit else None)
            receipt = dict(pair=pair, report=str(reports[0]), sha256=digest(reports[0]),
                log=str(log_path), log_sha256=digest(log_path), quiet_cpu_samples=quiet[-12:],
                quiet_build_free_samples=12, quiet_sequence_started_utc=quiet_started,
                build_monitor_observations=observations, cases=2 * len(methods), execution_context=context)
            temporary = receipt_path.with_suffix('.tmp')
            temporary.write_text(json.dumps(receipt, indent=2))
            temporary.replace(receipt_path)
            receipts.append(receipt)
            break
    merged = combine_reports([json.loads(Path(r['report']).read_bytes()) for r in receipts], pairs, methods, pairs[0] if implicit else None)
    folder = out / f'round{index}-{arm}-{fixture}'
    (folder / 'results').mkdir(parents=True, exist_ok=True)
    report = folder / 'results' / f'OoplesFinance.StockIndicators.CompetitorBenchmarks.{fixture}-report-full-compressed.json'
    merged_text = json.dumps(merged)
    merged_log = '\n'.join(Path(r['log']).read_text(encoding='utf-8-sig') for r in receipts)
    if report.exists():
        assert report.read_text(encoding='utf-8') == merged_text
    else:
        report.write_text(merged_text, encoding='utf-8')
    log_path = folder.with_suffix('.log')
    if log_path.exists():
        assert log_path.read_text(encoding='utf-8-sig') == merged_log
    else:
        log_path.write_text(merged_log, encoding='utf-8')
    state.pop('operation', None)
    state.pop('checkpointed_operations', None)
    state.pop('operation_count', None)
    return dict(index=index, arm=arm, fixture=fixture, quiet_cpu_samples=receipts[0]['quiet_cpu_samples'],
        quiet_build_free_samples=12, build_monitor_observations=sum(r['build_monitor_observations'] for r in receipts),
        report=str(report), sha256=digest(report), cases=len(merged['Benchmarks']),
        operation_checkpoints=receipts)
