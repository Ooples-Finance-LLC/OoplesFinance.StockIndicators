"""Validate and archive completed full paired-batch timings.

Pass --snapshot for the immutable qualified snapshot, --source for the guarded
runner artifact directory/stem, --destination for a new evidence directory, and
--verifier for the existing verify-si-quiet-evidence.py module. These tools never launch benchmarks.

Requires the preserved source/binary hashes, clean runner exit, measured-process
identity, 240 seconds of quiet evidence, and complete timing configurations.
Allocation comparisons are not builder history-mode or native-competitor results.
"""
from pathlib import Path
import argparse
import datetime
import gzip
import hashlib
import itertools
import json
import math
import re
import runpy
import shutil

FIXTURE = 'PairedBatchAllocationBenchmarks'
OPERATIONS = ('ComparePriceMomentumOscillator', 'RelativeNormalizedVolatility',
              'RelativeStrength3DIndicator', 'SectorRotationModel')


def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def collect(report):
    if '10.0.12' not in report['HostEnvironmentInfo']['RuntimeVersion']:
        raise ValueError('Unexpected benchmark runtime')
    rows = {}
    for row in report['Benchmarks']:
        name = row['FullName']
        if FIXTURE + '.' not in name or 'History:' in name:
            raise ValueError('Wrong fixture or invented history mode')
        operation = re.search(r'Operation: "([^"]+)"', name)
        count = re.search(r'Count: (\d+)', name)
        if not operation or not count:
            raise ValueError('Missing operation/count')
        key = (operation[1], int(count[1]), row['Method'])
        if key in rows:
            raise ValueError('Duplicate benchmark arm')
        stats = row.get('Statistics')
        if not stats or stats['N'] < 7:
            raise ValueError('Insufficient samples')
        mean = stats['Mean']
        interval = stats['ConfidenceInterval']
        allocation = row['Memory']['BytesAllocatedPerOperation']
        if not all(math.isfinite(v) for v in [mean, interval['Lower'], interval['Upper'], allocation]):
            raise ValueError('Nonfinite measurement')
        if not 0 < interval['Lower'] <= mean <= interval['Upper'] or allocation < 0:
            raise ValueError('Invalid measurement bounds')
        rows[key] = dict(mean_ns=mean, confidence_interval=interval, allocated_bytes=allocation, samples=stats['N'])
    expected = set(itertools.product(OPERATIONS, (1000, 10000), ('Legacy', 'Shared')))
    if set(rows) != expected:
        raise ValueError('Missing or extra benchmark configurations')
    result = []
    for operation, count in itertools.product(OPERATIONS, (1000, 10000)):
        legacy = rows[operation, count, 'Legacy']
        candidate = rows[operation, count, 'Shared']
        a, b = legacy['confidence_interval'], candidate['confidence_interval']
        verdict = 'faster' if b['Upper'] < a['Lower'] else 'slower' if b['Lower'] > a['Upper'] else 'inconclusive'
        result.append(dict(operation=operation, count=count, history='FullPairedBatch',
                           control=operation == 'SectorRotationModel', legacy=legacy, shared=candidate,
                           shared_to_legacy_ratio=candidate['mean_ns'] / legacy['mean_ns'], verdict=verdict))
    return result


def validate_quiet(source, pid, samples, verifier):
    quiet = read(Path(str(source) + '.quiet.json'))
    timestamp = lambda row: datetime.datetime.fromisoformat(row['Time'].replace('Z', '+00:00'))
    if len(quiet) < 5 or (timestamp(quiet[-1]) - timestamp(quiet[0])).total_seconds() < 240:
        raise ValueError('Missing 240-second quiet window')
    # Reuse the established identity, activity, freshness, and ownership checks.
    verifier(source, pid, samples)


def archive(snapshot, source, destination, verifier_path):
    if destination.exists():
        raise ValueError('Never overwrite prior or partial evidence')
    manifest = read(snapshot / 'manifest.json')
    for name, expected in manifest['files_sha256'].items():
        if sha(snapshot / name) != expected:
            raise ValueError('Snapshot hash mismatch: ' + name)
    state = read(Path(str(source) + '.exit'))
    if state['ExitCode'] != 0 or state['Interrupted']:
        raise ValueError('Benchmark did not finish cleanly')
    pid = int(Path(str(source) + '.pid').read_text(encoding='utf-8-sig'))
    samples = [json.loads(line) for line in Path(str(source) + '.processes.jsonl').read_text(encoding='utf-8-sig').splitlines() if line]
    verifier = runpy.run_path(str(verifier_path))['verify_quiet_evidence']
    validate_quiet(source, pid, samples, verifier)
    commands = [p['CommandLine'] for row in samples for p in row['Processes']
                if p['ProcessId'] == pid and p['CommandLine']]
    if not commands or not all(snapshot.as_posix().lower() in c.replace('\\', '/').lower() for c in commands):
        raise ValueError('Measured process does not identify the preserved snapshot')
    reports = list((source / 'results').glob('*' + FIXTURE + '*full-compressed.json'))
    if len(reports) != 1:
        raise ValueError('Expected exactly one raw benchmark report')
    comparisons = collect(read(reports[0]))
    # Require all evidence before creating the archive.
    paths = {suffix: Path(str(source) + suffix) for suffix in
             ('.log', '.stderr.log', '.processes.jsonl', '.exit', '.quiet.json', '.pid')}
    for path in paths.values():
        if not path.is_file():
            raise ValueError('Missing runner evidence: ' + str(path))
    destination.mkdir(parents=True, exist_ok=False)
    shutil.copyfile(snapshot / 'manifest.json', destination / 'preserved-candidate-manifest.json')
    shutil.copyfile(reports[0], destination / 'report-full-compressed.json')
    shutil.copyfile(verifier_path, destination / verifier_path.name)
    shutil.copyfile(Path(__file__), destination / 'record_paired_batch_performance.py')
    for suffix, path in paths.items():
        if suffix in ('.log', '.stderr.log', '.processes.jsonl'):
            (destination / (suffix[1:] + '.gz')).write_bytes(gzip.compress(path.read_bytes(), mtime=0))
        else:
            shutil.copyfile(path, destination / ('runner' + suffix))
    summary = dict(candidate_base_commit=manifest['commit'], candidate_input_sha256=manifest['input_sha256'],
                   comparisons=comparisons, fixture=FIXTURE,
                   interpretation='Full paired batches versus the preserved original loop; Sector Rotation is an unchanged control. No builder LatestOnly or native competitor measurement is represented.')
    (destination / 'summary.json').write_text(json.dumps(summary, indent=2) + '\n', encoding='utf-8')
    (destination / 'archive-sha256.json').write_text(json.dumps({p.name: sha(p) for p in destination.iterdir() if p.is_file()}, indent=2) + '\n', encoding='utf-8')
    return summary


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    for name in ('snapshot', 'source', 'destination', 'verifier'):
        parser.add_argument('--' + name, type=Path, required=True)
    args = parser.parse_args()
    result = archive(args.snapshot, args.source, args.destination, args.verifier)
    print('Archived', len(result['comparisons']), 'paired comparisons; review remains required.')
