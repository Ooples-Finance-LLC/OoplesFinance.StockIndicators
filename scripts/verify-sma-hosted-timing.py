import hashlib,json,os,re,sys
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parents[1]/'benchmarks'))
from paired_batch_timing import statistics

def collect(data, fixture, manifest):
    expected={(size,method) for size in (1000,10000) for method in ('Ooples','Competitor')}
    rows={}
    for row in data['Benchmarks']:
        match=re.fullmatch(r'(?:\w+\.)*LibraryPairBenchmarks\.(Ooples|Competitor)\((.*)\)',row['FullName'])
        assert match, row['FullName']
        params={}
        for token in match[2].split(','):
            key,value=token.split(':',1); key=key.strip()
            assert key not in params
            params[key]=value.strip().strip('"')
        assert set(params)=={'Bars','PairId'} and params['PairId']==manifest['pair'],params
        key=(int(params['Bars']),match[1])
        assert key in expected and key not in rows and row['Method']==match[1]
        rows[key]=statistics(row)
    assert rows.keys()==expected
    runtime=data.get('HostEnvironmentInfo',{}).get('RuntimeVersion','')
    assert re.search(r'(?<![\d.])10\.0\.13(?![\d.])',runtime),runtime
    results=[]
    for size in (1000,10000):
        ours, theirs=rows[size,'Ooples'],rows[size,'Competitor']
        results.append(dict(pair=manifest['pair'],bars=size,ooples=ours,competitor=theirs,
            competitor_over_ooples=theirs['mean_ns']/ours['mean_ns'],
            verdict='faster' if ours['upper_ns']<theirs['lower_ns'] else 'slower' if ours['lower_ns']>theirs['upper_ns'] else 'inconclusive'))
    return dict(runtime=runtime,rows=results)


if __name__=='__main__':
    reports=list(Path('performance/results').glob('*-report-full-compressed.json'))
    assert len(reports)==1,reports
    data=json.loads(reports[0].read_text())
    result=collect(data,'LibraryPairBenchmarks',{'pair':'Skender.GetSmaAnalysis'})
    result.update(source_sha='80634d449d4a2eb1e580ad3caf3763e61c556fc1',binary_run=39713589879,
        workflow_sha=os.environ['GITHUB_SHA'],run_url=os.environ['GITHUB_SERVER_URL']+'/'+os.environ['GITHUB_REPOSITORY']+'/actions/runs/'+os.environ['GITHUB_RUN_ID'],
        report_sha256=hashlib.sha256(reports[0].read_bytes()).hexdigest(),
        benchmark_binary_sha256=hashlib.sha256(Path('binaries/OoplesFinance.StockIndicators.CompetitorBenchmarks.dll').read_bytes()).hexdigest(),
        production_binary_sha256=hashlib.sha256(Path('binaries/OoplesFinance.StockIndicators.dll').read_bytes()).hexdigest(),
        note='Dedicated hosted SMA comparison only; confidence intervals are retained; this does not establish performance for other indicators.')
    Path('performance/sma-comparison.json').write_text(json.dumps(result,indent=2)+'\n')
    print(json.dumps(result,indent=2))
