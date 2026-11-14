"""Check requested methods against the frozen benchmark program before timing."""
import hashlib
import json
from pathlib import Path
import re
import subprocess
import tempfile

ASSEMBLY = 'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'
PREFIX = 'OoplesFinance.StockIndicators.CompetitorBenchmarks.'


def requested_methods(plan):
    result = {}
    for shard in plan['targeted_shards']:
        for task in shard['tasks']:
            result.setdefault(task['fixture'], set()).update(task['methods'])
    assert result
    return {fixture: sorted(methods) for fixture, methods in sorted(result.items())}


def parse_listing(text, fixture):
    pattern = r'^\s*' + re.escape(PREFIX + fixture + '.') + r'(\w+)\s*$'
    return sorted(set(re.findall(pattern, text, re.M)))


def inspect_methods(runtime, plan, runner=None):
    runtime = Path(runtime).resolve()
    runner = subprocess.check_output if runner is None else runner
    inventories = {}
    for fixture, methods in requested_methods(plan).items():
        # List mode reads the actual BenchmarkDotNet inventory without setup,
        # compilation, or workload execution. Keep incidental files out of inputs.
        with tempfile.TemporaryDirectory(prefix='si-fixture-inventory-') as work:
            assert Path(work).resolve().parent == Path(tempfile.gettempdir()).resolve()
            output = runner(['dotnet', str(runtime / ASSEMBLY), '--list', 'flat',
                             '--filter', '*' + fixture + '*'], cwd=work,
                            stderr=subprocess.STDOUT, timeout=90, text=True)
        available = parse_listing(output, fixture)
        missing = sorted(set(methods) - set(available))
        if missing:
            raise AssertionError(f'Frozen fixture {fixture} lacks requested methods {missing}; available={available}')
        inventories[fixture] = dict(requested=methods, available=available)
    return inventories


def record_inventory(folder, plan, plan_sha256):
    folder = Path(folder)
    data = dict(plan_sha256=plan_sha256,
                benchmark_sha256=hashlib.sha256((folder / 'common' / ASSEMBLY).read_bytes()).hexdigest(),
                fixtures=inspect_methods(folder / 'common', plan))
    raw = (json.dumps(data, indent=2) + '\n').encode()
    (folder / 'fixture-method-inventory.json').write_bytes(raw)
    return hashlib.sha256(raw).hexdigest()


def verify_inventory(folder, plan, proof):
    folder = Path(folder)
    raw = (folder / 'fixture-method-inventory.json').read_bytes()
    assert hashlib.sha256(raw).hexdigest() == proof['fixture_inventory_sha256']
    data = json.loads(raw)
    assert data['plan_sha256'] == proof['plan_sha256']
    assert data['benchmark_sha256'] == hashlib.sha256((folder / 'common' / ASSEMBLY).read_bytes()).hexdigest()
    requested = requested_methods(plan)
    assert set(data['fixtures']) == set(requested)
    for fixture, methods in requested.items():
        row = data['fixtures'][fixture]
        assert row['requested'] == methods
        assert row['available'] == sorted(set(row['available']))
        assert set(methods) <= set(row['available'])
