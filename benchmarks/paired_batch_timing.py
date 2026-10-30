"""Validate Legacy/Shared full-batch statistics; this is not a competitor receipt.

The timing runner must separately verify immutable inputs, runtime execution and
quiet-machine evidence before publishing these rows as qualified measurements.
"""
import math
import re


FIXTURES = {
    'PairedBatchAllocationBenchmarks': (
        'ComparePriceMomentumOscillator', 'RelativeNormalizedVolatility',
        'RelativeStrength3DIndicator', 'SectorRotationModel'),
    'KaufmanStressBatchBenchmarks': ('KaufmanStressIndicator',),
    'RsmkBatchBenchmarks': ('RSMKIndicator',),
    'SectorRotationBatchBenchmarks': ('SectorRotationModel',),
}
BASELINES = {
    'PairedBatchAllocationBenchmarks': 'Original publication loop with the same numerical state',
    'KaufmanStressBatchBenchmarks': 'Preserved original batch and extrema kernel',
    'RsmkBatchBenchmarks': 'Preserved original batch implementation',
    'SectorRotationBatchBenchmarks': 'Original dictionary publication loop with the same numerical state',
}


def numeric(value):
    return type(value) in (int, float) and math.isfinite(value)


def statistics(benchmark):
    stats = benchmark.get('Statistics') or {}
    n = stats.get('N')
    if type(n) is not int or n < 7:
        raise ValueError('At least seven samples are required per arm')
    ci = stats.get('ConfidenceInterval') or {}
    mean, lower, upper = stats.get('Mean'), ci.get('Lower'), ci.get('Upper')
    allocation = (benchmark.get('Memory') or {}).get('BytesAllocatedPerOperation')
    if not all(numeric(v) for v in (mean, lower, upper, allocation)):
        raise ValueError('Missing or nonfinite statistics/allocation')
    if not 0 < lower <= mean <= upper or allocation < 0:
        raise ValueError('Invalid confidence bounds or allocation')
    return dict(mean_ns=mean, lower_ns=lower, upper_ns=upper,
                allocated_bytes=allocation, samples=n)


def collect(data, fixture):
    if fixture not in FIXTURES:
        raise ValueError('Unsupported paired fixture')
    runtime = data.get('HostEnvironmentInfo', {}).get('RuntimeVersion', '')
    if not re.search(r'(?<![\d.])10\.0\.12(?![\d.])', runtime):
        raise ValueError('Expected runtime 10.0.12')
    operations = FIXTURES[fixture]
    groups = {}
    for benchmark in data.get('Benchmarks', []):
        method = benchmark.get('Method')
        if method not in ('Legacy', 'Shared'):
            raise ValueError('Unexpected benchmark arm')
        full = benchmark.get('FullName', '')
        match = re.fullmatch(r'(?:[\w]+\.)*' + re.escape(fixture) + r'\.'
                            + method + r'\((.*)\)', full)
        if not match:
            raise ValueError('Unexpected fixture or method identity')
        parameters = {}
        for token in match[1].split(','):
            part = re.fullmatch(r'\s*(\w+):\s*(.*?)\s*', token)
            if not part or part[1] in parameters:
                raise ValueError('Malformed or duplicate parameters')
            parameters[part[1]] = part[2]
        expected = {'Count', 'Operation'} if len(operations) > 1 else {'Count'}
        if set(parameters) != expected:
            raise ValueError('Unexpected workload parameters')
        if parameters['Count'] not in ('1000', '10000'):
            raise ValueError('Unexpected input size')
        operation = parameters.get('Operation', operations[0]).strip('"')
        if operation not in operations:
            raise ValueError('Unexpected paired operation')
        key = (operation, int(parameters['Count']))
        arms = groups.setdefault(key, {})
        if method in arms:
            raise ValueError('Duplicate arm; do not select the fastest run')
        arms[method] = statistics(benchmark)
    expected_grid = {(op, count) for op in operations for count in (1000, 10000)}
    if set(groups) != expected_grid or any(set(arms) != {'Legacy', 'Shared'} for arms in groups.values()):
        raise ValueError('Incomplete operation/size/arm grid')
    rows = []
    for (operation, count), arms in sorted(groups.items()):
        old, new = arms['Legacy'], arms['Shared']
        verdict = ('faster' if new['upper_ns'] < old['lower_ns'] else
                   'slower' if new['lower_ns'] > old['upper_ns'] else 'inconclusive')
        ratio = old['mean_ns'] / new['mean_ns']
        if not math.isfinite(ratio):
            raise ValueError('Nonfinite time ratio')
        rows.append(dict(fixture=fixture, operation=operation, bars=count,
                         history='full paired batch', legacy=old, shared=new,
                         legacy_over_shared=ratio, verdict=verdict,
                         allocation_saved_bytes=old['allocated_bytes'] - new['allocated_bytes'],
                         role='control' if fixture == 'PairedBatchAllocationBenchmarks'
                         and operation == 'SectorRotationModel' else 'candidate',
                         baseline=BASELINES[fixture]))
    return dict(status='statistics-validated-control-evidence-required',
                comparison='Ooples legacy versus Ooples candidate; no competitor comparison',
                rows=rows)
