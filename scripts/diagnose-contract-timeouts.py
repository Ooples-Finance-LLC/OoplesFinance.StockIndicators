"""Replay only the two timed-out shards from immutable hosted binaries."""
import datetime
import importlib.util
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

ROOT = Path(__file__).resolve().parents[1]
SOURCE = 'aa624cf480de63594820988f391b91edaf9f3f7e'
RUN = 39600484300
REPO = 'Ooples-Finance-LLC/OoplesFinance.StockIndicators'
spec = importlib.util.spec_from_file_location('contracts', ROOT / 'scripts/run-competitor-contracts.py')
contracts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(contracts)


def partition(methods):
    affected = sorted(contracts.assignment(methods, 9, 20) + contracts.assignment(methods, 10, 20))
    isolated = [contracts.PREFIX + 'RemainingVervoortModifiedBenchmarkTests.Modified' + mode + '10000'
                for mode in ['Full', 'Latest']]
    assert len(affected) == len(set(affected)) == 229 and all(n in affected for n in isolated)
    buckets = [[n] for n in isolated] + [[] for _ in range(18)]
    loads = [0] * 18
    for name in sorted(set(affected) - set(isolated), key=lambda n: (-contracts.HISTORICAL_SECONDS.get(n, 1), n)):
        target = min(range(18), key=lambda i: (loads[i], i))
        buckets[target + 2].append(name)
        loads[target] += contracts.HISTORICAL_SECONDS.get(name, 1)
    assert sorted(n for bucket in buckets for n in bucket) == affected
    return buckets


def main(shard):
    run = json.loads(subprocess.check_output(['gh', 'api', f'repos/{REPO}/actions/runs/{RUN}'], encoding='utf-8'))
    assert run['head_sha'] == SOURCE and run['id'] == RUN
    methods = json.loads((ROOT / 'contracts/contract-methods.json').read_text(encoding='utf-8'))
    assert len(methods) == 1717 and methods == sorted(set(methods))
    selected = partition(methods)[shard]
    dll = ROOT / 'contracts/benchmarks/OoplesFinance.StockIndicators.CompetitorTests/bin/Release/net10.0/OoplesFinance.StockIndicators.CompetitorTests.dll'
    output = ROOT / f'diagnostic-{shard}'
    output.mkdir(exist_ok=False)
    state = dict(source_sha=SOURCE, binary_run_id=RUN, shard=shard, expected=selected, results=[])

    def save():
        state['updated_utc'] = datetime.datetime.now(datetime.timezone.utc).isoformat()
        temporary = output / 'status.tmp'
        temporary.write_text(json.dumps(state, indent=2) + '\n', encoding='utf-8')
        temporary.replace(output / 'status.json')

    for index, name in enumerate(selected):
        state.update(status='running', active_method=name)
        save()
        print('START ' + name, flush=True)
        directory = output / str(index)
        directory.mkdir()
        started = time.monotonic()
        with (directory / 'console.log').open('wb') as log:
            child = subprocess.Popen(['dotnet', 'vstest', str(dll), '/TestCaseFilter:FullyQualifiedName=' + name,
                '/Logger:console;Verbosity=normal', '/Logger:trx;LogFileName=contracts.trx',
                '/ResultsDirectory:' + str(directory)], stdout=log, stderr=subprocess.STDOUT, start_new_session=True)
            try:
                result = child.wait(timeout=2400)
            except subprocess.TimeoutExpired:
                os.killpg(child.pid, signal.SIGTERM)
                child.wait(timeout=20)
                state.update(status='method-timeout', elapsed_seconds=time.monotonic() - started)
                save()
                raise
        assert result == 0, (name, result)
        count = contracts.verify_trx(directory / 'contracts.trx', [name])
        state['results'].append(dict(method=name, cases=count, seconds=time.monotonic() - started))
        save()
        print('PASS ' + name + ' cases=' + str(count), flush=True)
    state.update(status='all-assigned-methods-passed', active_method=None)
    save()


if __name__ == '__main__':
    main(int(sys.argv[1]))
