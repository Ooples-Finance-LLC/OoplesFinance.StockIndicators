from pathlib import Path
import hashlib,json,math,os,re,shutil,subprocess,sys
OLD='29a41d8b7956a05812a5950631f07b84bcb04ee0'
NEW=os.environ['CANDIDATE_SHA']
QUALIFIED_SOURCE='8c895af7c2e77632e6523ec7b348289a61d7b4f0'
RUN=39719116665
root=Path.cwd();candidate=root/'candidate';out=root/'control';out.mkdir(exist_ok=True)
repository='Ooples-Finance-LLC/OoplesFinance.StockIndicators'
PAIRS=['QuanTAlib.Dema','QuanTAlib.Tema']
METHODS=['Ooples','Competitor']

def git(*args):return subprocess.check_output(['git','-C',str(candidate),*args],text=True).strip()
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
assert re.fullmatch('[a-f0-9]{40}',NEW)
assert sys.argv[1] in ['verify','measure']
if sys.argv[1]=='verify':
 run=json.loads(subprocess.check_output(['gh','api',f'repos/{repository}/actions/runs/{RUN}']))
 assert run['head_sha']==OLD and run['conclusion']=='success'
 assert git('rev-parse','HEAD')==NEW
 subprocess.run(['git','-C',str(candidate),'fetch','--no-tags','--depth=1','origin',OLD],check=True)
 changes=git('diff','--name-only',OLD,NEW,'--','src','benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks').splitlines()
 assert changes==['src/Helpers/CertifiedMeanRelativeError.cs','src/Indicators/CompensatedExponentialAverage.cs'],changes
 # SMA error diagnostics do not participate in either selected EMA fixture.
 qualification=json.loads((candidate/'benchmarks/results/remaining-indicators/publication-tema-factored-20261022-verification.json').read_text())
 assert qualification['input_head']==QUALIFIED_SOURCE
 assert not git('diff','--name-only',QUALIFIED_SOURCE,NEW,'--','src','tests','benchmarks/OoplesFinance.StockIndicators.CompetitorTests')
 assert qualification['gpu_passed']==100 and qualification['unit']['passed']==16 and qualification['parity']['passed']==19
 assert qualification['inventory_tests_passed']==8
 for check in ['build-net461','build-net10','build-unit','build-comparator']:assert qualification['checks'][check]=='passed'
 (out/'provenance.json').write_text(json.dumps({'old_run':run,'old_source':OLD,'new_source':NEW,'changes':changes,'qualification':qualification},indent=2))
else:
 proof=json.loads((out/'provenance.json').read_text());assert proof['new_source']==NEW and git('rev-parse','HEAD')==NEW
 shutil.copytree(root/'old-binaries',root/'new-binaries')
 shutil.copyfile(candidate/'src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll',root/'new-binaries/OoplesFinance.StockIndicators.dll')
 old_files={p.relative_to(root/'old-binaries').as_posix():digest(p) for p in (root/'old-binaries').rglob('*') if p.is_file()}
 new_files={p.relative_to(root/'new-binaries').as_posix():digest(p) for p in (root/'new-binaries').rglob('*') if p.is_file()}
 assert old_files.keys()==new_files.keys()
 assert [p for p in old_files if old_files[p]!=new_files[p]]==['OoplesFinance.StockIndicators.dll']
 (out/'binary-manifest.json').write_text(json.dumps({'old':old_files,'new':new_files},indent=2))
 rounds=[]
 for index,label in enumerate(['new']):
  source=OLD if label=='old' else NEW;binary=root/(label+'-binaries');folder=out/(str(index)+'-'+label)
  subprocess.run(['dotnet',str(binary/'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'),'--filter',*[f'*LibraryPairBenchmarks*{pair}*' for pair in PAIRS],'--warmupCount','10','--iterationCount','10','--exporters','json','--artifacts',str(folder)],cwd=binary,check=True)
  reports=list((folder/'results').glob('*-report-full-compressed.json'));assert len(reports)==1
  raw=json.loads(reports[0].read_bytes());rows={}
  for row in raw['Benchmarks']:
   name=row['FullName'];assert 'LibraryPairBenchmarks.' in name
   pair=re.search(r'PairId: "([^"]+)"',name)[1];count=int(re.search(r'Bars: (\d+)',name)[1]);key=pair,count,row['Method'];assert key not in rows
   stats=row['Statistics'];values=stats['OriginalValues'];ci=stats['ConfidenceInterval']
   assert stats['N']==len(values) and stats['N']>=7
   assert all(math.isfinite(x) and x>0 for x in values)
   assert math.isclose(sum(values)/len(values),stats['Mean'],rel_tol=1e-12)
   assert math.isfinite(ci['Lower']) and ci['Lower']<=stats['Mean']<=ci['Upper']
   rows[key]={'pair':pair,'bars':count,'method':row['Method'],'mean_ns':stats['Mean'],'lower_ns':ci['Lower'],'upper_ns':ci['Upper'],'samples':stats['N'],'allocated_bytes':row['Memory']['BytesAllocatedPerOperation']}
  assert rows.keys()=={(p,n,m) for p in PAIRS for n in [1000,10000] for m in METHODS}
  rounds.append({'order':index,'version':label,'source_sha':source,'rows':list(rows.values()),'report_sha256':digest(reports[0]),'report_file':str(reports[0].relative_to(root))})
  (out/'comparison.json').write_text(json.dumps({'pairs':PAIRS,'workflow_sha':os.environ['GITHUB_SHA'],'run_id':os.environ['GITHUB_RUN_ID'],'order':['new'],'rounds':rounds,'complete':len(rounds)==1},indent=2))
 for label,files in [('old',old_files),('new',new_files)]:
  for name,h in files.items():assert digest(root/(label+'-binaries')/name)==h
 assert len(rounds)==1
