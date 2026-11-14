"""Check JIT log ordering around ten calls after 20,000 warmups; no timing verdict."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess

ops = Path(__file__).parent
q = json.loads((ops / 'context-price-prefilter-20261114-draft.json').read_text())
suffix = Path('benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/bin/Release/net10.0')
runtimes = {'old': Path(r'C:\Users\cheat\source\repos\Ooples-Finance-LLC\si-star-reversal-certified-difference-20261114') / suffix,
            'new': Path(q['repo']) / suffix}
hashes = {'old': '186c441ad23620cfaae3a94e9d10df7f0b46cacc66cdb8fb03860536b00ce7e5', 'new': q['production_sha256']}
for label, runtime in runtimes.items():
    assert hashlib.sha256((runtime / 'OoplesFinance.StockIndicators.dll').read_bytes()).hexdigest() == hashes[label]
out = ops / 'context-prefilter-long-warmup-jit-20261114'
out.mkdir()
driver = out / 'ten-calls-after-warmup.ps1'
content = (ops / 'context-price-prefilter-20261114-roi/profile-context-prefilter.ps1').read_text()
needle = 'while(timer.Elapsed.TotalSeconds<seconds){result=run();count++;}'
assert content.count(needle) == 1
replacement = '''Console.WriteLine("TEN-CALL MEASUREMENT START");
  for(int sample=0;sample<10;sample++){
   long started=Stopwatch.GetTimestamp();result=run();long ticks=Stopwatch.GetTimestamp()-started;count++;
   Console.WriteLine("DIAGNOSTIC CALL "+sample+" ticks="+ticks+" frequency="+Stopwatch.Frequency);
  }
  Console.WriteLine("TEN-CALL MEASUREMENT END");'''
assert content.count('for(int i=0;i<1000;i++)result=run();') == 1
content = content.replace('for(int i=0;i<1000;i++)result=run();', 'for(int i=0;i<20000;i++)result=run();')
driver.write_text(content.replace(needle, replacement))
env = os.environ.copy()
for key in ('DOTNET_JitStdOutFile', 'COMPlus_JitStdOutFile', 'COMPlus_JitDisasm'):
    env.pop(key, None)
env['DOTNET_JitDisasm'] = 'OoplesFinance.StockIndicators.Indicators.ContextReversalState:Matches OoplesFinance.StockIndicators.Indicators.ContextReversalState:Update'
results = []
for pair in ('TaLib.Candles.InvertedHammer', 'TaLib.Candles.ShootingStar'):
    for bars in (1000,):
        for label in ('old', 'new'):
            log = out / (pair.split('.')[-1] + '-' + str(bars) + '-' + label + '.log')
            with log.open('wb') as output:
                subprocess.run(['pwsh', '-NoProfile', '-File', str(driver), '-Pair', pair,
                                '-Bars', str(bars), '-Workers', '4', '-Runtime', str(runtimes[label]),
                                '-Expected', hashes[label]], env=env, stdout=output, stderr=subprocess.STDOUT, check=True)
            text = log.read_text(encoding='utf-8-sig')
            marker = text.index('TEN-CALL MEASUREMENT START')
            calls = re.findall(r'DIAGNOSTIC CALL (\d+) ticks=(\d+) frequency=(\d+)', text)
            assert len(calls) == 10
            headers = [(match.start(), match.group()) for match in re.finditer(r'; Assembly listing for method [^\n]*ContextReversalState[^\n]*', text)]
            optimized = [(position, header) for position, header in headers
                         if any(tier in header for tier in ('(Tier1)', '(Tier1-OSR)', '(Tier0-FullOpts)'))]
            row = dict(pair=pair, bars=bars, label=label,
                       optimized_headers_before_measurement=[header for position, header in optimized if position < marker],
                       optimized_headers_after_measurement_marker=[header for position, header in optimized if position >= marker],
                       measured_call_us=[int(ticks)*1e6/int(frequency) for _, ticks, frequency in calls],
                       log_sha256=hashlib.sha256(log.read_bytes()).hexdigest())
            results.append(row)
            print(json.dumps(row), flush=True)
(out / 'verification.json').write_text(json.dumps(dict(source=q['source'], hashes=hashes, warmups=20000,
    calls=10, workers=4, rows=results, production_changed=False, qualification=False,
    interpretation='Diagnostic stdout ordering only. A compiled optimized header is not proof every worker invocation used it. These external-loop timings are not BDN measurements, native comparisons, or policy adoption evidence.'), indent=2) + '\n')
