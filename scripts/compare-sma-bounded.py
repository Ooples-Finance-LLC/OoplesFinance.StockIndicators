from pathlib import Path
import hashlib,importlib.util,json,os,shutil,subprocess,sys
OLD='29a41d8b7956a05812a5950631f07b84bcb04ee0'
NEW='e4c3cbf1771d87c39ff05b28d7b7891b66e68b12'
RUN=39719116665
root=Path.cwd();candidate=root/'candidate';out=root/'control';out.mkdir(exist_ok=True)
repository='Ooples-Finance-LLC/OoplesFinance.StockIndicators'
def git(*args):return subprocess.check_output(['git','-C',str(candidate),*args],text=True).strip()
def digest(path):return hashlib.sha256(path.read_bytes()).hexdigest()
assert sys.argv[1] in ['verify','measure']
if sys.argv[1]=='verify':
 run=json.loads(subprocess.check_output(['gh','api',f'repos/{repository}/actions/runs/{RUN}']))
 jobs=json.loads(subprocess.check_output(['gh','api',f'repos/{repository}/actions/runs/{RUN}/jobs?per_page=100']))
 build=[j for j in jobs['jobs'] if j['name']=='build']
 assert run['head_sha']==OLD and run['conclusion']=='success' and len(build)==1 and build[0]['conclusion']=='success'
 assert git('rev-parse','HEAD')==NEW
 subprocess.run(['git','-C',str(candidate),'fetch','--no-tags','--depth=1','origin',OLD],check=True)
 changes=git('diff','--name-only',OLD,NEW,'--','src','benchmarks/OoplesFinance.StockIndicators.CompetitorBenchmarks').splitlines()
 assert changes==['src/Helpers/CertifiedMeanRelativeError.cs'],changes
 qualification=json.loads((candidate/'benchmarks/results/remaining-indicators/publication-sma-bounded-accumulation-20261022-verification.json').read_text())
 assert qualification['input_head']=='805569ffb3534fd65d4601561b7ab362d3873690'
 assert not git('diff','--name-only',qualification['input_head'],NEW,'--','src')
 assert qualification['gpu_passed']==100 and qualification['unit']['passed']==44 and qualification['parity']['passed']==2
 assert qualification['inventory_tests_passed']==8
 for check in ['build-net461','build-net10','build-unit','build-comparator']:assert qualification['checks'][check]=='passed'
 (out/'provenance.json').write_text(json.dumps({'old_run':run,'old_build':build[0],'old_source':OLD,'new_source':NEW,'changes':changes,'qualification':qualification},indent=2))
else:
 proof=json.loads((out/'provenance.json').read_text());assert proof['new_source']==NEW and git('rev-parse','HEAD')==NEW
 pair=os.environ['COMPARISON_PAIR'];assert pair=='Skender.GetSmaAnalysis'
 shutil.copytree(root/'old-binaries',root/'new-binaries')
 shutil.copyfile(candidate/'src/bin/Release/net10.0/OoplesFinance.StockIndicators.dll',root/'new-binaries/OoplesFinance.StockIndicators.dll')
 old_files={p.relative_to(root/'old-binaries').as_posix():digest(p) for p in (root/'old-binaries').rglob('*') if p.is_file()}
 new_files={p.relative_to(root/'new-binaries').as_posix():digest(p) for p in (root/'new-binaries').rglob('*') if p.is_file()}
 assert old_files.keys()==new_files.keys()
 assert [p for p in old_files if old_files[p]!=new_files[p]]==['OoplesFinance.StockIndicators.dll']
 (out/'binary-manifest.json').write_text(json.dumps({'old':old_files,'new':new_files},indent=2))
 spec=importlib.util.spec_from_file_location('statistics',root/'scripts/verify-sma-hosted-timing.py');module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
 rounds=[]
 for index,label in enumerate(['old','new','new','old']):
  source=OLD if label=='old' else NEW;binary=root/(label+'-binaries');folder=out/(str(index)+'-'+label)
  subprocess.run(['dotnet',str(binary/'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'),'--filter','*LibraryPairBenchmarks.Ooples*','*LibraryPairBenchmarks.Competitor*','--warmupCount','10','--iterationCount','10','--exporters','json','--artifacts',str(folder)],cwd=binary,check=True)
  reports=list((folder/'results').glob('*-report-full-compressed.json'));assert len(reports)==1
  raw=reports[0].read_bytes();receipt=module.collect(json.loads(raw),'LibraryPairBenchmarks',{'pair':pair})
  receipt.update(order=index,version=label,source_sha=source,report_sha256=digest(reports[0]),report_file=str(reports[0].relative_to(root)),binary_hashes={name:digest(binary/name) for name in ['OoplesFinance.StockIndicators.dll','OoplesFinance.StockIndicators.CompetitorBenchmarks.dll']})
  rounds.append(receipt)
  (out/'comparison.json').write_text(json.dumps({'pair':pair,'workflow_sha':os.environ['GITHUB_SHA'],'run_id':os.environ['GITHUB_RUN_ID'],'order':['old','new','new','old'],'rounds':rounds,'complete':len(rounds)==4},indent=2))
 for label,files in [('old',old_files),('new',new_files)]:
  for name,h in files.items():assert digest(root/(label+'-binaries')/name)==h
 assert len(rounds)==4
