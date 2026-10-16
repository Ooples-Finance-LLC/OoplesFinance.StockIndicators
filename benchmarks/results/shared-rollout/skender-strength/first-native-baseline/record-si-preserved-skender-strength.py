from pathlib import Path
import gzip, hashlib, json, re, shutil, subprocess, sys

root = Path('C:/Users/cheat/source/repos/Ooples-Finance-LLC/si-shared-rollout')
snapshot = Path('C:/Users/cheat/temp/si-skender-common-2e533ff2-preserved')
snapshot_manifest = json.loads((snapshot / 'manifest.json').read_text())
assert snapshot_manifest['base_commit'].startswith('2e533ff2')
for relative, digest in snapshot_manifest['files_sha256'].items():
    assert hashlib.sha256((snapshot / relative).read_bytes()).hexdigest() == digest, relative
source = Path('C:/Users/cheat/temp') / sys.argv[1]
destination = root / 'benchmarks/results/shared-rollout/skender-strength/first-native-baseline'
assert not destination.exists(), 'Never overwrite recorded evidence'
state = json.loads(Path(str(source) + '.exit').read_text(encoding='utf-8-sig'))
assert state['ExitCode'] == 0 and not state['Interrupted'], state
pid = int(Path(str(source) + '.pid').read_text(encoding='utf-8-sig'))
samples = [json.loads(line) for line in Path(str(source) + '.processes.jsonl').read_text(encoding='utf-8-sig').splitlines() if line]
assert len(samples) >= 3
import runpy
runpy.run_path('C:/Users/cheat/temp/verify-si-quiet-evidence.py')['verify_quiet_evidence'](source, pid, samples)
report_path, = (source / 'results').glob('*SharedSkenderStrengthBenchmarks*full-compressed.json')
report = json.loads(report_path.read_text(encoding='utf-8-sig'))
assert '10.0.12' in report['HostEnvironmentInfo']['RuntimeVersion']
rows = {}
for row in report['Benchmarks']:
    full = row['FullName']
    assert 'SharedSkenderStrengthBenchmarks' in full
    count = int(re.search(r'Count: (\d+)', full).group(1))
    operation = next(op for op in ('Skender.GetRsi', 'Skender.GetAdx') if op in full)
    key = (operation, count, row['Method'])
    assert key not in rows
    stats = row['Statistics']
    assert stats and stats['N'] >= 7 and stats['Mean'] > 0
    rows[key] = {'operation': operation, 'bars': count, 'method': row['Method'], 'mean_ns': stats['Mean'], 'confidence_interval': stats['ConfidenceInterval'], 'allocated_bytes': row['Memory']['BytesAllocatedPerOperation']}
assert set(rows) == {(op,n,m) for op in ('Skender.GetRsi','Skender.GetAdx') for n in (1000,10000) for m in ('NativeSingle','NativeParallel','BuilderFull','BuilderLatestOnly')}
summary = []
for op in ('Skender.GetRsi','Skender.GetAdx'):
    for n in (1000,10000):
        native = min((rows[op,n,m] for m in ('NativeSingle','NativeParallel')), key=lambda row: row['mean_ns'])
        for method in ('BuilderFull','BuilderLatestOnly'):
            row = rows[op,n,method]
            summary.append({**row, 'fastest_native': native, 'same_run_native_mean_ratio': row['mean_ns']/native['mean_ns'], 'intervals_separated_in_builder_favor': row['confidence_interval']['Upper'] < native['confidence_interval']['Lower']})
destination.mkdir(parents=True)
shutil.copyfile(snapshot / 'manifest.json', destination / 'preserved-candidate-manifest.json')
(destination / 'native-baseline.json.gz').write_bytes(gzip.compress(report_path.read_bytes(),mtime=0))
for suffix in ('.log','.stderr.log','.processes.jsonl'):
    (destination / (suffix[1:]+'.gz')).write_bytes(gzip.compress(Path(str(source)+suffix).read_bytes(),mtime=0))
for suffix,name in (('.exit','runner-exit.json'),('.quiet.json','quiet-preflight.json')):
    shutil.copyfile(Path(str(source)+suffix),destination/name)
for helper in ('si-quiet-process-monitor.ps1','verify-si-quiet-evidence.py','run-si-preserved-skender-strength.ps1','record-si-preserved-skender-strength.py'):
    shutil.copyfile(Path('C:/Users/cheat/temp')/helper,destination/helper)
(destination/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
(destination/'provenance.json').write_text(json.dumps({'base_commit':snapshot_manifest['base_commit'],'purpose':'First direct-native serial and full-prefix parallel baselines; preserve once and reuse. All four arms measured in one run. Catalog promotion requires separate review.','monitor_limitations':'Process identity and CPU samples cannot exclude brief unobserved activity or non-CPU contention.','validation':'19 comparator and 100 GPU cases passed, with all 356 affected cases passing again after shared native support consolidation; no production source changes.'},indent=2)+'\n')
print('Recorded 16 cases; review qualification before catalog promotion.')
