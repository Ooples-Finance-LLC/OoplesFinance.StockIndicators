import gzip
import hashlib
import itertools
import json
import math
import re
import runpy
import shutil
import sys
from pathlib import Path

temp = Path('Z:/StockIndicators-work/si-followon-performance-20261018')
stage, = [s for s in json.loads((temp / 'si-remaining-timing-stages.json').read_text()) if s['name'] == sys.argv[1]]
assert re.fullmatch(r'si-[a-z0-9-]+', sys.argv[2])
source = temp / sys.argv[2]
snapshot = Path(stage['snapshot'])
manifest = json.loads((snapshot / 'manifest.json').read_text())
for name, digest in manifest['files_sha256'].items():
    assert hashlib.sha256((snapshot / name).read_bytes()).hexdigest() == digest, name
destination = Path(stage['destination'])
assert not destination.exists(), 'Never overwrite archived or partial evidence'
state = json.loads(Path(str(source) + '.exit').read_text(encoding='utf-8-sig'))
assert state['ExitCode'] == 0 and not state['Interrupted']
pid = int(Path(str(source) + '.pid').read_text())
samples = [json.loads(line) for line in Path(str(source) + '.processes.jsonl').read_text(encoding='utf-8-sig').splitlines() if line]
runpy.run_path(str(temp / 'verify-si-quiet-evidence.py'))['verify_quiet_evidence'](source, pid, samples)
report_path, = (source / 'results').glob('*' + stage['fixture'] + '*full-compressed.json')
report = json.loads(report_path.read_text(encoding='utf-8-sig'))
assert '10.0.12' in report['HostEnvironmentInfo']['RuntimeVersion']
rows = {}
for row in report['Benchmarks']:
    name = row['FullName']
    assert stage['fixture'] + '.' in name
    key = (re.search(r'Operation: "([^"]+)"', name)[1],
           int(re.search(r'Count: (\d+)', name)[1]),
           re.search(r'History: (Full|LatestOnly)', name)[1], row['Method'])
    assert key not in rows
    statistics = row['Statistics']
    assert statistics and statistics['N'] >= 7
    mean = statistics['Mean']
    interval = statistics['ConfidenceInterval']
    allocation = row['Memory']['BytesAllocatedPerOperation']
    assert math.isfinite(mean) and mean > 0 and math.isfinite(allocation) and allocation >= 0
    assert math.isfinite(interval['Lower']) and math.isfinite(interval['Upper'])
    assert 0 < interval['Lower'] <= mean <= interval['Upper']
    rows[key] = dict(mean_ns=mean, confidence_interval=interval, allocated_bytes=allocation)
expected = set(itertools.product(stage['operations'], (1000, 10000), ('Full', 'LatestOnly'), ('Legacy', 'Shared')))
assert set(rows) == expected, 'Missing or extra benchmark cases'
comparisons = []
for operation, count, history in itertools.product(stage['operations'], (1000, 10000), ('Full', 'LatestOnly')):
    legacy = rows[operation, count, history, 'Legacy']
    shared = rows[operation, count, history, 'Shared']
    comparisons.append(dict(operation=operation, count=count, history=history, legacy=legacy, shared=shared,
                            shared_to_legacy_ratio=shared['mean_ns'] / legacy['mean_ns']))
destination.mkdir(parents=True)
shutil.copyfile(snapshot / 'manifest.json', destination / 'preserved-candidate-manifest.json')
shutil.copyfile(report_path, destination / 'report-full-compressed.json')
for suffix in ('.log', '.stderr.log', '.processes.jsonl'):
    (destination / (suffix[1:] + '.gz')).write_bytes(gzip.compress(Path(str(source) + suffix).read_bytes(), mtime=0))
for suffix, name in (('.exit', 'runner-exit.json'), ('.quiet.json', 'quiet-preflight.json')):
    shutil.copyfile(Path(str(source) + suffix), destination / name)
for name in ('si-quiet-process-monitor.ps1', 'verify-si-quiet-evidence.py', 'verify-si-remaining-snapshot.py',
             'record-si-remaining-timing.py', 'run-si-remaining-' + stage['name'] + '.ps1'):
    shutil.copyfile(temp / name, destination / name)
(destination / 'summary.json').write_text(json.dumps(dict(candidate_base_commit=manifest['commit'], candidate_input_sha256=manifest['input_sha256'], commit_is_base_only=manifest['commit_is_base_only'],
    comparisons=comparisons, interpretation='Same-library legacy/shared measurements from the exact preserved working candidate. The base commit alone does not identify the candidate. Inherited MACD failures remain. Review before performance qualification; no native competitor timings collected.'), indent=2) + '\n')
(destination / 'archive-sha256.json').write_text(json.dumps({p.name: hashlib.sha256(p.read_bytes()).hexdigest()
    for p in destination.iterdir() if p.is_file()}, indent=2) + '\n')
print('Archived', len(rows), 'cases; performance review remains required.')
