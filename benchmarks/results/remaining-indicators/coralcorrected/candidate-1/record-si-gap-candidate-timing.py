"""Archive one fresh Shared-only run and compare it with immutable saved baselines."""
from pathlib import Path
import gzip,hashlib,itertools,json,math,re,runpy,shutil,sys,datetime
Z=Path('Z:/StockIndicators-work')
Q=Z/'si-followon-performance-20261018'
reviewer=runpy.run_path(str(Z/'si-review-remaining-timing-v2.py'))
def digest(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def parse_candidate(report,stage):
    assert '10.0.12' in report['HostEnvironmentInfo']['RuntimeVersion']
    rows={}
    for row in report['Benchmarks']:
        name=row['FullName'];assert stage['fixture']+'.' in name and row['Method']=='Shared', 'Candidate run must contain Shared only'
        key=(re.search(r'Operation: "([^"]+)"',name)[1],int(re.search(r'Count: (\d+)',name)[1]),re.search(r'History: (Full|LatestOnly)',name)[1])
        assert key not in rows, 'Duplicate candidate case'
        stats=row['Statistics'];assert stats['N']>=7
        value={'mean_ns':stats['Mean'],'confidence_interval':stats['ConfidenceInterval'],'allocated_bytes':row['Memory']['BytesAllocatedPerOperation']}
        reviewer['arm'](value);rows[key]=value
    expected=set(itertools.product(stage['operations'],(1000,10000),('Full','LatestOnly')))
    assert set(rows)==expected,'Missing or extra candidate cases'
    return rows

def compare_candidate(rows,baseline):
    result=[]
    for old in baseline['comparisons']:
        key=(old['operation'],old['count'],old['history']);new=rows[key]
        comparisons={}
        for arm in ('legacy','shared'):
            record={k:old[k] for k in ('operation','count','history')}
            record.update(legacy=old[arm],shared=new,shared_to_legacy_ratio=new['mean_ns']/old[arm]['mean_ns'])
            comparisons[arm]=reviewer['compare'](record)
        result.append({'operation':key[0],'count':key[1],'history':key[2],'candidate':new,'versus_saved_legacy':comparisons['legacy'],'versus_saved_shared':comparisons['shared']})
    assert len(result)==len(rows)
    return result

def main():
    stage,=[s for s in json.loads((Q/'si-remaining-timing-stages.json').read_text()) if s['name']==sys.argv[1]]
    assert stage['candidate_only'] is True and re.fullmatch(r'si-[a-z0-9-]+',sys.argv[2])
    source=Q/sys.argv[2];snapshot=Path(stage['snapshot']);manifest=json.loads((snapshot/'manifest.json').read_text())
    for name,h in manifest['files_sha256'].items():assert digest(snapshot/name)==h,name
    destination=Path(stage['destination']);assert not destination.exists(),'Never overwrite partial or completed evidence'
    state=json.loads(Path(str(source)+'.exit').read_text(encoding='utf-8-sig'));assert state['ExitCode']==0 and not state['Interrupted']
    pid=int(Path(str(source)+'.pid').read_text());samples=[json.loads(s) for s in Path(str(source)+'.processes.jsonl').read_text(encoding='utf-8-sig').splitlines() if s]
    runpy.run_path(str(Q/'verify-si-quiet-evidence.py'))['verify_quiet_evidence'](source,pid,samples)
    quiet=json.loads(Path(str(source)+'.quiet.json').read_text(encoding='utf-8-sig'))
    assert (datetime.datetime.fromisoformat(quiet[-1]['Time'].replace('Z','+00:00'))-datetime.datetime.fromisoformat(quiet[0]['Time'].replace('Z','+00:00'))).total_seconds()>=240, 'Candidate requires 240-second quiet window'
    report_path,=(source/'results').glob('*'+stage['fixture']+'*full-compressed.json')
    rows=parse_candidate(json.loads(report_path.read_text(encoding='utf-8-sig')),stage)
    baseline=reviewer['review_stage'](stage['baseline_stage']);assert baseline['status']=='reviewed-same-library-only'
    comparisons=compare_candidate(rows,baseline)
    destination.mkdir(parents=True)
    shutil.copyfile(snapshot/'manifest.json',destination/'preserved-candidate-manifest.json');shutil.copyfile(report_path,destination/'report-full-compressed.json')
    for suffix in ('.log','.stderr.log','.processes.jsonl'):
        (destination/(suffix[1:]+'.gz')).write_bytes(gzip.compress(Path(str(source)+suffix).read_bytes(),mtime=0))
    for suffix,name in (('.exit','runner-exit.json'),('.quiet.json','quiet-preflight.json')):shutil.copyfile(Path(str(source)+suffix),destination/name)
    for name in ('si-quiet-process-monitor.ps1','verify-si-quiet-evidence.py','verify-si-remaining-snapshot.py','record-si-gap-candidate-timing.py','run-si-remaining-'+stage['name']+'.ps1'):shutil.copyfile(Q/name,destination/name)
    shutil.copyfile(Z/'si-review-remaining-timing-v2.py',destination/'baseline-reviewer.py')
    (destination/'.gitattributes').write_bytes(b'* -text\n')
    summary={'status':'reviewed-candidate-only-same-library','candidate_base_commit':manifest['commit'],'candidate_input_sha256':manifest['input_sha256'],'commit_is_base_only':manifest['commit_is_base_only'],'baseline_stage':stage['baseline_stage'],'baseline_archive_sha256':baseline['archive_sha256'],'comparisons':comparisons,'qualification':'Candidate-only timings against saved historical arms. No native-competitor qualification. Inspect historical-run variability and outstanding correctness/hosted gates.'}
    (destination/'summary.json').write_text(json.dumps(summary,indent=2)+'\n')
    (destination/'archive-sha256.json').write_text(json.dumps({p.name:digest(p) for p in destination.iterdir() if p.is_file()},indent=2)+'\n')
    print('Archived',len(rows),'Shared-only cases against preserved baselines; inspect performance results.')
if __name__=='__main__':main()
