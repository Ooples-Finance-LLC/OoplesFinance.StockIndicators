"""Review saved legacy/shared timings without running or qualifying benchmarks.

Interval comparisons use the recorded individual-arm confidence intervals.
They are not a confidence interval for the ratio. Native-competitor qualification
and correctness gates remain separate requirements.
"""
import argparse
import hashlib
import itertools
import json
import math
import re
from pathlib import Path


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def arm(value):
    mean = value['mean_ns']
    allocation = value['allocated_bytes']
    interval = value['confidence_interval']
    lower, upper = interval['Lower'], interval['Upper']
    assert all(math.isfinite(x) for x in (mean, allocation, lower, upper))
    assert 0 < lower <= mean <= upper and allocation >= 0
    assert interval['N'] >= 7
    return mean, allocation, lower, upper


def compare(value):
    legacy, legacy_bytes, legacy_low, legacy_high = arm(value['legacy'])
    shared, shared_bytes, shared_low, shared_high = arm(value['shared'])
    ratio = shared / legacy
    assert math.isclose(ratio, value['shared_to_legacy_ratio'], rel_tol=1e-12)
    if shared_high < legacy_low:
        result = 'shared-faster-separated-intervals'
    elif shared_low > legacy_high:
        result = 'shared-slower-separated-intervals'
    else:
        result = 'overlapping-intervals'
    return {key: value[key] for key in ('operation', 'count', 'history')} | {
        'latency_result': result,
        'shared_to_legacy_mean_ratio': ratio,
        'mean_latency_delta_ns': shared - legacy,
        'allocation_delta_bytes': shared_bytes - legacy_bytes,
        'shared_to_legacy_allocation_ratio': shared_bytes / legacy_bytes if legacy_bytes else None,
        'legacy': value['legacy'], 'shared': value['shared'],
    }


def review_stage(stage):
    destination = Path(stage['destination'])
    if not destination.exists():
        return {'stage': stage['name'], 'status': 'pending', 'comparisons': []}
    archive_path = destination / 'archive-sha256.json'
    assert archive_path.is_file(), 'Partial archive: inspect; do not restart timing'
    hashes = json.loads(archive_path.read_text())
    required = {'summary.json', 'report-full-compressed.json', 'preserved-candidate-manifest.json'}
    assert required <= hashes.keys()
    for name, expected in hashes.items():
        assert Path(name).name == name, 'Archive entry must be a basename'
        assert digest(destination / name) == expected, ('Archive bytes changed', name)
    candidate_path = Path(stage['snapshot']) / 'manifest.json'
    assert (destination / 'preserved-candidate-manifest.json').read_bytes() == candidate_path.read_bytes()
    candidate = json.loads(candidate_path.read_text())
    summary = json.loads((destination / 'summary.json').read_text())
    if 'candidate_commit' in summary:
        assert stage['name'] == 'wilder', 'Unknown legacy archive schema'
        assert summary['candidate_commit'] == candidate['commit']
        assert 'candidate_base_commit' not in summary
        input_hash, base_only = None, None
    else:
        assert summary['candidate_base_commit'] == candidate['commit']
        assert summary['candidate_input_sha256'] == candidate['input_sha256']
        assert summary['commit_is_base_only'] == candidate['commit_is_base_only']
        input_hash, base_only = candidate['input_sha256'], candidate['commit_is_base_only']
    comparisons = summary['comparisons']
    keys = [(v['operation'], v['count'], v['history']) for v in comparisons]
    expected = set(itertools.product(stage['operations'], (1000, 10000), ('Full', 'LatestOnly')))
    assert len(keys) == len(set(keys)) and set(keys) == expected, 'Case matrix differs'
    report = json.loads((destination / 'report-full-compressed.json').read_text(encoding='utf-8-sig'))
    assert '10.0.12' in report['HostEnvironmentInfo']['RuntimeVersion']
    raw = {}
    for row in report['Benchmarks']:
        full = row['FullName']
        assert stage['fixture'] + '.' in full
        key = (re.search(r'Operation: "([^"]+)"', full)[1],
               int(re.search(r'Count: (\d+)', full)[1]),
               re.search(r'History: (Full|LatestOnly)', full)[1], row['Method'])
        assert key not in raw
        statistics = row['Statistics']
        assert statistics['N'] >= 7
        raw[key] = {'mean_ns': statistics['Mean'],
                    'confidence_interval': statistics['ConfidenceInterval'],
                    'allocated_bytes': row['Memory']['BytesAllocatedPerOperation']}
        arm(raw[key])
    assert set(raw) == {key + (method,) for key in expected for method in ('Legacy', 'Shared')}
    for value in comparisons:
        key = (value['operation'], value['count'], value['history'])
        for method in ('Legacy', 'Shared'):
            assert value[method.lower()] == raw[key + (method,)], 'Summary differs from raw report'
    rows = [compare(value) for value in comparisons]
    return {'stage': stage['name'], 'status': 'reviewed-same-library-only',
        'archive_sha256': digest(archive_path),
        'candidate_base_commit': candidate['commit'],
        'candidate_input_sha256': input_hash,
        'commit_is_base_only': base_only,
        'candidate_manifest_sha256': digest(candidate_path),
        'comparisons': rows}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stages', type=Path)
    parser.add_argument('--out', type=Path)
    args = parser.parse_args()
    stages = json.loads(args.stages.read_text())
    assert len({s['name'] for s in stages}) == len(stages), 'Duplicate stage names'
    results = []
    for stage in stages:
        try:
            results.append(review_stage(stage))
        except (AssertionError, KeyError, ValueError, OSError) as error:
            results.append({'stage': stage['name'], 'status': 'needs-inspection',
                            'error': str(error), 'comparisons': []})
    rows = [dict(stage=s['stage'], **row) for s in results for row in s['comparisons']]
    concerns = [row for row in rows if row['latency_result'] != 'shared-faster-separated-intervals'
                or row['allocation_delta_bytes'] > 0]
    concerns.sort(key=lambda row: (-row['shared_to_legacy_mean_ratio'], -row['allocation_delta_bytes']))
    counts = {status: sum(s['status'] == status for s in results)
              for status in ('pending', 'needs-inspection', 'reviewed-same-library-only')}
    output = {'stage_counts': counts, 'measured_comparisons': len(rows),
        'separated_latency_losses': sum(r['latency_result'] == 'shared-slower-separated-intervals' for r in rows),
        'overlapping_intervals': sum(r['latency_result'] == 'overlapping-intervals' for r in rows),
        'allocation_regressions': sum(r['allocation_delta_bytes'] > 0 for r in rows),
        'qualification': 'Not qualified. These are legacy/shared comparisons only; native baselines, correctness, and required hosted checks remain separate.',
        'stages': results, 'review_priorities': concerns}
    if args.out:
        with args.out.open('x') as stream:
            json.dump(output, stream, indent=2)
            stream.write('\n')
    print(json.dumps({k: v for k, v in output.items() if k not in ('stages', 'review_priorities')}))
    if counts['needs-inspection']:
        for result in results:
            if result['status'] == 'needs-inspection':
                print(json.dumps(result))
        raise SystemExit(2)


if __name__ == '__main__':
    main()
