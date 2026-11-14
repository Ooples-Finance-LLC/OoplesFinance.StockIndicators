"""Bounded source-bound screen; never qualification or report evidence."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import psutil

ops = Path(__file__).parent
record = ops / 'context-price-prefilter-20261114-draft.json'
q = json.loads(record.read_text())
assert (q['unit_passed'], q['comparator_passed'], q['gpu_passed']) == (16, 147, 100)
repo = Path(q['repo'])
suffix = Path('benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0')
runtimes = {'old': Path(r'C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-star-reversal-certified-difference-20261114') / suffix,
            'new': repo / suffix}
hashes = {'old': '186c441ad23620cfaae3a94e9d10df7f0b46cacc66cdb8fb03860536b00ce7e5',
          'new': q['production_sha256']}
for label, runtime in runtimes.items():
    assert hashlib.sha256((runtime / 'OoplesFinance.StockIndicators.dll').read_bytes()).hexdigest() == hashes[label]
assert not subprocess.check_output(['git', 'status', '--porcelain'], cwd=repo)
assert not subprocess.check_output(['git', 'diff', q['baseline'], 'HEAD', '--', str(suffix.parents[2])], cwd=repo)
out = ops / 'context-sizeguard-20261114-roi'
out.mkdir()
driver = out / 'profile-context-prefilter.ps1'
content = (ops / 'profile-native-gaps-perfview-20261113.ps1').read_text()
needle = 'for(int i=0;i<100;i++)result=run();'
assert content.count(needle) == 1
driver.write_text(content.replace(needle, 'for(int i=0;i<1000;i++)result=run();'))
env = os.environ.copy()
for key in ('DOTNET_JitDisasm', 'DOTNET_JitStdOutFile', 'COMPlus_JitDisasm', 'COMPlus_JitStdOutFile'):
    env.pop(key, None)

def concurrent_builds():
    count = 0
    for process in psutil.process_iter(['name', 'cmdline']):
        try:
            if (process.info['name'] or '').lower() not in ('dotnet', 'dotnet.exe'):
                continue
            if any(arg.lower() in ('build', 'test', 'vstest', 'restore', 'publish', 'pack')
                   for arg in process.info['cmdline'] or []):
                count += 1
        except (psutil.NoSuchProcess, psutil.AccessDenied):
            pass
    return count

def run(label, pair, bars, log, environment):
    args = ['pwsh', '-NoProfile', '-File', str(driver), '-Pair', pair, '-Seconds', '5',
            '-Workers', '4', '-Bars', str(bars), '-Runtime', str(runtimes[label]), '-Expected', hashes[label]]
    with log.open('wb') as output:
        subprocess.run(args, env=environment, stdout=output, stderr=subprocess.STDOUT, check=True)

rounds = []
for pair in ('TaLib.Candles.InvertedHammer', 'TaLib.Candles.ShootingStar', 'TaLib.Candles.Hammer', 'TaLib.Candles.HangingMan'):
    for bars in (1000, 10000):
        for label in ('old1', 'new1', 'new2', 'old2'):
            cpu_before, builds_before = psutil.cpu_percent(interval=1), concurrent_builds()
            log = out / (pair.split('.')[-1] + '-' + str(bars) + '-' + label + '.log')
            run(label[:3], pair, bars, log, env)
            cpu_after, builds_after = psutil.cpu_percent(interval=1), concurrent_builds()
            matches = re.findall(r'PROFILE END iterations=(\d+) seconds=([\d.]+) allocated=(\d+)', log.read_text(encoding='utf-8-sig'))
            assert len(matches) == 1
            batches, seconds, allocated = matches[0]
            row = dict(pair=pair, bars=bars, label=label, mean_batch_ms=1000*float(seconds)/int(batches),
                       batches=int(batches), allocated_bytes=int(allocated), cpu_before=cpu_before,
                       cpu_after=cpu_after, concurrent_builds_before=builds_before, concurrent_builds_after=builds_after,
                       idle_endpoints=cpu_before < 15 and cpu_after < 15 and builds_before == builds_after == 0)
            rounds.append(row)
            print(json.dumps(row), flush=True)
groups = []
for pair in sorted({row['pair'] for row in rounds}):
    for bars in (1000, 10000):
        rows = [row for row in rounds if row['pair'] == pair and row['bars'] == bars]
        old = [row['mean_batch_ms'] for row in rows if row['label'].startswith('old')]
        new = [row['mean_batch_ms'] for row in rows if row['label'].startswith('new')]
        groups.append(dict(pair=pair, bars=bars, separated_diagnostic_means=max(new) < min(old),
                           mean_reduction=1-sum(new)/sum(old), idle_endpoints=all(row['idle_endpoints'] for row in rows)))
jit = out / 'context-matches-jit.txt'
jit_env = dict(env, DOTNET_JitDisasm='OoplesFinance.StockIndicators.Indicators.ContextReversalState:Matches OoplesFinance.StockIndicators.Indicators.ContextReversalState:Update', DOTNET_JitStdOutFile=str(jit))
run('new', 'TaLib.Candles.InvertedHammer', 10000, out / 'jit.log', jit_env)
sections = [section for section in re.split(r'(?=; Assembly listing for method )', jit.read_text())
            if 'ContextReversalState:Matches' in section.split('\n')[0]
            and any(tier in section.split('\n')[0] for tier in ('(Tier1)', '(Tier1-OSR)', '(Tier0-FullOpts)'))]
assert sections, 'Optimized actual predicate method was not observed'
result = dict(source=q['source'], baseline=q['baseline'], hashes=hashes, workers=4, warmups=1000,
              rounds=rounds, groups=groups, optimized_matches_observed=True,
              timing_qualification=False, native_win_evidence=False,
              interpretation='Five-second throughput diagnostics, not confidence intervals or report evidence. Endpoint CPU/build checks do not certify continuous host idleness. Full source composition and unchanged qualification gates remain necessary.')
(out / 'verification.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(groups), flush=True)
