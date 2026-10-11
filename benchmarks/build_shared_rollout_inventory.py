"""Index saved overlap evidence without treating normalized adapters as native wins."""
import argparse
import gzip
import json
import math
from pathlib import Path
import re


def inventory(root: Path, qualifications=()):
    manifest = json.loads((root / 'competitor-library-manifest.json').read_text(encoding='utf-8-sig'))
    pairs = {row['Id']: row for row in manifest if row['Status'] == 'paired'}
    measurements = {}
    for path in sorted((root / 'results/2026-10-06-library-comparisons').glob('*.json.gz')):
        with gzip.open(path, 'rt', encoding='utf-8-sig') as stream:
            report = json.load(stream)
        for row in report['Benchmarks']:
            # BDN abbreviates Parameters; FullName retains the actual API identifier.
            match = re.search(r'PairId: "([^"]+)"', row['FullName'])
            if not match or match[1] not in pairs:
                raise ValueError(f'Unmapped benchmark: {path.name}: {row["FullName"]}')
            bars = re.search(r'Bars: (\d+)', row['FullName'])
            if not bars:
                raise ValueError(f'Missing Bars: {path.name}: {row["FullName"]}')
            count = int(bars[1])
            key = (match[1], count, row['Method'])
            if key in measurements:
                raise ValueError(f'Duplicate measurement: {key}')
            mean = row['Statistics']['Mean']
            allocation = row['Memory']['BytesAllocatedPerOperation']
            if not math.isfinite(mean) or mean <= 0 or allocation <= 0:
                raise ValueError(f'Invalid measurement: {key}')
            measurements[key] = dict(mean_ns=mean, allocated_bytes=allocation,
                                     evidence=str(path.relative_to(root)).replace('\\', '/'))
    rows = []
    for pair_id, pair in sorted(pairs.items()):
        sizes = []
        for count in (1000, 10000):
            for method in ('Ooples', 'Competitor'):
                if (pair_id, count, method) not in measurements:
                    raise ValueError(f'Missing measurement: {pair_id}, bars={count}, method={method}')
            ours = measurements[(pair_id, count, 'Ooples')]
            theirs = measurements[(pair_id, count, 'Competitor')]
            sizes.append(dict(bars=count, saved_ooples=ours, saved_competitor=theirs,
                              saved_time_ratio=ours['mean_ns'] / theirs['mean_ns']))
        rows.append(dict(pair_id=pair_id, indicator_mapping=pair['OoplesIndicator'],
                         outputs=pair['Outputs'],
                         qualification='pending-native-builder-comparison', measurements=sizes))
    if len(measurements) != len(rows) * 4:
        raise ValueError('Unexpected measurement count')
    indexed = {row['pair_id']: row for row in rows}
    pilot_ids = ('QuanTAlib.Jma', 'QuanTAlib.Atr', 'Skender.GetRollingPivots', 'Skender.GetFractal',
                 'TaLib.Candles.RickshawMan', 'TaLib.Functions.Asin', 'Trady.Candlestick.BullishShortDay',
                 'Trady.Indicator.SimpleMovingAverage', 'Skender.GetSma', 'TaLib.Functions.Sma', 'QuanTAlib.Sma')
    for pair_id in pilot_ids:
        if pair_id not in indexed:
            raise ValueError(f'Pilot ID missing from paired manifest: {pair_id}')
        indexed[pair_id]['qualification'] = 'prior-pilot-evidence'
        indexed[pair_id]['prior_evidence'] = 'results/eight-cpu-pilots'
    qualified_keys = set()
    for path in qualifications:
        opener = gzip.open if path.suffix == '.gz' else open
        with opener(path, 'rt', encoding='utf-8-sig') as stream:
            qualified = json.load(stream)
        cases = {}
        for row in qualified['Benchmarks']:
            prefix = 'TaLib.Functions.'
            operation = re.search(r'Operation: "([^"]+)"', row['FullName'])
            count = re.search(r'Count: (\d+)', row['FullName'])
            if operation and any(name in row['FullName'] for name in
                                 ('SharedPointwiseBenchmarks.', 'SharedRegressionBenchmarks.', 'SharedPairInputBenchmarks.',
                                  'SharedPriceProjectionBenchmarks.', 'SharedGeneratedStateBenchmarks.',
                                  'SharedRecursiveStateBenchmarks.', 'SharedLaggedChangeBenchmarks.')):
                api = operation[1]
            elif 'SharedDispersionBenchmarks.' in row['FullName']:
                variance = re.search(r'Variance: (True|False)', row['FullName'])
                if not variance:
                    raise ValueError(f'Unknown dispersion format: {row["FullName"]}')
                api = 'Var' if variance[1] == 'True' else 'StdDev'
            elif 'SharedRollingSumBenchmarks.' in row['FullName']:
                api = 'Sum'
            elif 'SharedEngulfingBenchmarks.' in row['FullName']:
                api = 'Engulfing'
                prefix = 'TaLib.Candles.'
            else:
                raise ValueError(f'Unknown qualification format: {row["FullName"]}')
            if not count:
                raise ValueError(f'Missing count: {row["FullName"]}')
            key = (prefix + api, int(count[1]))
            methods = cases.setdefault(key, {})
            if row['Method'] in methods:
                raise ValueError(f'Duplicate qualification: {key}: {row["Method"]}')
            mean = row['Statistics']['Mean']
            allocation = row['Memory']['BytesAllocatedPerOperation']
            if not math.isfinite(mean) or mean <= 0 or allocation <= 0:
                raise ValueError(f'Invalid qualification: {key}')
            methods[row['Method']] = dict(mean_ns=mean, allocated_bytes=allocation,
                                         confidence_interval=row['Statistics']['ConfidenceInterval'])
        for (pair_id, count), methods in sorted(cases.items()):
            if pair_id not in indexed:
                raise ValueError(f'Unknown qualified workload: {pair_id}/{count}: {path}')
            if (pair_id, count) in qualified_keys:
                raise ValueError(f'Duplicate qualified workload: {pair_id}/{count}')
            qualified_keys.add((pair_id, count))
            required = {'BuilderFull', 'BuilderLatestOnly', 'NativeSingle', 'NativeParallel'}
            if set(methods) != required:
                raise ValueError(f'Incomplete qualification: {pair_id}/{count}')
            best = min(('NativeSingle', 'NativeParallel'), key=lambda name: methods[name]['mean_ns'])
            native_lower = min(methods[name]['confidence_interval']['Lower'] for name in ('NativeSingle', 'NativeParallel'))
            resolved = path.resolve()
            try:
                evidence = resolved.relative_to(root.resolve()).as_posix()
            except ValueError:
                # External files have no portable benchmark-root-relative name.
                # A file URI stays absolute when the inventory itself is moved.
                evidence = resolved.as_uri()
            result = dict(bars=count, evidence=evidence, methods=methods, fastest_measured_native=best,
                          full_mean_ratio=methods['BuilderFull']['mean_ns'] / methods[best]['mean_ns'],
                          latest_mean_ratio=methods['BuilderLatestOnly']['mean_ns'] / methods[best]['mean_ns'],
                          full_clear_win=methods['BuilderFull']['confidence_interval']['Upper'] < native_lower,
                          latest_clear_win=methods['BuilderLatestOnly']['confidence_interval']['Upper'] < native_lower)
            indexed[pair_id].setdefault('current', []).append(result)
            indexed[pair_id]['qualification'] = 'measured-against-serial-and-eight-worker-native'
    return dict(paired_apis=len(rows), indicator_mappings=len({r['indicator_mapping'] for r in rows}),
                measured_current_apis=sum('current' in row for row in rows),
                note='Evidence paths are relative to the benchmarks root; external qualification reports use absolute file URIs. '
                     'Historical normalized-adapter results are prioritization evidence, not raw native or current builder wins. '
                     'A mapping can contain multiple operations/startup conventions; do not collapse it into one formula. '
                     'Clear wins use nonoverlapping reported confidence intervals against both tested native methods; '
                     'other worker budgets and workloads still require qualification. Contracts are in competitor-library-manifest.json.',
                pairs=rows)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--qualification', type=Path, action='append', default=[])
    args = parser.parse_args()
    result = inventory(Path(__file__).resolve().parent, args.qualification)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    if args.output.suffix == '.gz':
        with gzip.open(args.output, 'wt', encoding='utf-8') as stream:
            json.dump(result, stream, indent=2)
    else:
        args.output.write_text(json.dumps(result, indent=2) + '\n', encoding='utf-8')
    print(f"Indexed {result['paired_apis']} paired APIs / {result['indicator_mappings']} mappings; all four saved rows per API verified.")
    print(f"Current native/builder measurements: {result['measured_current_apis']} APIs.")
