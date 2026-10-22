from pathlib import Path
import hashlib,importlib.util,json,os,subprocess,sys
versions={'old':(39719116665,'29a41d8b7956a05812a5950631f07b84bcb04ee0'),'new':(39721035437,'cfb6d342a5fe35c2c48df46937d2ab3ddaf25e06')}
root=Path.cwd();out=root/'control';out.mkdir(exist_ok=True)
repository='Ooples-Finance-LLC/OoplesFinance.StockIndicators'
assert sys.argv[1] in ['verify','measure']
if sys.argv[1]=='verify':
    proof={}
    for label,(run_id,source) in versions.items():
        run=json.loads(subprocess.check_output(['gh','api',f'repos/{repository}/actions/runs/{run_id}']))
        jobs=json.loads(subprocess.check_output(['gh','api',f'repos/{repository}/actions/runs/{run_id}/jobs?per_page=100']))
        build=[j for j in jobs['jobs'] if j['name']=='build']
        assert run['head_sha']==source and len(build)==1 and build[0]['conclusion']=='success'
        proof[label]={'run':run,'build':build[0]}
    (out/'provenance.json').write_text(json.dumps(proof,indent=2))
else:
    pair=os.environ['COMPARISON_PAIR'];assert pair in ['TaLib.Functions.Sar','TaLib.Functions.SarExt']
    proof=json.loads((out/'provenance.json').read_text())
    module_path=root/'scripts/verify-sma-hosted-timing.py'
    spec=importlib.util.spec_from_file_location('statistics',module_path);module=importlib.util.module_from_spec(spec);spec.loader.exec_module(module)
    rounds=[]
    for index,label in enumerate(['old','new','new','old']):
        run_id,source=versions[label];assert proof[label]['run']['head_sha']==source
        binary=root/(label+'-binaries');folder=out/(str(index)+'-'+label)
        subprocess.run(['dotnet',str(binary/'OoplesFinance.StockIndicators.CompetitorBenchmarks.dll'),'--filter','*LibraryPairBenchmarks.Ooples*','*LibraryPairBenchmarks.Competitor*','--warmupCount','10','--iterationCount','10','--exporters','json','--artifacts',str(folder)],cwd=binary,check=True)
        reports=list((folder/'results').glob('*-report-full-compressed.json'));assert len(reports)==1
        raw=reports[0].read_bytes();receipt=module.collect(json.loads(raw),'LibraryPairBenchmarks',{'pair':pair})
        receipt.update(order=index,version=label,source_sha=source,binary_run=run_id,report_sha256=hashlib.sha256(raw).hexdigest(),report_file=str(reports[0].relative_to(root)),binary_hashes={name:hashlib.sha256((binary/name).read_bytes()).hexdigest() for name in ['OoplesFinance.StockIndicators.dll','OoplesFinance.StockIndicators.CompetitorBenchmarks.dll']})
        rounds.append(receipt)
        (out/'comparison.json').write_text(json.dumps({'pair':pair,'workflow_sha':os.environ['GITHUB_SHA'],'run_id':os.environ['GITHUB_RUN_ID'],'order':['old','new','new','old'],'rounds':rounds,'complete':len(rounds)==4},indent=2))
    assert len(rounds)==4
