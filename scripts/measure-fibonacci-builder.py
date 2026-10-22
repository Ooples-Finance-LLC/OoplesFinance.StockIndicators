from pathlib import Path
import hashlib,json,math,os,re,subprocess,sys
SOURCE='29a41d8b7956a05812a5950631f07b84bcb04ee0';RUN=39719116665
root=Path.cwd();out=root/'measurement';out.mkdir(exist_ok=True)
repository='Ooples-Finance-LLC/OoplesFinance.StockIndicators'
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
assert sys.argv[1] in ['verify','measure']
if sys.argv[1]=='verify':
 run=json.loads(subprocess.check_output(['gh','api',f'repos/{repository}/actions/runs/{RUN}']))
 assert run['head_sha']==SOURCE and run['conclusion']=='success'
 subprocess.run(['git','fetch','--no-tags','--depth=1','origin',SOURCE],check=True)
 changed=subprocess.check_output(['git','diff','--name-only',SOURCE,'HEAD','--','src','benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks'],text=True).splitlines()
 assert changed==['src/Helpers/CertifiedMeanRelativeError.cs'],changed
 # The only changed production helper is SMA relative-error accumulation;
 # Fibonacci uses its unchanged exact weighted-mean accumulator and fixture.
 for name in ['src/Indicators/FibonacciWeightedAverage.cs','src/Helpers/ExactMeanAccumulator.cs','benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks/LibraryComparisons/SharedFibonacciWeightedBenchmarks.cs']:
  assert not subprocess.check_output(['git','diff',SOURCE,'HEAD','--',name])
 (out/'provenance.json').write_text(json.dumps({'binary_run':run,'source_sha':SOURCE,'changed_production_files_since_baseline':changed,'note':'Existing qualified source/binaries; no production changes or new builds for this measurement. Fibonacci implementation and fixture are unchanged.'},indent=2))
else:
 assert json.loads((out/'provenance.json').read_text())['source_sha']==SOURCE
 binary=root/'binaries';hashes={p.relative_to(binary).as_posix():digest(p) for p in binary.rglob('*') if p.is_file()}
 (out/'binary-manifest.json').write_text(json.dumps(hashes,indent=2))
 subprocess.run(['dotnet',str(binary/'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'),'--filter','*SharedFibonacciWeightedBenchmarks*','--warmupCount','10','--iterationCount','10','--exporters','json','--artifacts',str(out/'artifacts')],cwd=binary,check=True)
 reports=list((out/'artifacts/results').glob('*-report-full-compressed.json'));assert len(reports)==1
 raw=json.loads(reports[0].read_bytes());expected={(n,m) for n in [1000,10000] for m in ['BuilderFull','BuilderLatestOnly','NativeSingle','NativeParallel']};rows={}
 for row in raw['Benchmarks']:
  name=row['FullName'];assert 'SharedFibonacciWeightedBenchmarks.' in name and 'Operation: "QuanTAlib.Fwma"' in name
  count=int(re.search(r'Count: (\d+)',name)[1]);key=(count,row['Method']);assert key in expected and key not in rows
  stats=row['Statistics'];assert stats['N']==len(stats['OriginalValues']) and stats['N']>=7
  assert all(math.isfinite(x) and x>0 for x in stats['OriginalValues'])
  assert math.isclose(sum(stats['OriginalValues'])/stats['N'],stats['Mean'],rel_tol=1e-12)
  interval=stats['ConfidenceInterval'];assert math.isfinite(interval['Lower']) and interval['Lower']<=stats['Mean']<=interval['Upper']
  rows[key]={'mean_ns':stats['Mean'],'lower_ns':interval['Lower'],'upper_ns':interval['Upper'],'samples':stats['N'],'allocated_bytes':row['Memory']['BytesAllocatedPerOperation']}
 assert rows.keys()==expected
 comparisons=[]
 for count in [1000,10000]:
  native={m:rows[count,m] for m in ['NativeSingle','NativeParallel']}
  for mode in ['BuilderFull','BuilderLatestOnly']:
   ours=rows[count,mode];fastest=min(native,key=lambda m:native[m]['mean_ns'])
   verdict='faster' if ours['upper_ns']<min(n['lower_ns'] for n in native.values()) else 'slower' if ours['lower_ns']>min(n['upper_ns'] for n in native.values()) else 'inconclusive'
   comparisons.append({'pair':'QuanTAlib.Fwma','bars':count,'mode':mode,'ooples':ours,'native':native,'fastest_native':fastest,'verdict':verdict})
 for name,h in hashes.items():assert digest(binary/name)==h
 (out/'comparison.json').write_text(json.dumps({'source_sha':SOURCE,'binary_run':RUN,'run_id':os.environ['GITHUB_RUN_ID'],'workflow_sha':os.environ['GITHUB_SHA'],'report_sha256':digest(reports[0]),'report_file':str(reports[0].relative_to(out)),'host':raw['HostEnvironmentInfo'],'comparisons':comparisons,'complete':True},indent=2))
